# cmd 启动方式验证：只观察、不杀进程，游戏跑起来留给用户看
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $proc.Id)
Start-Sleep -Seconds 15
$game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($game) {
  Write-Output ("t=15s 游戏存活 pid=" + $game.Id + " 主窗=" + $game.MainWindowHandle + " 响应=" + $game.Responding)
  $gpu = (Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples |
    Where-Object { $_.InstanceName -match 'iw5sp' -and $_.CookedValue -gt 0 } |
    Measure-Object CookedValue -Sum
  Write-Output ("GPU利用率=" + [Math]::Round($gpu.Sum, 1) + "%")
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\cmdstart-t15s.png" -WaitSeconds 3 2>$null
} else {
  Write-Output 't=15s 游戏未运行'
}
Write-Output '===== 日志关键行 ====='
Get-Content "$g\运行日志.txt" -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -Last 8
Write-Output 'done（游戏和启动器都不杀，留在屏幕上）'
