param([string]$Tag='menu2')
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$d='D:\YunGame\Z\JustCause1'
$out='D:\AI\legacy-game-compat\Games\JustCause\evidence'
$p=Start-Process -FilePath (Join-Path $d 'JustCause.exe') -WorkingDirectory $d -PassThru
$t0=Get-Date
$shot=0
$keySent=$false
while(((Get-Date)-$t0).TotalSeconds -lt 60){
  Start-Sleep -Milliseconds 800
  $proc=Get-Process -Id $p.Id -EA SilentlyContinue
  if(-not $proc){ Write-Output ("exited {0:n0}s 0x{1:X8}" -f ((Get-Date)-$t0).TotalSeconds,$p.ExitCode); break }
  $el=((Get-Date)-$t0).TotalSeconds
  if($el -lt 30){ continue }
  $hwnd=$proc.MainWindowHandle
  if($hwnd -eq 0){ continue }
  $fg=[M]::GetForegroundWindow(); $fpid=0
  [M]::GetWindowThreadProcessId($fg,[ref]$fpid) | Out-Null
  $isFg = ($fpid -eq $p.Id)
  if(-not $keySent -and $isFg -and $el -ge 34){
    [M]::keybd_event(0x20,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [M]::keybd_event(0x20,0,2,[UIntPtr]::Zero)
    $keySent=$true
    Write-Output ("space sent at {0:n0}s" -f $el)
  }
  if($el -ge 33 -and $el -lt 50){
    $r=New-Object M+RECT
    [M]::GetWindowRect($hwnd,[ref]$r) | Out-Null
    $w=$r.Right-$r.Left; $h=$r.Bottom-$r.Top
    if($w -gt 100){
      $shot++
      $bmp=New-Object System.Drawing.Bitmap($w,$h)
      $g=[System.Drawing.Graphics]::FromImage($bmp)
      if($isFg){ $g.CopyFromScreen($r.Left,$r.Top,0,0,(New-Object System.Drawing.Size($w,$h))) }
      else { $hdc=$g.GetHdc(); [M]::PrintWindow($hwnd,$hdc,2) | Out-Null; $g.ReleaseHdc($hdc) }
      $bmp.Save((Join-Path $out ("{0}-{1:d2}.png" -f $Tag,$shot)),[System.Drawing.Imaging.ImageFormat]::Png)
      $g.Dispose();$bmp.Dispose()
      Write-Output ("t={0:n0}s shot {1} fg={2}" -f $el,$shot,$isFg)
      Start-Sleep -Seconds 2
    }
  }
}
