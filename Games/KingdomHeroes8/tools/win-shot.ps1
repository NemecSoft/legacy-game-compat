param(
  [int]$Pid2 = 0,
  [string]$ProcName = '',
  [string]$Shot = '',
  [int]$ClickX = -1,
  [int]$ClickY = -1,
  [int]$WaitMs = 900,
  [switch]$Restore
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)]
public struct RCT { public int Left, Top, Right, Bottom; }
public class W {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RCT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static System.Collections.Generic.List<string> Titles = new System.Collections.Generic.List<string>();
  public static IntPtr Best = IntPtr.Zero;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (Want != 0 && pid != Want) return true;
    if (!IsWindowVisible(h)) return true;
    StringBuilder sb = new StringBuilder(512);
    GetWindowTextW(h, sb, sb.Capacity);
    string t = sb.ToString();
    if (t.Length == 0) return true;
    RCT r; GetWindowRect(h, out r);
    int w = r.Right - r.Left, ht = r.Bottom - r.Top;
    if (w < 200 || ht < 120) return true;
    Titles.Add(pid + " | " + h + " | " + w + "x" + ht + " | " + t);
    if (Best == IntPtr.Zero) Best = h;
    return true;
  }
}
'@

if ($Pid2 -le 0 -and $ProcName -ne '') {
  $p = Get-Process -Name $ProcName -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { throw "NO process $ProcName" }
  $Pid2 = $p.Id
}
if ($Pid2 -le 0) { throw 'need -Pid2 or -ProcName' }
Write-Output ("TARGET pid=$Pid2")
[W]::Want = [uint32]$Pid2
[W]::EnumWindows([W+EnumProc] { param($h, $l) [W]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
foreach ($t in [W]::Titles) { Write-Output ('  WIN ' + $t) }
if ([W]::Best -eq [IntPtr]::Zero) { throw "no window for pid=$Pid2" }
$hwnd = [W]::Best
[W]::ShowWindow($hwnd, 9) | Out-Null
[W]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0043) | Out-Null
[W]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500
$rc = New-Object RCT
[W]::GetWindowRect($hwnd, [ref]$rc) | Out-Null
Write-Output ("RECT $($rc.Left),$($rc.Top) - $($rc.Right),$($rc.Bottom)  fg=" + [W]::GetForegroundWindow())
if ($ClickX -ge 0) {
  [W]::SetCursorPos($rc.Left + $ClickX, $rc.Top + $ClickY) | Out-Null
  Start-Sleep -Milliseconds 220
  [W]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [W]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
  [W]::GetWindowRect($hwnd, [ref]$rc) | Out-Null
}
$w2 = $rc.Right - $rc.Left; $h2 = $rc.Bottom - $rc.Top
$bmp = New-Object System.Drawing.Bitmap $w2, $h2
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rc.Left, $rc.Top, 0, 0, (New-Object System.Drawing.Size $w2, $h2))
$g.Dispose()
$bmp.Save($Shot)
$bmp.Dispose()
[W]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0043) | Out-Null
Write-Output ("SHOT $Shot (${w2}x${h2})")
