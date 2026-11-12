param([switch]$AllowGameLaunch)

# 验证 CoD2 进度修复：拉起游戏让引擎读 config，退出后核对进度是否保持
# 注意：不传任何窗口干预；游戏退出后核对 main\players\Player1\config.cfg
# 没有 -AllowGameLaunch 直接 exit 90（规则 24 第 14 类：用户明令别反复启动游戏）
if (-not $AllowGameLaunch) {
  Write-Output '拒绝：本脚本会真启动游戏一次。确认要跑请加 -AllowGameLaunch'
  exit 90
}
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD2'
$cfg = Join-Path $g 'main\players\Player1\config.cfg'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public static IntPtr FirstWindowOf(uint want) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == want && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
}
"@

# 修复后快照
$before = (Select-String -LiteralPath $cfg -Pattern 'mis_0\d "9"|mis_10 "9"|sv_lastSaveGame').Line -join ' | '
Write-Output "启动前: $before"

$p = Start-Process (Join-Path $g 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 6
$gp = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
if (-not $gp) { Write-Output '游戏进程没起来！'; exit 1 }
Write-Output ("游戏已启动 pid=" + $gp.Id)
Start-Sleep -Seconds 34   # 进主菜单

# 发 WM_CLOSE 走正常退出流程
$h = [W]::FirstWindowOf([uint32]$gp.Id)
if ($h -ne [IntPtr]::Zero) {
    [W]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Write-Output '已发 WM_CLOSE'
} else { Write-Output '没找到游戏窗口，跳过 WM_CLOSE' }

$deadline = (Get-Date).AddSeconds(40)
while ((Get-Process CoD2SP_s -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
$left = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
if ($left) { Stop-Process -Id $left.Id -Force; Write-Output 'WM_CLOSE 无效，已强杀（游戏有安全模式补丁，不弹框）' }
Start-Sleep -Seconds 3
Wait-Process -Id $p.Id -ErrorAction SilentlyContinue

# 核对退出后的 config
$after = (Select-String -LiteralPath $cfg -Pattern 'mis_0\d|mis_10|sv_lastSaveGame').Line -join ' | '
Write-Output "退出后: $after"
Write-Output ("config 时间戳: " + (Get-Item $cfg).LastWriteTime)
$ok = ((Select-String -LiteralPath $cfg -Pattern 'mis_0\d "9"|mis_10 "9"').Count -eq 10)
Write-Output ("判定: " + $(if ($ok) { '通过——引擎接受进度，未归零' } else { '失败——引擎改动了进度，需要深挖' }))
