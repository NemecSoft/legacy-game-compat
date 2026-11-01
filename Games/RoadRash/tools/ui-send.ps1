param(
  [string]$Key = 'ESC',
  [int]$WaitBefore = 0,
  [switch]$StartGame,
  [string]$Shot = 'ui.png'
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class KeySend {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public static void Tap(byte vk) {
    keybd_event(vk, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(vk, 0, 2, UIntPtr.Zero);
  }
}
"@

function Get-VK([string]$name) {
  switch ($name.ToUpperInvariant()) {
    'ESC' { 0x1B }
    'ENTER' { 0x0D }
    'SPACE' { 0x20 }
    'UP' { 0x26 }
    'DOWN' { 0x28 }
    'LEFT' { 0x25 }
    'RIGHT' { 0x27 }
    'TAB' { 0x09 }
    'BACK' { 0x08 }
    'W' { 0x57 }; 'A' { 0x41 }; 'S' { 0x53 }; 'D' { 0x44 }
    'J' { 0x4A }; 'K' { 0x4B }; 'L' { 0x4C }
    default { [Convert]::ToInt32($name, 16) }
  }
}

if ($StartGame) {
  Get-Process -Name Roadrash -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 800
  Copy-Item 'D:\YunGame\Race\RoadRash\_cnc-ddraw\ddraw.ini' 'D:\YunGame\Race\RoadRash\ddraw.ini' -Force
  Start-Process -FilePath 'D:\YunGame\Race\RoadRash\Roadrash.exe' -WorkingDirectory 'D:\YunGame\Race\RoadRash' | Out-Null
}
if ($WaitBefore -gt 0) { Start-Sleep -Seconds $WaitBefore }

foreach ($k in $Key.Split(',')) {
  $k = $k.Trim()
  if (-not $k) { continue }
  [KeySend]::Tap((Get-VK $k))
  Start-Sleep -Milliseconds 700
}

$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
$g.Dispose()
$path = Join-Path $PSScriptRoot $Shot
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("sent=$Key shot=$path alive=" + [bool](Get-Process Roadrash -ErrorAction SilentlyContinue))
