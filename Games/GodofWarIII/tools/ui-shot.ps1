param(
  [int]$X = -1,
  [int]$Y = -1,
  [string]$Shot = '',
  [int]$Pid2 = 0,
  [int]$WaitMs = 900,
  [int]$Wheel = 0,
  [int]$WheelX = 600,
  [int]$WheelY = 520,
  [string]$Key = '',
  [switch]$RightClick,
  [int]$SaveX = -1,
  [int]$SaveY = -1
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class UiShot {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr e);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static IntPtr Best;
  public static uint Want;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid == Want && IsWindowVisible(h)) {
      RECT r; GetWindowRect(h, out r);
      if ((r.Right - r.Left) > 600 && Best == IntPtr.Zero) Best = h;
    }
    return true;
  }
}
'@
if ($Pid2 -le 0) {
  $p = Get-Process -Name SpartanPort -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { throw 'NO SpartanPort RUNNING' }
  $Pid2 = $p.Id
}
$proc = Get-Process -Id $Pid2 -ErrorAction SilentlyContinue
if (-not $proc) { throw "NO_PROCESS pid=$Pid2" }
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) {
  [UiShot]::Want = [uint32]$Pid2
  [UiShot]::EnumWindows([UiShot+EnumProc] { param($h, $l) [UiShot]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
  $hwnd = [UiShot]::Best
}
if ($hwnd -eq [IntPtr]::Zero) { throw "NO_WINDOW pid=$Pid2" }
[UiShot]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0043) | Out-Null
Start-Sleep -Milliseconds 250
$r = New-Object UiShot+RECT
[UiShot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
if ($X -ge 0) {
  [UiShot]::SetCursorPos($r.Left + $X, $r.Top + $Y) | Out-Null
  Start-Sleep -Milliseconds 200
  [UiShot]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [UiShot]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
if ($RightClick) {
  [UiShot]::SetCursorPos($r.Left + $X, $r.Top + $Y) | Out-Null
  Start-Sleep -Milliseconds 200
  [UiShot]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [UiShot]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
if ($Key -ne '') {
  $vk = [int][char]$Key.ToUpper()
  [UiShot]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  [UiShot]::keybd_event([byte]$vk, 0, 2, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
if ($SaveX -ge 0) {
  [UiShot]::SetCursorPos($r.Left + $SaveX, $r.Top + $SaveY) | Out-Null
  Start-Sleep -Milliseconds 200
  [UiShot]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [UiShot]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
if ($Wheel -ne 0) {
  [UiShot]::SetCursorPos($r.Left + $WheelX, $r.Top + $WheelY) | Out-Null
  Start-Sleep -Milliseconds 200
  $d = if ($Wheel -lt 0) { [uint32]([int64]$Wheel + 4294967296) } else { [uint32]$Wheel }
  [UiShot]::mouse_event(0x0800, 0, 0, $d, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $ht))
$g.Dispose()
$bmp.Save($Shot)
$bmp.Dispose()
[UiShot]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0043) | Out-Null
"SHOT $Shot (${w}x${ht}) pid=$Pid2"
