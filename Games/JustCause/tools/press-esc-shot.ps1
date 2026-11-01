param([string]$Tag='menu')
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class K {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p=Get-Process JustCause -EA SilentlyContinue | Select-Object -First 1
if(-not $p){Write-Output 'game not running'; exit}
$hwnd=$p.MainWindowHandle
$fg=[K]::GetForegroundWindow(); $fpid=0
[K]::GetWindowThreadProcessId($fg,[ref]$fpid) | Out-Null
Write-Output ("pid={0} hwnd={1} fgpid={2}" -f $p.Id,$hwnd,$fpid)
if($fpid -ne $p.Id){Write-Output 'game is NOT foreground; refusing to send keys'; exit}
[K]::keybd_event(0x1B,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[K]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
Start-Sleep -Seconds 2
$r=New-Object K+RECT
[K]::GetWindowRect($hwnd,[ref]$r) | Out-Null
$w=$r.Right-$r.Left; $h=$r.Bottom-$r.Top
$bmp=New-Object System.Drawing.Bitmap($w,$h)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$hdc=$g.GetHdc(); [K]::PrintWindow($hwnd,$hdc,2) | Out-Null; $g.ReleaseHdc($hdc)
$f="D:\AI\legacy-game-compat\Games\JustCause\evidence\$Tag.png"
$bmp.Save($f,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
Write-Output "saved $f"
