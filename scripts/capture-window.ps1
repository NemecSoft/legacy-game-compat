param(
  [Parameter(Mandatory = $true)][string]$Process,
  [Parameter(Mandatory = $true)][string]$Out,
  [int]$WaitSeconds = 15,
  [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WindowShot {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  public struct RECT { public int Left, Top, Right, Bottom; }
  public static void TopMost(IntPtr h, bool on) {
    IntPtr after = on ? new IntPtr(-1) : new IntPtr(-2);
    SetWindowPos(h, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
  }
}
"@

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$p = $null
while ((Get-Date) -lt $deadline) {
  $p = Get-Process $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if ($p) { break }
  Start-Sleep -Milliseconds 300
}
if (-not $p) { throw "没有找到窗口进程：$Process" }

$h = $p.MainWindowHandle
[WindowShot]::TopMost($h, $true) | Out-Null
[WindowShot]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 800
$r = New-Object WindowShot+RECT
[WindowShot]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
if ($w -le 0 -or $hh -le 0) { throw "窗口尺寸无效：$w x $hh" }
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$g.Dispose()
$Out = [IO.Path]::GetFullPath($Out)
$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
[WindowShot]::TopMost($h, $false) | Out-Null
if (-not $KeepOpen) { Stop-Process -Id $p.Id -Force }
Write-Output ("saved={0} size={1}x{2} rect={3},{4}" -f $Out, $w, $hh, $r.Left, $r.Top)
