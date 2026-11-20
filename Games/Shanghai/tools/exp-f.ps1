# F 实验：原版最小改动 conf，直接跑游戏 -windows，量窗口
$game = 'D:\YunGame\IndieGame\shanghai'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W3 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
'@
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'shanghai.exe') -Argument '-windows' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiExpF' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiExpF'
$deadline = (Get-Date).AddSeconds(35); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 700
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Start-Sleep -Seconds 10
if ($gp) {
    $p = Get-Process -Id $gp -ErrorAction SilentlyContinue
    if ($p -and $p.MainWindowHandle -ne [IntPtr]::Zero) {
        $r = New-Object W3+RECT
        [W3]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
        Write-Host ("[F 原版+1024x768+appctlFalse] 窗口 {0}x{1} @ ({2},{3})" -f ($r.R-$r.L), ($r.B-$r.T), $r.L, $r.T)
    } else { Write-Host "无主窗口" }
    Stop-Process -Id $gp -Force -ErrorAction SilentlyContinue
}
$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) { $s = Get-Process shanghai -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Milliseconds 800 }
Unregister-ScheduledTask -TaskName 'ShanghaiExpF' -Confirm:$false -ErrorAction SilentlyContinue
Write-Host 'F 实验结束'
