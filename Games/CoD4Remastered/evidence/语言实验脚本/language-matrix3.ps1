param([int]$WaitSec = 16)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$dir = 'D:\YunGame\CoD\CoD4.Remastered'
$bak = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\backup'
$ev  = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\evidence'

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

function Run-Case([string]$name, [string[]]$extraArgs, [hashtable]$envVars) {
  Write-Host "===== $name : 参数=[$($extraArgs -join ' ')] 环境变量=[$($envVars.Keys -join ',')] ====="
  $psi = New-Object Diagnostics.ProcessStartInfo
  $psi.FileName = Join-Path $dir 'h1_sp64_ship.exe'
  $psi.WorkingDirectory = $dir
  $psi.UseShellExecute = $false
  $psi.Arguments = ($extraArgs -join ' ')
  foreach ($k in $envVars.Keys) { $psi.EnvironmentVariables[$k] = $envVars[$k] }
  $p = [Diagnostics.Process]::Start($psi)
  Start-Sleep -Seconds 6
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds 4
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds ([Math]::Max(1, $WaitSec - 10))
  Write-Host ("进程存活=" + (-not $p.HasExited))

  $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object Drawing.Bitmap($bounds.Width, $bounds.Height)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
  $g.Dispose()
  $shot = Join-Path $ev ("probe3-$name.png")
  $bmp.Save($shot, [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "截图：$shot"

  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
}

try {
  # 两个 ini 全部设 english，再叠加强制手段
  Set-Lang (Join-Path $dir 'steam_emu.ini') 'english'
  Set-Lang (Join-Path $dir 'CONFIG.ini') 'english'

  Run-Case 'env-lang-en' @('+set r_fullscreen 0') @{ 'LANG' = 'en_US.UTF-8'; 'LANGUAGE' = 'en_US'; 'LC_ALL' = 'en_US.UTF-8' }
  Run-Case 'cvar-language-0' @('+set r_fullscreen 0', '+set language 0') @{}
  Run-Case 'cvar-loc-language' @('+set r_fullscreen 0', '+set loc_language english') @{}
}
finally {
  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  foreach ($n in 'steam_emu.ini','CONFIG.ini') {
    Copy-Item (Join-Path $bak ($n + '.原版')) (Join-Path $dir $n) -Force
  }
  Write-Host '===== 已恢复两个 ini ====='
}
