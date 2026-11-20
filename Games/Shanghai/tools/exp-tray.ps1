# 托盘版实测：--go 启动 → 游戏运行中枚举 game_starter 可见窗口（应 0）→ 强杀 → 桌面还原
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'

$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiTray' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiTray'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"
Start-Sleep -Seconds 10

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W5 {
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    public delegate bool EnumProc(IntPtr h, IntPtr lp);
}
'@
$launcherPid = (Get-Process game_starter -ErrorAction SilentlyContinue).Id
$vis = 0
if ($launcherPid) {
    [W5]::EnumWindows({ param($h, $lp)
        $pid2 = 0; [W5]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $launcherPid -and [W5]::IsWindowVisible($h)) { $script:vis++ }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
Write-Host ("游戏运行中启动器可见窗口数: $vis（应 0=已藏托盘）")

& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out 'D:\AI\legacy-game-compat\Games\Shanghai\evidence\verify-tray-mode.png' -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "截图: $_" }

Stop-Process -Id $gp -Force -ErrorAction SilentlyContinue
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("启动器退出后桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName 'ShanghaiTray' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 8
