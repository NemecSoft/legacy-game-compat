# CoD8 21 秒退出复现：密集截图（6/12/17/20 秒），抓退出前画面
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$log = "$g\运行日志.txt"
if (Test-Path $log) { Remove-Item $log -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $proc.Id)

foreach ($t in @(6, 12, 17, 20)) {
  Start-Sleep -Seconds ($(if ($t -eq 6) { 6 } elseif ($t -eq 12) { 6 } elseif ($t -eq 17) { 5 } else { 3 }))
  $game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($game -and $game.MainWindowHandle -ne 0) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\exit21-t$($t)s.png" -WaitSeconds 2 2>$null
    if ($?) { Write-Output ("t=$t`S 截图完成 pid=" + $game.Id) }
  } elseif ($game) {
    Write-Output ("t=$t`s 进程在但无窗口 pid=" + $game.Id)
  } else {
    Write-Output ("t=$t`s 游戏进程已退出")
    break
  }
}
Start-Sleep -Seconds 6
$g2 = Get-Process iw5sp -ErrorAction SilentlyContinue
if ($g2) {
  Write-Output '>>> 26 秒仍存活（本次没复现 21 秒退出）'
  Get-Process iw5sp, game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
} else {
  Write-Output '<<< 21 秒退出复现'
}
Write-Output 'done'
