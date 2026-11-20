# 最终版完整链路验证：/readings + 默认档 --go（切桌面铺满、强杀、还原）
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'
Write-Host '===== /readings 自检 ====='
& (Join-Path $game 'game_starter.exe') /readings
Write-Host ("readings exit=" + $LASTEXITCODE)

Write-Host '===== 最终 exe 默认档完整链路 ====='
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiFinal' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiFinal'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 8
Add-Type -AssemblyName System.Windows.Forms
& 'D://AI//legacy-game-compat//scripts//capture-window.ps1' -Process 'shanghai' -Out (Join-Path $ev 'verify-final-default.png') -WaitSeconds 6 2>&1 | ForEach-Object { Write-Host "截图: $_" }
Stop-Process -Id $gp -Force
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $s = Get-Process game_starter -ErrorAction SilentlyContinue
    if (-not $s) { break }
    Start-Sleep -Seconds 1
}
Start-Sleep -Seconds 2
$b2 = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("启动器退出后桌面: {0}x{1}（应还原 1920x1080）" -f $b2.Width, $b2.Height)
Unregister-ScheduledTask -TaskName 'ShanghaiFinal' -Confirm:$false
Write-Host '===== 运行日志尾部 ====='
Get-Content (Join-Path $game '运行日志.txt') -Tail 16
