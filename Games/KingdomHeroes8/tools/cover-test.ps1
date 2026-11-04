param(
  [int]$HoldSec = 14,
  [string]$Shot = '',
  [switch]$NoOwner
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class C {
  [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr v);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$p = Get-Process -Name SG8Launcher -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw 'NO SG8Launcher RUNNING' }
$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, [int]$p.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if ($null -eq $win) { throw 'no window' }
# 原版会记住上次窗口位置（可能被挪到屏幕外），先拉回可见位置
$oh = [IntPtr]$win.Current.NativeWindowHandle
[C]::SetWindowPos($oh, [IntPtr](-2), 300, 180, 0, 0, 0x0001 -bor 0x0004) | Out-Null
Start-Sleep -Milliseconds 800
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
$links = @()
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
  if ($e.Current.ControlType.ProgrammaticName -match 'Hyperlink' -and $e.Current.Name.Length -gt 0) {
    $links += $e
    Write-Output ('  链接 ' + $e.Current.Name + ' @ ' + $e.Current.BoundingRectangle)
  }
}
if ($links.Count -eq 0) { throw '没找到左下角链接' }

$x1 = 99999; $y1 = 99999; $x2 = -99999; $y2 = -99999
foreach ($e in $links) {
  $r = $e.Current.BoundingRectangle
  if ($r.X -lt $x1) { $x1 = [int]$r.X }
  if ($r.Y -lt $y1) { $y1 = [int]$r.Y }
  if ($r.X + $r.Width -gt $x2) { $x2 = [int]($r.X + $r.Width) }
  if ($r.Y + $r.Height -gt $y2) { $y2 = [int]($r.Y + $r.Height) }
}
$pad = 6
$x1 -= $pad; $y1 -= $pad; $x2 += $pad; $y2 += $pad
Write-Output ("覆盖区 $x1,$y1 - $x2,$y2  (尺寸 $($x2-$x1)x$($y2-$y1))")

# 用 PrintWindow 抓原版窗口自己的画面取页脚底色（不会被别的窗口污染）
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class P {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$ownerH = [IntPtr]$win.Current.NativeWindowHandle
$prc = New-Object P+RECT
[P]::GetWindowRect($ownerH, [ref]$prc) | Out-Null
$pw = $prc.Right - $prc.Left; $ph = $prc.Bottom - $prc.Top
$own = New-Object System.Drawing.Bitmap $pw, $ph
$og = [System.Drawing.Graphics]::FromImage($own)
$hdc = $og.GetHdc()
[P]::PrintWindow($ownerH, $hdc, 2) | Out-Null
$og.ReleaseHdc($hdc)
$og.Dispose()
$sx = [int]($x2 + 40 - $prc.Left); $sy = [int]((($y1 + $y2) / 2) - $prc.Top)
if ($sx -lt 4 -or $sx -ge ($pw - 4) -or $sy -lt 4 -or $sy -ge ($ph - 4)) { $sx = $pw - 60; $sy = $ph - 30 }
$bg = $own.GetPixel($sx, $sy)
$own.Save(($Shot + '.owner.png'))
$own.Dispose()
Write-Output ("PrintWindow ${pw}x${ph} 取样($sx,$sy) 底色 RGB=$($bg.R),$($bg.G),$($bg.B)")

$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.ShowInTaskbar = $false
$form.StartPosition = 'Manual'
$form.BackColor = [System.Drawing.Color]::FromArgb($bg.R, $bg.G, $bg.B)
$form.TopMost = $false
$form.Bounds = (New-Object System.Drawing.Rectangle $x1, $y1, ($x2 - $x1), ($y2 - $y1))
$form.Show()
if (-not $NoOwner) {
  $lh = [IntPtr]$win.Current.NativeWindowHandle
  [C]::SetWindowLongPtr($form.Handle, -8, $lh) | Out-Null   # GWLP_HWNDPARENT
  Write-Output ('已设为原版窗口的从属窗口（owner=' + $lh + '）')
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class F {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
}
'@
$owner = [IntPtr]$win.Current.NativeWindowHandle
[F]::ShowWindow($owner, 9) | Out-Null
[F]::SetForegroundWindow($owner) | Out-Null
[F]::SetForegroundWindow($form.Handle) | Out-Null
Start-Sleep -Milliseconds 600

# 客观校验：链接文字所在像素现在是不是已经变成底色
$b3 = New-Object System.Drawing.Bitmap 4, 4
$g3 = [System.Drawing.Graphics]::FromImage($b3)
foreach ($e in $links) {
  $r = $e.Current.BoundingRectangle
  $g3.CopyFromScreen([int]($r.X + 6), [int]($r.Y + $r.Height / 2), 0, 0, (New-Object System.Drawing.Size 4, 4))
  $px = $b3.GetPixel(2, 2)
  $same = ($px.R -eq $bg.R -and $px.G -eq $bg.G -and $px.B -eq $bg.B)
  Write-Output ('  复查 ' + $e.Current.Name + ' 处像素 = ' + $px.R + ',' + $px.G + ',' + $px.B + ' 与底色一致=' + $same)
}
$g3.Dispose(); $b3.Dispose()
Write-Output "遮罩已贴上，保持 ${HoldSec}s（期间移动/最大化原版窗口试试会不会跟住）"
Start-Sleep -Seconds $HoldSec
if ($Shot -ne '') {
  $rc = New-Object C+RECT
  [C]::GetWindowRect($oh, [ref]$rc) | Out-Null
  $w = $rc.Right - $rc.Left; $h = $rc.Bottom - $rc.Top
  $b2 = New-Object System.Drawing.Bitmap $w, $h
  $g2 = [System.Drawing.Graphics]::FromImage($b2)
  $g2.CopyFromScreen($rc.Left, $rc.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
  $g2.Dispose(); $b2.Save($Shot); $b2.Dispose()
  Write-Output ("SHOT $Shot (${w}x${h})")
}
Start-Sleep -Seconds 3
$form.Close()
Write-Output '遮罩已关闭'
