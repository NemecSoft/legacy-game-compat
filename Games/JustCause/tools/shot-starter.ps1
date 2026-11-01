param([string]$Out='D:\AI\legacy-game-compat\Games\JustCause\evidence\starter-ui.png')
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class U {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p=Start-Process -FilePath 'D:\YunGame\Z\JustCause1\game_starter.exe' -WorkingDirectory 'D:\YunGame\Z\JustCause1' -PassThru
for($i=0;$i -lt 20;$i++){
  Start-Sleep -Milliseconds 300
  $p.Refresh()
  if($p.MainWindowHandle -ne 0){ break }
}
$h=$p.MainWindowHandle
Write-Output ("pid={0} hwnd={1}" -f $p.Id,$h)
if($h -eq 0){ exit 1 }
$r=New-Object U+RECT
[U]::GetWindowRect($h,[ref]$r) | Out-Null
$w=$r.Right-$r.Left; $hh=$r.Bottom-$r.Top
$bmp=New-Object System.Drawing.Bitmap($w,$hh)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$hdc=$g.GetHdc(); [U]::PrintWindow($h,$hdc,2) | Out-Null; $g.ReleaseHdc($hdc)
$bmp.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
Write-Output ("saved {0} {1}x{2}" -f $Out,$w,$hh)
[U]::PostMessage($h,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 1
Write-Output ("alive after close=" + [bool](Get-Process -Id $p.Id -EA SilentlyContinue))
