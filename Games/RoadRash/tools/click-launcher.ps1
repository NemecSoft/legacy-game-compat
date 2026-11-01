param(
  [string]$Proc = 'game_starter',
  [int]$ClientX = 0,
  [int]$ClientY = 0,
  [int]$Key = -1,          # 0=Tab 1=Enter 2=Esc 4=Left 5=Up 6=Right 7=Down
  [switch]$UnTopMost,
  [string]$Shot = 'click-result.png'
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Clicker {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint c, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr e);
  public struct POINT { public int X; public int Y; }
  public static void TopMost(IntPtr h, bool on) {
    IntPtr after = on ? new IntPtr(-1) : new IntPtr(-2);   // HWND_TOPMOST / HWND_NOTOPMOST
    SetWindowPos(h, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOMOVE|NOSIZE|NOACTIVATE
  }
  public static void ClickClient(IntPtr h, int cx, int cy) {
    POINT p = new POINT(); p.X = cx; p.Y = cy;
    ClientToScreen(h, ref p);
    SetCursorPos(p.X, p.Y);
    System.Threading.Thread.Sleep(150);
    mouse_event(2, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(4, 0, 0, 0, UIntPtr.Zero);
  }
  public static void Tap(byte vk) {
    keybd_event(vk, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(90);
    keybd_event(vk, 0, 2, UIntPtr.Zero);
  }
}
"@

$vkMap = @{ 0 = 0x09; 1 = 0x0D; 2 = 0x1B; 4 = 0x25; 5 = 0x26; 6 = 0x27; 7 = 0x28 }
$p = Get-Process $Proc -ErrorAction Stop | Select-Object -First 1
[Clicker]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
[Clicker]::TopMost($p.MainWindowHandle, -not $UnTopMost)
[Clicker]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500

if ($ClientX -ne 0 -or $ClientY -ne 0) { [Clicker]::ClickClient($p.MainWindowHandle, $ClientX, $ClientY) }
if ($Key -ge 0) { [Clicker]::Tap([byte]$vkMap[[int]$Key]) }
Start-Sleep -Milliseconds 700

$r = New-Object System.Drawing.Rectangle
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
$g.Dispose()
$path = Join-Path $PSScriptRoot $Shot
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("clicked=" + $ClientX + "," + $ClientY + " key=" + $Key + " shot=" + $path)
