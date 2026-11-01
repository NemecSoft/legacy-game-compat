param([int]$Seconds=60,[string]$Tag='text',[int]$Every=5,[switch]$ForceForeground)
$d='D:\YunGame\Z\JustCause1'
$out='D:\AI\legacy-game-compat\Games\JustCause\evidence'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class Win {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p=Start-Process -FilePath (Join-Path $d 'JustCause.exe') -WorkingDirectory $d -PassThru
$t0=Get-Date
$shot=0
$next=$Every
$activated=$false
while(((Get-Date)-$t0).TotalSeconds -lt $Seconds){
  Start-Sleep -Seconds 1
  $proc=Get-Process -Id $p.Id -EA SilentlyContinue
  if(-not $proc){
    Write-Output ("exited after {0:n0}s exitcode=0x{1:X8}" -f ((Get-Date)-$t0).TotalSeconds,$p.ExitCode)
    break
  }
  $el=((Get-Date)-$t0).TotalSeconds
  $hwnd=$proc.MainWindowHandle
  if($hwnd -ne 0 -and -not $activated){
    try{ (New-Object -ComObject WScript.Shell).AppActivate($p.Id) | Out-Null }catch{}
    $activated=$true
  }
  if($el -ge $next){
    $next+=$Every
    if($hwnd -ne 0){
      $r=New-Object Win+RECT
      [Win]::GetWindowRect($hwnd,[ref]$r) | Out-Null
      $w=$r.Right-$r.Left; $h=$r.Bottom-$r.Top
      if($w -gt 100 -and $h -gt 100){
        $shot++
        $bmp=New-Object System.Drawing.Bitmap($w,$h)
        $g=[System.Drawing.Graphics]::FromImage($bmp)
        $fg=[Win]::GetForegroundWindow(); $fpid=0
        [Win]::GetWindowThreadProcessId($fg,[ref]$fpid) | Out-Null
        $mode='pw'
        if($fpid -eq $p.Id){
          $g.CopyFromScreen($r.Left,$r.Top,0,0,(New-Object System.Drawing.Size($w,$h)))
          $mode='screen'
        } else {
          $hdc=$g.GetHdc(); [Win]::PrintWindow($hwnd,$hdc,2) | Out-Null; $g.ReleaseHdc($hdc)
        }
        $f=Join-Path $out ("{0}-{1:d2}.png" -f $Tag,$shot)
        $bmp.Save($f,[System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose();$bmp.Dispose()
        Write-Output ("t={0:n0}s shot {1} {2}x{3} mode={4}" -f $el,$shot,$w,$h,$mode)
      }
    } else {
      Write-Output ("t={0:n0}s no window yet" -f $el)
    }
  }
}
if(Get-Process -Id $p.Id -EA SilentlyContinue){Write-Output 'game still running'}
