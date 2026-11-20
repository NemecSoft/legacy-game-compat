# 秘籍功能实测：--go 启动游戏 → 另一实例 /cheat=all 发三连秘籍 → 截图看武器栏变化
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

# 1) 启动游戏（零交互）
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiCheat' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiCheat'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 12

# 2) 发秘籍前先截一张（武器栏基线）
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'cheat-before.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "基线截图: $_" }

# 3) 秘籍通道：三连（无敌+全武器+无限弹药）
$out = & (Join-Path $game 'game_starter.exe') '/cheat=all' 2>&1
Write-Host ("秘籍通道退出码: " + $LASTEXITCODE)
$out | ForEach-Object { Write-Host ("  " + $_) }
Start-Sleep -Seconds 3

# 4) 发完截图（haveallweapon 生效的话顶部武器栏会变多把）
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'cheat-after.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "发后截图: $_" }

# 5) 收尾
Stop-Process -Id $gp -Force -ErrorAction SilentlyContinue
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName 'ShanghaiCheat' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 10
