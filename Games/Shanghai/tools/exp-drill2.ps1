# 灾备演练 v2：
# A) --go 原生启动 → 游戏加载的 d3d8 模块应为系统目录（非游戏目录）
# B) 杀主启动器（保留恢复进程）→ 杀游戏 → 恢复进程应把桌面还原回 1920×1080 并自杀
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiDr2' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiDr2'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 12

# A: 模块路径
try {
    (Get-Process -Id $gp -Module -ErrorAction Stop) | Where-Object { $_.ModuleName -match '^d3d8' } |
        ForEach-Object { Write-Host ("游戏加载: " + $_.ModuleName + " <- " + $_.FileName) }
} catch { Write-Host ("模块枚举失败（不影响）: " + $_.Exception.Message) }

& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'verify-native-nolayer.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "截图: $_" }

# 区分主启动器（父进程不是 game_starter）与恢复进程（父是 game_starter）
$gsIds = (Get-Process game_starter -ErrorAction SilentlyContinue).Id
$mains = @(); $restorers = @()
Get-CimInstance Win32_Process -Filter "Name='game_starter.exe'" | ForEach-Object {
    if ($gsIds -contains $_.ParentProcessId) { $restorers += $_.ProcessId }
    else { $mains += $_.ProcessId }
}
Write-Host ("主启动器: " + ($mains -join ',') + "  恢复进程: " + ($restorers -join ','))

# B: 杀主启动器（保留恢复进程）→ 杀游戏
foreach ($id in $mains) { Stop-Process -Id $id -Force; Write-Host "强杀主启动器 pid=$id" }
Start-Sleep -Seconds 1
Stop-Process -Id $gp -Force; Write-Host "强杀游戏 pid=$gp"
Write-Host "等恢复进程还原（最多 30s）..."
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Seconds 2
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
} while (((Get-Date) -lt $deadline) -and ($b.Width -eq 800))
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("最终桌面: $($b.Width)x$($b.Height)（应 1920x1080）")
Start-Sleep -Seconds 5
Write-Host ("残留 game_starter: " + (Get-Process game_starter -ErrorAction SilentlyContinue).Count + "（恢复进程应已自杀=0）")
Unregister-ScheduledTask -TaskName 'ShanghaiDr2' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 8
