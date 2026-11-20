# 闭环验证：启动游戏 → WM_CLOSE 正常退出 → 确认引擎写回 800×600 默认的 options.ini → 再启动确认不回退
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

function Start-Game {
    $action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
    Register-ScheduledTask -TaskName 'ShanghaiClose' -Action $action -Force | Out-Null
    Start-ScheduledTask -TaskName 'ShanghaiClose'
    $deadline = (Get-Date).AddSeconds(40); $gp = 0
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 800
        $p = Get-Process shanghai -ErrorAction SilentlyContinue
        if ($p) { $gp = $p.Id; break }
    }
    Unregister-ScheduledTask -TaskName 'ShanghaiClose' -Confirm:$false
    return $gp
}
function Wait-GameExit($gp) {
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process shanghai -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W3 {
    [DllImport("user32.dll")] public static extern IntPtr PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    public delegate bool EnumProc(IntPtr h, IntPtr lp);
}
'@

# 第一发：启动 → 正常退出（WM_CLOSE）
$gp = Start-Game
Write-Host "第一发 pid=$gp"
Start-Sleep -Seconds 12
$mainH = [IntPtr]::Zero
[W3]::EnumWindows({ param($h, $lp)
    $pid2 = 0; [W3]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $gp -and [W3]::IsWindowVisible($h)) { $script:mainH = $h; return $false }
    return $true
}, [IntPtr]::Zero) | Out-Null
Write-Host ("主窗口句柄: " + $mainH)
[W3]::PostMessageW($mainH, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null   # WM_CLOSE
Write-Host "已发 WM_CLOSE，等游戏自己退出"
Wait-GameExit $gp

$opt = Join-Path $game 'ini\options.ini'
if (Test-Path $opt) {
    Write-Host ("options.ini 已重建：大小 " + (Get-Item $opt).Length + " 字节")
    Copy-Item $opt 'D:\AI\legacy-game-compat\Games\Shanghai\evidence\options-after-close.bin'
} else { Write-Host "警告：正常退出也没重建 options.ini" }

# 第二发：确认不回退（窗口应仍 800x600）
$gp2 = Start-Game
Write-Host "第二发 pid=$gp2"
Start-Sleep -Seconds 12
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W4 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    public delegate bool EnumProc(IntPtr h, IntPtr lp);
}
'@
$script:sz = '没量到'
if ($gp2) {
    [W4]::EnumWindows({ param($h, $lp)
        $pid2 = 0; [W4]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $gp2 -and [W4]::IsWindowVisible($h)) {
            $r = New-Object W4+RECT
            [W4]::GetWindowRect($h, [ref]$r) | Out-Null
            if (($r.R - $r.L) -gt 100) { $script:sz = "$($r.R - $r.L)x$($r.B - $r.T)" }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
Write-Host ("第二发窗口: " + $script:sz + "（应 800x600）")
if ($gp2) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
       -Out (Join-Path $ev 'reset-second-launch.png') -WaitSeconds 6 2>&1 | ForEach-Object { Write-Host "截图: $_" }
    Stop-Process -Id $gp2 -Force -ErrorAction SilentlyContinue
    Wait-GameExit $gp2
}
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("桌面: $($b.Width)x$($b.Height)")
