param(
  [string]$Exe = 'D:\YunGame\Z\JustCause1\game_starter.exe',
  [string]$Out = 'D:\AI\legacy-game-compat\Games\JustCause\evidence\starter-gradient.png'
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class L {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
for ($i=0; $i -lt 40; $i++) {
  Start-Sleep -Milliseconds 250
  $p.Refresh()
  if ($p.MainWindowHandle -ne 0) { break }
}
$h = $p.MainWindowHandle
Write-Output ("pid={0} hwnd={1}" -f $p.Id, $h)
if ($h -eq 0) { throw 'launcher window not found' }
[L]::ShowWindow($h, 9) | Out-Null
[L]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500
$r = New-Object L+RECT
[L]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[L]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("saved {0} {1}x{2}" -f $Out, $w, $hh)
Write-Output "window left open for preview"
