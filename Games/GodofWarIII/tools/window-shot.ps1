param(
  [string]$Shot = '',
  [int]$ClickX = -1,
  [int]$ClickY = -1,
  [int]$WaitMs = 800
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class GdiShot {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
'@
$p = Get-Process -Name game_starter -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw 'NO game_starter RUNNING' }
$hwnd = $p.MainWindowHandle
[GdiShot]::ShowWindow($hwnd, 9) | Out-Null
[GdiShot]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0043) | Out-Null
Start-Sleep -Milliseconds 400
if ($ClickX -ge 0) {
  $r = New-Object GdiShot+RECT
  [GdiShot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  [GdiShot]::SetCursorPos($r.Left + $ClickX, $r.Top + $ClickY) | Out-Null
  Start-Sleep -Milliseconds 220
  [GdiShot]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [GdiShot]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $WaitMs
}
$r2 = New-Object GdiShot+RECT
[GdiShot]::GetWindowRect($hwnd, [ref]$r2) | Out-Null
$w = $r2.Right - $r2.Left; $ht = $r2.Bottom - $r2.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r2.Left, $r2.Top, 0, 0, (New-Object System.Drawing.Size $w, $ht))
$g.Dispose()
$bmp.Save($Shot)
$bmp.Dispose()
[GdiShot]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0043) | Out-Null
"SHOT $Shot (${w}x${ht})"
