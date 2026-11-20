# 重置实验：options.ini 移走后启动游戏，验证引擎按默认 800×600 重建，量窗口尺寸取证
param([string]$Tag = 'reset-options')
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

# 记录启动前 options.ini 是否存在
Write-Host ("启动前 options.ini 存在: " + (Test-Path (Join-Path $game 'ini\options.ini')))

$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName "ShanghaiReset" -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName "ShanghaiReset"

$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Write-Host ("游戏 pid=$gp")
Start-Sleep -Seconds 10

# 量窗口尺寸（游戏窗口应该正好 800x600）
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W2 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    public delegate bool EnumProc(IntPtr h, IntPtr lp);
}
'@
$script:sz = '没量到'
if ($gp) {
    [W2]::EnumWindows({ param($h, $lp)
        $pid2 = 0; [W2]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $gp -and [W2]::IsWindowVisible($h)) {
            $r = New-Object W2+RECT
            [W2]::GetWindowRect($h, [ref]$r) | Out-Null
            if (($r.R - $r.L) -gt 100) { $script:sz = "$($r.R - $r.L)x$($r.B - $r.T) @ ($($r.L),$($r.T))" }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
Write-Host ("游戏窗口: " + $script:sz)

# 截图取证
if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
       -Out (Join-Path $ev "$Tag.png") -WaitSeconds 6 2>&1 | ForEach-Object { Write-Host "截图: $_" }
    Stop-Process -Id $gp -Force
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
}

# 引擎是否重建了 options.ini
$opt = Join-Path $game 'ini\options.ini'
if (Test-Path $opt) {
    Write-Host ("引擎已重建 options.ini，大小 " + (Get-Item $opt).Length + "，修改时间 " + (Get-Item $opt).LastWriteTime.ToString('HH:mm:ss'))
} else {
    Write-Host "警告：引擎没有重建 options.ini！"
}
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("启动器退出后桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName "ShanghaiReset" -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 6
