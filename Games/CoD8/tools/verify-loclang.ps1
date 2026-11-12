# loc_language 验证：--go 跑起来，20 秒时截图看菜单文字是否正常
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 14
$game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($game) {
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\loclang-t14s.png" -WaitSeconds 3 2>$null
  Write-Output 't=14s 截图完成'
  Start-Sleep -Seconds 12
  if (Get-Process iw5sp -ErrorAction SilentlyContinue) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\loclang-t26s.png" -WaitSeconds 3 2>$null
    Write-Output 't=26s 仍存活，截图完成（不再手动杀，留给你看）'
  } else {
    Write-Output 't=26s 游戏已退出'
  }
} else {
  Write-Output 't=14s 游戏未起来'
}
Write-Output 'done'
