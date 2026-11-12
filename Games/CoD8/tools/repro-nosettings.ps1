# 试验 F：settings_*.zip.iw5 移走对照（引擎对缺失会重建）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$bk = 'D:\AI\legacy-game-compat\Games\CoD8\backup'
New-Item -ItemType Directory -Force -Path $bk | Out-Null

$moved = @()
foreach ($f in @('settings_c.zip.iw5','settings_s.zip.iw5')) {
  $src = Join-Path $game "players2\$f"
  if (Test-Path $src) { Copy-Item $src (Join-Path $bk ($f + '.before-test')) -Force; Remove-Item $src -Force; $moved += $f }
}
Write-Output ('已移走: ' + ($moved -join ', '))

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
$alive = $false
for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $alive = $true
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-nosettings-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(10000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode)

Write-Output '----- 是否重建了 settings 包 -----'
Get-ChildItem "$game\players2\settings_*.iw5" -ErrorAction SilentlyContinue | ForEach-Object { Write-Output ($_.Name + " " + $_.Length + " " + $_.LastWriteTime.ToString('HH:mm:ss')) }

if ($alive) {
  # 游戏活了：别杀，让用户接着用？不——试验环境先收干净：结束进程，文件保持"重建后的新状态"
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) { Stop-Process -Id $gp.Id -Force; Write-Output '试验成功：已结束游戏进程，重建的 settings 包留在原处' }
} else {
  # 没好：把备份放回去
  foreach ($f in $moved) { Copy-Item (Join-Path $bk ($f + '.before-test')) (Join-Path $game "players2\$f") -Force }
  Write-Output '试验无效：settings 包已原样放回'
}
