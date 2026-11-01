param([string]$Exe='D:\Games\War3\War3.exe',[int]$Wait=14,[string]$Tag='war3-dxvk')
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class WC2 {
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
$p=Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
Start-Sleep -Seconds $Wait
$p.Refresh()
$alive=[bool](Get-Process -Id $p.Id -EA SilentlyContinue)
Write-Output ("pid=" + $p.Id + " alive=" + $alive)
$wins=New-Object System.Collections.ArrayList
$cb=[WC2+EnumProc]{
  param($h,$l)
  $wp=0; [WC2]::GetWindowThreadProcessId($h,[ref]$wp) | Out-Null
  if($wp -eq $p.Id -and [WC2]::IsWindowVisible($h)){
    $t=New-Object System.Text.StringBuilder 256; [WC2]::GetWindowTextW($h,$t,256) | Out-Null
    $c=New-Object System.Text.StringBuilder 128; [WC2]::GetClassNameW($h,$c,128) | Out-Null
    $r=New-Object WC2+RECT; [WC2]::GetWindowRect($h,[ref]$r) | Out-Null
    [void]$script:wins.Add([pscustomobject]@{H=$h;T=$t.ToString();C=$c.ToString();W=($r.Right-$r.Left);Ht=($r.Bottom-$r.Top);R=$r})
  }
  return $true
}
$script:wins=$wins
[WC2]::EnumWindows($cb,[IntPtr]::Zero) | Out-Null
foreach($w in $wins){ Write-Output ("win class=" + $w.C + " title=" + $w.T + " " + $w.W + "x" + $w.Ht) }
$big = $wins | Sort-Object { $_.W * $_.Ht } -Descending | Select-Object -First 1
if($big -and $big.W -gt 100){
  $bmp=New-Object System.Drawing.Bitmap($big.W,$big.Ht)
  $g=[System.Drawing.Graphics]::FromImage($bmp)
  $dc=$g.GetHdc(); [WC2]::PrintWindow($big.H,$dc,2) | Out-Null; $g.ReleaseHdc($dc)
  $bmp.Save(("D:\AI\legacy-game-compat\Games\War3\evidence\{0}.png" -f $Tag),[System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose();$bmp.Dispose()
  Write-Output ("saved " + $Tag + ".png " + $big.W + "x" + $big.Ht)
}