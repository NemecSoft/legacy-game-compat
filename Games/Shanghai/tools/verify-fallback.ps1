# 备用档（--mode=2：桌面不动）实测。验证：桌面保持 1920×1080、游戏窗口模式跑起来、退出码回传。
$game = 'D:\YunGame\IndieGame\shanghai'
$ev   = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') `
          -Argument '--go --mode=2' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiVerify2' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiVerify2'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 12
& 'D://AI//legacy-game-compat//scripts//capture-window.ps1' -Process 'shanghai' -Out (Join-Path $ev 'verify-fallback-mode.png') -WaitSeconds 6 2>&1 | ForEach-Object { Write-Host "截图: $_" }
if ($gp) { Stop-Process -Id $gp -Force }
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $s = Get-Process game_starter -ErrorAction SilentlyContinue
    if (-not $s) { break }
    Start-Sleep -Seconds 1
}
Start-Sleep -Seconds 2
$b2 = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("启动器退出后桌面: {0}x{1}" -f $b2.Width, $b2.Height)
Unregister-ScheduledTask -TaskName 'ShanghaiVerify2' -Confirm:$false
Write-Host '===== 运行日志尾部 ====='
Get-Content (Join-Path $game '运行日志.txt') -Tail 14
