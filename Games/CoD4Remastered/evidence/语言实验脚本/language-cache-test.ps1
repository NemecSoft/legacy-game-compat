param([int]$WaitSec = 20)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$dir  = 'D:\YunGame\CoD\CoD4.Remastered'
$bak  = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\backup'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\evidence'
$tmp  = Join-Path $bak 'players2.实验暂存'
$codx = 'C:\Users\Public\Documents\Steam\CODEX\393080'
$codxBak = Join-Path $bak 'CODEX存档.实验暂存'
$steamKey = 'HKCU:\Software\Valve\Steam'
$origReg = (Get-ItemProperty $steamKey -Name Language).Language

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

try {
  # 1) 把 players2 与 CODEX 存档整体移到暂存（不删用户任何东西）
  if (Test-Path $tmp) { throw "暂存目录已存在，先手工检查：$tmp" }
  if (Test-Path $codxBak) { throw "CODEX 暂存已存在，先手工检查：$codxBak" }
  Move-Item (Join-Path $dir 'players2') $tmp
  if (Test-Path $codx) { Move-Item $codx $codxBak }
  Write-Host '已把 players2 / CODEX 存档移到暂存（完整保留，未删除）'

  # 2) 全设英文（两个 ini + Steam 注册表）
  Set-ItemProperty $steamKey -Name Language -Value 'english' -Type String
  Set-Lang (Join-Path $dir 'steam_emu.ini') 'english'
  Set-Lang (Join-Path $dir 'CONFIG.ini') 'english'
  Write-Host '两个 ini + 注册表全部设为 english'

  # 3) 全新状态启动
  $p = Start-Process -FilePath (Join-Path $dir 'h1_sp64_ship.exe') `
    -WorkingDirectory $dir -ArgumentList '+set r_fullscreen 0' -PassThru
  Start-Sleep -Seconds 7
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds 5
  try { [Windows.Forms.SendKeys]::SendWait('{ENTER}') } catch { }
  Start-Sleep -Seconds ([Math]::Max(1, $WaitSec - 12))
  Write-Host ("全新启动，进程存活=" + (-not $p.HasExited))

  $bounds = [Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object Drawing.Bitmap($bounds.Width, $bounds.Height)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
  $g.Dispose()
  $shot = Join-Path $ev 'probe4-clean-english.png'
  $bmp.Save($shot, [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "截图：$shot"
}
finally {
  Get-Process -Name 'h1_sp64_ship' -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 1
  # 4) 完整还原：实验新生成的一律移到留底目录（绝不删除），再把原始存档放回去
  $gen = Join-Path $bak '实验新生成存档（可删）'
  New-Item -ItemType Directory -Force -Path $gen | Out-Null
  if (Test-Path (Join-Path $dir 'players2')) {
    Move-Item (Join-Path $dir 'players2') (Join-Path $gen 'players2')
  }
  if (Test-Path $tmp) { Move-Item $tmp (Join-Path $dir 'players2') }
  if (Test-Path $codx) { Move-Item $codx (Join-Path $gen 'CODEX') }
  if (Test-Path $codxBak) { Move-Item $codxBak $codx }
  Set-ItemProperty $steamKey -Name Language -Value $origReg -Type String
  foreach ($n in 'steam_emu.ini','CONFIG.ini') {
    Copy-Item (Join-Path $bak ($n + '.原版')) (Join-Path $dir $n) -Force
  }
  Write-Host '===== 已完整还原：players2 / CODEX 存档 / 注册表 / 两个 ini ====='
  Write-Host ("players2 文件数=" + (Get-ChildItem (Join-Path $dir 'players2') -File).Count)
  Write-Host ("注册表 Language=" + (Get-ItemProperty $steamKey -Name Language).Language)
}
