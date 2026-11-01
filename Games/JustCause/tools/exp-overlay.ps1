param([int]$Shots=12,[int]$Interval=5,[string]$Tag='ov')
$d='D:\YunGame\Z\JustCause1'
$out='D:\AI\legacy-game-compat\Games\JustCause\evidence'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$p=Start-Process -FilePath (Join-Path $d 'JustCause.exe') -WorkingDirectory $d -PassThru
for($i=1;$i -le $Shots;$i++){
  Start-Sleep -Seconds $Interval
  $alive=$(Get-Process -Id $p.Id -EA SilentlyContinue)
  $fg=''
  try{
    Add-Type @"
using System;using System.Runtime.InteropServices;
public class W{ [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid); }
"@ -EA SilentlyContinue
    $h=[W]::GetForegroundWindow(); $fpid=0; [W]::GetWindowThreadProcessId($h,[ref]$fpid)|Out-Null; $fg="fgpid=$fpid"
  }catch{}
  if($alive){
    $b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height)
    $g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($b.Location,[System.Drawing.Point]::Empty,$b.Size)
    $f=Join-Path $out ("{0}-{1:d2}.png" -f $Tag,$i)
    $bmp.Save($f,[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
    Write-Output ("t={0}s alive=yes {1} shot={2}" -f ($i*$Interval),$fg,$f)
  } else {
    $code=$p.ExitCode
    Write-Output ("t={0}s alive=no exitcode=0x{1:X8}" -f ($i*$Interval),$code)
    break
  }
}
if(Get-Process -Id $p.Id -EA SilentlyContinue){Write-Output 'still running; leaving it'}
