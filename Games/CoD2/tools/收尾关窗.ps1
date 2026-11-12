# 临时工具：优雅关闭 CoD2 游戏进程（WM_CLOSE 主窗口，15 秒退不掉才强杀并注明）
param([int]$GamePid = 0)
$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;using System.Runtime.InteropServices;
public class GC2 { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(CB cb, IntPtr lp);
public delegate bool CB(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); }
"@
$gp = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
if (-not $gp) { "游戏未在运行"; exit }
$want = if ($GamePid -gt 0) { $GamePid } else { $gp.Id }
$main = [IntPtr]::Zero
$cb = [GC2+CB]{ param($h, $l) $p = 0u; [void][GC2]::GetWindowThreadProcessId($h, [ref]$p); if ($p -eq $want -and [GC2]::IsWindowVisible($h)) { $script:main = $h; return $false }; return $true }
[GC2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($main -ne [IntPtr]::Zero) { [void][GC2]::PostMessage($main, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero); "已发 WM_CLOSE" }
$proc = Get-Process -Id $want -ErrorAction SilentlyContinue
if ($proc) { $proc.WaitForExit(15000) | Out-Null; if (-not $proc.HasExited) { Stop-Process -Id $want -Force; "15 秒没退干净，已强杀" } else { "游戏已退出" } }
