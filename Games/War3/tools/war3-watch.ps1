param([string]$Exe='Warcraft III.exe',[int]$Wait=12)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class EW2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
}
"@
$d='D:\Games\War3'
$before = @(Get-Process | Select-Object -ExpandProperty Id)
$p=Start-Process -FilePath (Join-Path $d $Exe) -WorkingDirectory $d -PassThru
Start-Sleep -Seconds $Wait
$new = @(Get-Process | Where-Object { $before -notcontains $_.Id })
Write-Output ("started pid=" + $p.Id + " alive=" + [bool](Get-Process -Id $p.Id -EA SilentlyContinue))
foreach($n in $new){ $n.Refresh(); Write-Output ("newproc " + $n.ProcessName + ":" + $n.Id + " hwnd=" + $n.MainWindowHandle + " title=" + $n.MainWindowTitle) }
$ids = @($p.Id) + @($new | ForEach-Object { $_.Id })
$found = New-Object System.Collections.ArrayList
$cb = [EW2+EnumProc]{
  param($h,$l)
  $wp=0
  [EW2]::GetWindowThreadProcessId($h,[ref]$wp) | Out-Null
  if($ids -contains [int]$wp){
    $sb=New-Object System.Text.StringBuilder 512; [EW2]::GetWindowTextW($h,$sb,512) | Out-Null
    $cn=New-Object System.Text.StringBuilder 256; [EW2]::GetClassNameW($h,$cn,256) | Out-Null
    [void]$found.Add([pscustomobject]@{H=$h;Pid=$wp;Title=$sb.ToString();Class=$cn.ToString();Vis=[EW2]::IsWindowVisible($h)})
  }
  return $true
}
[EW2]::EnumWindows($cb,[IntPtr]::Zero) | Out-Null
$i=0
foreach($w in $found){
  Write-Output ("win pid={0} vis={1} class={2} title={3}" -f $w.Pid,$w.Vis,$w.Class,$w.Title)
  if($w.Vis){
    $r=New-Object EW2+RECT
    [EW2]::GetWindowRect($w.H,[ref]$r) | Out-Null
    $ww=$r.Right-$r.Left; $hh=$r.Bottom-$r.Top
    if($ww -gt 50 -and $hh -gt 30){
      $i++
      $bmp=New-Object System.Drawing.Bitmap($ww,$hh)
      $g=[System.Drawing.Graphics]::FromImage($bmp)
      $dc=$g.GetHdc(); [EW2]::PrintWindow($w.H,$dc,2) | Out-Null; $g.ReleaseHdc($dc)
      $bmp.Save(("D:\AI\legacy-game-compat\Games\JustCause\evidence\war3-{0}-w{1}.png" -f $w.Pid,$i),[System.Drawing.Imaging.ImageFormat]::Png)
      $g.Dispose();$bmp.Dispose()
      Write-Output ("  saved war3-{0}-w{1}.png {2}x{3}" -f $w.Pid,$i,$ww,$hh)
    }
  }
}
Get-Process War3,'Warcraft III' -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue