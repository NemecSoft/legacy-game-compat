param([switch]$AllowGameLaunch)

# CoD2 双问题实测：① 启动器零干预后游戏窗口是否自动进前台 ② 启动全程有无弹框（#32770 对话框）
# 没有 -AllowGameLaunch 直接 exit 90（规则 24 第 14 类）
if (-not $AllowGameLaunch) {
  Write-Output '拒绝：本脚本会真启动游戏一次。确认要跑请加 -AllowGameLaunch'
  exit 90
}
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD2'

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class W2 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);

    public static string Dialogs() {
        var sb = new StringBuilder(300);
        var found = new System.Collections.Generic.List<string>();
        EnumWindows((h, l) => {
            if (IsWindowVisible(h)) {
                var cn = new StringBuilder(128); GetClassName(h, cn, 128);
                if (cn.ToString() == "#32770") {
                    var tt = new StringBuilder(256); GetWindowText(h, tt, 256);
                    uint p; GetWindowThreadProcessId(h, out p);
                    found.Add("对话框(pid=" + p + ") 标题=[" + tt + "]");
                }
            }
            return true;
        }, IntPtr.Zero);
        return found.Count > 0 ? string.Join(" | ", found) : "";
    }
    public static string FgTitle() {
        IntPtr h = GetForegroundWindow();
        var tt = new StringBuilder(256); GetWindowText(h, tt, 256);
        uint p; GetWindowThreadProcessId(h, out p);
        var cn = new StringBuilder(128); GetClassName(h, cn, 128);
        return "pid=" + p + " 类=" + cn + " 标题=[" + tt + "]";
    }
    public static IntPtr FirstWindowOf(uint want) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == want && IsWindowVisible(h)) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
}
"@

$starter = Start-Process (Join-Path $g 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 6
$gp = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
if (-not $gp) { Write-Output '游戏进程没起来！'; exit 1 }
$gamePid = [uint32]$gp.Id
Write-Output ("游戏已启动 pid=" + $gamePid)

for ($i = 1; $i -le 4; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
  if (-not $gp) { break }
  $dlg = [W2]::Dialogs()
  $fg = [W2]::FgTitle()
  $gw = [W2]::FirstWindowOf($gamePid)
  $inFront = ($gw -ne [IntPtr]::Zero -and $fg -match "pid=$gamePid\b")
  Write-Output ("t+$($i*10)s 游戏窗=" + ($gw -ne [IntPtr]::Zero) + " 在最前=$inFront | 前台: $fg" + $(if ($dlg) { " ⚠ $dlg" } else { " | 无弹框" }))
}

# 正常退出
$h = [W2]::FirstWindowOf($gamePid)
if ($h -ne [IntPtr]::Zero) { [W2]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Write-Output '已发 WM_CLOSE 正常退出' }
$deadline = (Get-Date).AddSeconds(40)
while ((Get-Process CoD2SP_s -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
$left = Get-Process CoD2SP_s -ErrorAction SilentlyContinue
if ($left) { Stop-Process -Id $left.Id -Force; Write-Output 'WM_CLOSE 无效已强杀（安全模式补丁在位不弹框）' }
Start-Sleep -Seconds 2
$finalDlg = [W2]::Dialogs()
Write-Output ("结束时弹框检查: " + $(if ($finalDlg) { $finalDlg } else { '无弹框' }))
