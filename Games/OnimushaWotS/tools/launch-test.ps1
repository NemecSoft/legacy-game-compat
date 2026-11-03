param(
  [string]$GameDir = 'D:\YunGame\F\OnimushaWotS',
  [string]$Exe = 'OnimushaWotS.exe',
  [int]$WaitSec = 45,
  [string]$Shot = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$exePath = Join-Path $GameDir $Exe
$p = Start-Process -FilePath $exePath -WorkingDirectory $GameDir -PassThru
Write-Output ('PID=' + $p.Id)
Start-Sleep -Seconds $WaitSec

$proc = Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($proc) {
  Write-Output ('HWND=0x{0:X}' -f $proc.MainWindowHandle)
  Write-Output ('TITLE=' + $proc.MainWindowTitle)
  Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WinRect {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
}
'@
  $r = New-Object WinRect+RECT
  [WinRect]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
  Write-Output ('WINDOW=' + ($r.Right - $r.Left) + 'x' + ($r.Bottom - $r.Top))
  $c = New-Object WinRect+RECT
  [WinRect]::GetClientRect($proc.MainWindowHandle, [ref]$c) | Out-Null
  Write-Output ('CLIENT=' + ($c.Right - $c.Left) + 'x' + ($c.Bottom - $c.Top))
} else {
  Write-Output 'NO_WINDOW'
}

if ($Shot) {
  $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
  $bmp.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output ('SHOT=' + $Shot)
}

$cfg = Join-Path $GameDir 'config.ini'
Write-Output ('CONFIG=' + (Test-Path $cfg))
if (Test-Path $cfg) {
  Get-Item $cfg | Select-Object Length, LastWriteTime | Format-List
}
