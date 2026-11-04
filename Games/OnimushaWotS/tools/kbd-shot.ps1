param(
  [string]$Keys = '9',
  [string]$Shot = ''
)
$keyList = @($Keys.Split(',') | ForEach-Object { [int]$_ })

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class KbdCap {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
'@

$p = Get-Process -Name game_starter -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { throw 'NO_WINDOW' }
$hwnd = $p.MainWindowHandle
$fg = [KbdCap]::GetForegroundWindow()
$fgPid = [uint32]0
$fgThread = [KbdCap]::GetWindowThreadProcessId($fg, [ref]$fgPid)
$myThread = [KbdCap]::GetCurrentThreadId()
[KbdCap]::AttachThreadInput($myThread, $fgThread, $true) | Out-Null
[KbdCap]::ShowWindow($hwnd, 9) | Out-Null
[KbdCap]::BringWindowToTop($hwnd) | Out-Null
[KbdCap]::SetForegroundWindow($hwnd) | Out-Null
[KbdCap]::SetActiveWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 250
[KbdCap]::AttachThreadInput($myThread, $fgThread, $false) | Out-Null
$fg = [KbdCap]::GetForegroundWindow()
$fgPid = [uint32]0
[KbdCap]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
if ($fgPid -ne [uint32]$p.Id) { throw ('FOREGROUND_FAILED foreground=' + $fgPid + ' launcher=' + $p.Id) }
Start-Sleep -Milliseconds 250
foreach ($vk in $keyList) {
  [KbdCap]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 70
  [KbdCap]::keybd_event([byte]$vk, 0, 2, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 380
}
Start-Sleep -Milliseconds 500
$r = New-Object KbdCap+RECT
[KbdCap]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$full = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$g.Dispose()
$x0 = [Math]::Max(0, $r.Left); $y0 = [Math]::Max(0, $r.Top)
$x1 = [Math]::Min($b.Width, $r.Right); $y1 = [Math]::Min($b.Height, $r.Bottom)
$w = $x1 - $x0; $h = $y1 - $y0
$crop = New-Object System.Drawing.Bitmap $w, $h
$gg = [System.Drawing.Graphics]::FromImage($crop)
$src = New-Object System.Drawing.Rectangle $x0, $y0, $w, $h
$dst = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$gg.DrawImage($full, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
$gg.Dispose(); $full.Dispose()
$crop.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
$crop.Dispose()
Write-Output ("SHOT=" + $Shot)
