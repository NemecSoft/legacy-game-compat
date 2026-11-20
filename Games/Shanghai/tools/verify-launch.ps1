# 血战上海滩 启动链路实测（一次性计划任务，脱离会话 job）。
# 验证：--go 零交互 → 默认档切桌面 800×600 → 游戏跑起来 → 强杀游戏 → 桌面自动还原 → 退出码回传。
# 不改游戏文件、不动系统设置；桌面还原由启动器 finally 负责（本脚本前后各读一次模式做对照）。
param()
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev   = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'
$log  = Join-Path $game '运行日志.txt'

function Get-Mode([string]$tag) {
    Add-Type -AssemblyName System.Windows.Forms
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $v = (Get-CimInstance Win32_VideoController | Select-Object -First 1)
    $line = ("[{0}] 桌面 {1}x{2} (VideoController 读数 {3}x{4}@{5})" -f $tag, $b.Width, $b.Height,
             $v.CurrentHorizontalResolution, $v.CurrentVerticalResolution, $v.CurrentRefreshRate)
    Write-Host $line
    Add-Content -Path (Join-Path $ev 'verify-launch.txt') -Value $line
}

Set-Content -Path (Join-Path $ev 'verify-launch.txt') -Value ("==== 实测 " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + " ====")
Get-Mode '启动前'

# 计划任务：直接拉 game_starter.exe --go（零交互默认档）
$name = 'ShanghaiVerify'
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') `
          -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName $name -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName $name

# 等游戏起来
$deadline = (Get-Date).AddSeconds(40)
$gamePid = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gamePid = $p.Id; break }
}
Write-Host ("游戏进程 pid={0}（起没起来见上）" -f $gamePid)

# 桌面应为 800×600（默认档已切）；读一次存证
Get-Mode '游戏运行中'

if ($gamePid) {
    # 等 12 秒让它进画面，截屏存证
    Start-Sleep -Seconds 12
& 'D://AI//legacy-game-compat//scripts//capture-window.ps1' -Process 'shanghai' -Out (Join-Path $ev 'verify-default-mode.png') -WaitSeconds 6 2>&1 | ForEach-Object { Write-Host "截图: $_" }
    Write-Host '已截屏 verify-default-mode.png'

    # 强杀游戏，看启动器是否回传退出码并还原桌面
    Stop-Process -Id $gamePid -Force
    Write-Host '已强杀游戏，等启动器收尾还原…'
}

# 等启动器退出（最多 30s）
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    $s = Get-Process game_starter -ErrorAction SilentlyContinue
    if (-not $s) { break }
    Start-Sleep -Seconds 1
}
$s = Get-Process game_starter -ErrorAction SilentlyContinue
Write-Host ("启动器还在跑: {0}" -f [bool]$s)
Start-Sleep -Seconds 2
Get-Mode '启动器退出后'

Unregister-ScheduledTask -TaskName $name -Confirm:$false
Write-Host '===== 运行日志尾部 ====='
Get-Content -Path $log -Tail 25
