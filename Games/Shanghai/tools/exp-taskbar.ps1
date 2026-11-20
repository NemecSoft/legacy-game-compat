# 任务栏版实测：--go → 启动器主窗口应最小化（任务栏可见）、游戏跑、退出后桌面还原
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'

$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiTb' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiTb'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $gp = $p.Id; break }
}
Start-Sleep -Seconds 10

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W6 {
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
'@
$main = Get-Process game_starter -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($main) {
    Write-Host ("启动器主窗口: 可见=" + [W6]::IsWindowVisible($main.MainWindowHandle) +
                " 最小化在任务栏=" + [W6]::IsIconic($main.MainWindowHandle) + "  （应 可见=True 最小化=True）")
} else { Write-Host "启动器主窗口: 没有（异常）" }

& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out 'D:\AI\legacy-game-compat\Games\Shanghai\evidence\verify-taskbar-mode.png' -WaitSeconds 6 -KeepOpen 2>&1 |
   ForEach-Object { Write-Host "截图: $_" }

Stop-Process -Id $gp -Force
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 3
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName 'ShanghaiTb' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 4
