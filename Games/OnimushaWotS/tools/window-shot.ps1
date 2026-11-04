param(
  [int]$ClickX = -1,
  [int]$ClickY = -1,
  [int]$Click2X = -1,
  [int]$Click2Y = -1,
  [string]$Shot = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class GCap {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@

$p = Get-Process -Name game_starter -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { throw 'NO_WINDOW' }
$r = New-Object GCap+RECT
[GCap]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
[GCap]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 300
if ($ClickX -ge 0) {
  [GCap]::SetCursorPos($r.Left + $ClickX, $r.Top + $ClickY) | Out-Null
  Start-Sleep -Milliseconds 200
  [GCap]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [GCap]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 700
  if ($Click2X -ge 0) {
    [GCap]::SetCursorPos($r.Left + $Click2X, $r.Top + $Click2Y) | Out-Null
    Start-Sleep -Milliseconds 200
    [GCap]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [GCap]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700
  }
}
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$full = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$g.Dispose()
$x0 = [Math]::Max(0, $r.Left); $y0 = [Math]::Max(0, $r.Top)
$x1 = [Math]::Min($b.Width, $r.Right + 40); $y1 = [Math]::Min($b.Height, $r.Bottom + 120)
$w = $x1 - $x0; $h = $y1 - $y0
$crop = New-Object System.Drawing.Bitmap $w, $h
$gg = [System.Drawing.Graphics]::FromImage($crop)
$src = New-Object System.Drawing.Rectangle $x0, $y0, $w, $h
$dst = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$gg.DrawImage($full, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
$gg.Dispose(); $full.Dispose()
$crop.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
$crop.Dispose()
Write-Output ("SHOT=" + $Shot + " " + $w + "x" + $h)
