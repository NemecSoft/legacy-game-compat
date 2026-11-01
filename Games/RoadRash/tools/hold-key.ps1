param(
  [string]$Key = 'W',
  [int]$HoldMs = 2000,
  [int]$WaitBefore = 0,
  [string]$Shot = 'hold.png'
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class HoldKey {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

function Get-VK([string]$name) {
  switch ($name.ToUpperInvariant()) {
    'ESC' { 0x1B }; 'ENTER' { 0x0D }; 'SPACE' { 0x20 }
    'UP' { 0x26 }; 'DOWN' { 0x28 }; 'LEFT' { 0x25 }; 'RIGHT' { 0x27 }
    'W' { 0x57 }; 'A' { 0x41 }; 'S' { 0x53 }; 'D' { 0x44 }
    'J' { 0x4A }; 'K' { 0x4B }; 'L' { 0x4C }
    'Q' { 0x51 }; 'E' { 0x45 }; 'U' { 0x55 }; 'F' { 0x46 }
    default { [Convert]::ToInt32($name, 16) }
  }
}

$p = Get-Process Roadrash -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p -and $p.MainWindowHandle -ne 0) { [HoldKey]::SetForegroundWindow($p.MainWindowHandle) | Out-Null }
if ($WaitBefore -gt 0) { Start-Sleep -Seconds $WaitBefore }

$vk = Get-VK $Key
[HoldKey]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds $HoldMs
[HoldKey]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300

$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
$g.Dispose()
$path = Join-Path $PSScriptRoot $Shot
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("held=$Key ${HoldMs}ms shot=$path alive=" + [bool](Get-Process Roadrash -ErrorAction SilentlyContinue))
