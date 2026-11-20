# 灾备版实测：
# A) --go 原生启动 → 游戏加载的 d3d8 模块路径应为 C:\Windows\...\d3d8.dll（非游戏目录）
# B) 杀启动器（模拟玩家强杀）→ 再杀游戏 → 独立恢复进程应把桌面还原回 1920×1080
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

# --- A: 启动 ---
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiDr' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiDr'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 12

# 游戏加载的 d3d8 模块路径（原生 or 层）
try {
    $mods = (Get-Process -Id $gp -Module -ErrorAction Stop) | Where-Object { $_.ModuleName -match '^d3d8' }
    foreach ($m in $mods) { Write-Host ("游戏加载: " + $m.ModuleName + " <- " + $m.FileName) }
} catch { Write-Host ("模块枚举失败（不影响）: " + $_.Exception.Message) }

& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'verify-native-nolayer.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "截图: $_" }

# 恢复进程应已存在（启动器拉的）
$rp = Get-Process game_starter | Where-Object { $_.Id -ne (Get-Process game_starter -ErrorAction SilentlyContinue | Select-Object -First 1).Id }
Write-Host ("game_starter 进程数: " + (Get-Process game_starter -ErrorAction SilentlyContinue).Count + "（主+恢复=2 预期）")

# --- B: 模拟玩家强杀启动器 → 杀游戏 → 恢复进程兜底 ---
Get-Process game_starter -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ("强杀启动器 pid=" + $_.Id); Stop-Process -Id $_.Id -Force }
Start-Sleep -Seconds 2
Write-Host "强杀游戏"
Stop-Process -Id $gp -Force
Write-Host "等恢复进程还原（最多 25s）..."
$deadline = (Get-Date).AddSeconds(25)
do {
    Start-Sleep -Seconds 2
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
} while (((Get-Date) -lt $deadline) -and ($b.Width -eq 800))
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("最终桌面: $($b.Width)x$($b.Height)（应 1920x1080）")
Write-Host ("残留 game_starter: " + (Get-Process game_starter -ErrorAction SilentlyContinue).Count)
Unregister-ScheduledTask -TaskName 'ShanghaiDr' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 10
