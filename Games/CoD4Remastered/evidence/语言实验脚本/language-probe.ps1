param(
  [Parameter(Mandatory=$true)][string]$Lang,
  [int]$WaitSec = 22,
  [string]$Shot = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$dir = 'D:\YunGame\CoD\CoD4.Remastered'
$bak = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\backup'

# 外科式替换：字节级只改 Language= 那一行，保留中文注释与 CRLF、不整份重写
function Set-Lang([string]$path, [string]$val) {
  $b = [IO.File]::ReadAllBytes($path)
  $t = [Text.Encoding]::ASCII.GetString($b)
  $m = [regex]::Match($t, 'Language\s*=\s*[^\r\n]*')
  if (-not $m.Success) { throw "找不到 Language 行：$path" }
  $oldBytes = [Text.Encoding]::ASCII.GetBytes($m.Value)
  $newBytes = [Text.Encoding]::ASCII.GetBytes('Language=' + $val)
  $out = New-Object byte[] ($b.Length - $oldBytes.Length + $newBytes.Length)
  [Array]::Copy($b, 0, $out, 0, $m.Index)
  [Array]::Copy($newBytes, 0, $out, $m.Index, $newBytes.Length)
  [Array]::Copy($b, $m.Index + $oldBytes.Length, $out,
    $m.Index + $newBytes.Length, $b.Length - $m.Index - $oldBytes.Length)
  [IO.File]::WriteAllBytes($path, $out)
}

function Show-Lang([string]$name) {
  $t = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $dir $name)))
  return ([regex]::Match($t, 'Language\s*=\s*[^\r\n]*').Value)
}

if (-not $Shot) { $Shot = "D:\AI\legacy-game-compat\Games\CoD4Remastered\evidence\probe-$Lang.png" }

Set-Lang (Join-Path $dir 'steam_emu.ini') $Lang
Set-Lang (Join-Path $dir 'CONFIG.ini') $Lang
Write-Host "[$Lang] 写入后：steam_emu.ini=$(Show-Lang 'steam_emu.ini') / CONFIG.ini=$(Show-Lang 'CONFIG.ini')"

$p = $null
try {
  $p = Start-Process -FilePath (Join-Path $dir 'h1_sp64_ship.exe') `
    -WorkingDirectory $dir -ArgumentList '+set r_fullscreen 0' -PassThru
  Start-Sleep -Seconds 6
  # 跳过开场 logo / 任意键提示
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds 4
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds ([Math]::Max(1, $WaitSec - 10))
  $alive = -not $p.HasExited
  Write-Host "[$Lang] 进程存活=$alive PID=$($p.Id)"
  if ($alive) { Get-Process -Id $p.Id | Select-Object Id, MainWindowTitle, Responding | Format-Table | Out-String | Write-Host }

  $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object Drawing.Bitmap($bounds.Width, $bounds.Height)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
  $g.Dispose()
  New-Item -ItemType Directory -Force -Path (Split-Path $Shot) | Out-Null
  $bmp.Save($Shot, [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "[$Lang] 截图：$Shot ($($bounds.Width)x$($bounds.Height))"
}
finally {
  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 500
  foreach ($n in 'steam_emu.ini','CONFIG.ini') {
    Copy-Item (Join-Path $bak ($n + '.原版')) (Join-Path $dir $n) -Force
  }
  Write-Host "[$Lang] 已恢复原值：steam_emu.ini=$(Show-Lang 'steam_emu.ini') / CONFIG.ini=$(Show-Lang 'CONFIG.ini')"
}
