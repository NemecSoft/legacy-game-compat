# 秘籍实测 v2：/cheat 用 Start-Process -Wait 拿退出码；发送后留 10s 生效再截图
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

# 1) 启动游戏（零交互）
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiCheat2' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiCheat2'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 12

# 2) 基线截图（KeepOpen：截图工具不杀进程）
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'cheat-before.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "基线: $_" }

# 3) 秘籍通道（Start-Process -Wait 拿退出码；winexe 无控制台输出，结论看退出码+日志）
$proc = Start-Process -FilePath (Join-Path $game 'game_starter.exe') -ArgumentList '/cheat=all' `
         -WorkingDirectory $game -Wait -PassThru
Write-Host ("秘籍通道退出码: 0x{0:X8} ({0})" -f $proc.ExitCode)

# 4) 留足生效时间再截（三连指令约 6-8s 发完，游戏内立即生效）
Start-Sleep -Seconds 10
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'cheat-after.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "发后: $_" }

# 5) 收尾（强杀，干净）
Stop-Process -Id $gp -Force -ErrorAction SilentlyContinue
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName 'ShanghaiCheat2' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 14
