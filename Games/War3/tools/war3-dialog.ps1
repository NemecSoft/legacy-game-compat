param([string]$Exe='D:\Games\War3\War3.exe')
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class DLG {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
}
"@
$p=Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
$found=$null
$t0=Get-Date
for($i=0;$i -lt 60 -and -not $found;$i++){
  Start-Sleep -Milliseconds 100
  if(-not (Get-Process -Id $p.Id -EA SilentlyContinue)){ break }
  $cb = [DLG+EnumProc]{
    param($h,$l)
    $wp=0; [DLG]::GetWindowThreadProcessId($h,[ref]$wp) | Out-Null
    if($wp -eq $p.Id){
      $cn=New-Object System.Text.StringBuilder 128; [DLG]::GetClassNameW($h,$cn,128) | Out-Null
      if($cn.ToString() -eq '#32770'){ $script:found=$h; return $false }
    }
    return $true
  }
  [DLG]::EnumWindows($cb,[IntPtr]::Zero) | Out-Null
}
Write-Output ("dialog=" + $found)
if($found){
  $title=New-Object System.Text.StringBuilder 512; [DLG]::GetWindowTextW($found,$title,512) | Out-Null
  Write-Output ("title=[" + $title.ToString() + "]")
  $texts=New-Object System.Collections.ArrayList
  $cb2 = [DLG+EnumProc]{
    param($h,$l)
    $cn=New-Object System.Text.StringBuilder 128; [DLG]::GetClassNameW($h,$cn,128) | Out-Null
    $tx=New-Object System.Text.StringBuilder 1024; [DLG]::GetWindowTextW($h,$tx,1024) | Out-Null
    [void]$script:texts.Add($cn.ToString() + ' : ' + $tx.ToString())
    return $true
  }
  [DLG]::EnumChildWindows($found,$cb2,[IntPtr]::Zero) | Out-Null
  foreach($t in $texts){ Write-Output ("  child " + $t) }
  $r=New-Object DLG+RECT
  [DLG]::GetWindowRect($found,[ref]$r) | Out-Null
  $w=$r.Right-$r.Left; $hh=$r.Bottom-$r.Top
  if($w -gt 20 -and $hh -gt 20){
    $bmp=New-Object System.Drawing.Bitmap($w,$hh)
    $g=[System.Drawing.Graphics]::FromImage($bmp)
    $dc=$g.GetHdc(); [DLG]::PrintWindow($found,$dc,2) | Out-Null; $g.ReleaseHdc($dc)
    $bmp.Save('D:\AI\legacy-game-compat\Games\War3\evidence\war3-dialog.png',[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$bmp.Dispose()
    Write-Output ("saved war3-dialog.png {0}x{1}" -f $w,$hh)
  }
}
Get-Process War3,'Warcraft III' -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue