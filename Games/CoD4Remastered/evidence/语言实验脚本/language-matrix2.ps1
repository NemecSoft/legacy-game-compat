param([int]$WaitSec = 18)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$dir = 'D:\YunGame\CoD\CoD4.Remastered'
$bak = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\backup'
$ev  = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\evidence'
$steamKey = 'HKCU:\Software\Valve\Steam'
$schDir = Join-Path $dir 'simplified_chinese'
$schOff = Join-Path $dir 'simplified_chinese.off'

$origReg = (Get-ItemProperty $steamKey -Name Language -ErrorAction SilentlyContinue).Language
Write-Host "注册表原始 Language = $origReg"

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

function Run-Case([string]$name, [string]$reg, [string]$emu, [string]$cfg, [bool]$hideSch) {
  Write-Host "===== $name : 注册表=$reg / emu=$emu / cfg=$cfg / 藏起简中包=$hideSch ====="
  Set-ItemProperty $steamKey -Name Language -Value $reg -Type String
  Set-Lang (Join-Path $dir 'steam_emu.ini') $emu
  Set-Lang (Join-Path $dir 'CONFIG.ini') $cfg
  if ($hideSch) {
    if (Test-Path $schDir) { Rename-Item $schDir 'simplified_chinese.off' }
  } else {
    if (Test-Path $schOff) { Rename-Item $schOff 'simplified_chinese' }
  }

  $p = Start-Process -FilePath (Join-Path $dir 'h1_sp64_ship.exe') `
    -WorkingDirectory $dir -ArgumentList '+set r_fullscreen 0' -PassThru
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
  $shot = Join-Path $ev ("probe2-$name.png")
  $bmp.Save($shot, [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "截图：$shot"

  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
}

try {
  Run-Case 'reg-en-ini-orig'  'english'  'english'  'schinese' $false
  Run-Case 'reg-en-ini-en'   'english'  'english'  'english'  $false
  Run-Case 'reg-en-hide-sch' 'english'  'english'  'english'  $true
}
finally {
  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  if (Test-Path $schOff) { Rename-Item $schOff 'simplified_chinese' }
  if (-not (Test-Path $schDir)) { throw '严重：simplified_chinese 目录未恢复！' }
  if ($origReg) { Set-ItemProperty $steamKey -Name Language -Value $origReg -Type String }
  foreach ($n in 'steam_emu.ini','CONFIG.ini') {
    Copy-Item (Join-Path $bak ($n + '.原版')) (Join-Path $dir $n) -Force
  }
  Write-Host '===== 已恢复：注册表 / 语言包目录 / 两个 ini ====='
  Write-Host ("注册表 Language = " + (Get-ItemProperty $steamKey -Name Language).Language)
}
