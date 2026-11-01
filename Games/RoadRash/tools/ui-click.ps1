param(
  [int]$X = 0,
  [int]$Y = 0,
  [string]$Keys = '',
  [int]$WaitAfter = 1200,
  [string]$Shot = 'ui-click.png'
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class UiSend2 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static string Activate(IntPtr h) {
    ShowWindow(h, 9);
    SetForegroundWindow(h);
    System.Threading.Thread.Sleep(300);
    RECT r; GetWindowRect(h, out r);
    return r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + " fg=" + (GetForegroundWindow()==h);
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
  public static void Tap(byte vk) {
    keybd_event(vk, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(vk, 0, 2, UIntPtr.Zero);
  }
}
"@

function Get-VK([string]$name) {
  switch ($name.ToUpperInvariant()) {
    'ESC' { 0x1B }; 'ENTER' { 0x0D }; 'SPACE' { 0x20 }
    'UP' { 0x26 }; 'DOWN' { 0x28 }; 'LEFT' { 0x25 }; 'RIGHT' { 0x27 }
    'TAB' { 0x09 }; 'BACK' { 0x08 }
    'W' { 0x57 }; 'A' { 0x41 }; 'S' { 0x53 }; 'D' { 0x44 }
    'J' { 0x4A }; 'K' { 0x4B }; 'L' { 0x4C }
    'Q' { 0x51 }; 'E' { 0x45 }; 'U' { 0x55 }; 'F' { 0x46 }
    default { [Convert]::ToInt32($name, 16) }
  }
}

$p = Get-Process Roadrash -ErrorAction SilentlyContinue | Select-Object -First 1
$info = ''
if ($p -and $p.MainWindowHandle -ne 0) {
  $info = [UiSend2]::Activate($p.MainWindowHandle)
} else {
  $info = 'no-window'
}

if ($X -gt 0 -or $Y -gt 0) {
  [UiSend2]::Click($X, $Y)
  Start-Sleep -Milliseconds 600
}

foreach ($k in $Keys.Split(',')) {
  $k = $k.Trim()
  if (-not $k) { continue }
  [UiSend2]::Tap((Get-VK $k))
  Start-Sleep -Milliseconds 700
}

Start-Sleep -Milliseconds $WaitAfter

$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
$g.Dispose()
$path = Join-Path $PSScriptRoot $Shot
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("win=$info click=$X,$Y keys=$Keys shot=$path alive=" + [bool](Get-Process Roadrash -ErrorAction SilentlyContinue))
