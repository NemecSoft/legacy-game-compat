# 实验备用档 + conf 输出 1024×768：量游戏窗口尺寸，判断 conf 是否被 dgVoodoo 读到
$game = 'D:\YunGame\IndieGame\shanghai'
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') `
          -Argument '--go --mode=2' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiExp' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiExp'
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
public class Win {
    public delegate bool EnumCb(IntPtr h, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCb cb, IntPtr lp);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
'@
$gp2 = $gp
$cb = [Win+EnumCb]{ param($h, $lp)
    $pid2 = 0; [Win]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $gp2 -and [Win]::IsWindowVisible($h)) {
        $r = New-Object Win+RECT
        [Win]::GetWindowRect($h, [ref]$r) | Out-Null
        Write-Host ("窗口 {0}x{1} @ ({2},{3})" -f ($r.R-$r.L), ($r.B-$r.T), $r.L, $r.T)
    }
    return $true
}
[Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if ($gp) { Stop-Process -Id $gp -Force }
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Unregister-ScheduledTask -TaskName 'ShanghaiExp' -Confirm:$false
Write-Host '实验结束'
