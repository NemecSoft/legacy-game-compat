param([string]$Src='存档-Milwaukee Jon(荘臣)-5级.rrs',[string]$Save='slot5.rrs',[string]$Out='hub-check.png')
$d='D:\YunGame\Race\RoadRash'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class KS2{
 [DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte sc,uint fl,UIntPtr ex);
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
 public static uint FgPid(){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return p;}
 public static void Tap(byte vk){keybd_event(vk,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(90);keybd_event(vk,0,2,UIntPtr.Zero);}
}
"@
Copy-Item (Join-Path $d $Src) (Join-Path $d $Save) -Force
$p=Start-Process -FilePath (Join-Path $d 'Roadrash.exe') -ArgumentList $Save -WorkingDirectory $d -PassThru
$gp=[uint32]$p.Id
Start-Sleep -Seconds 7
[KS2]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 400
if([KS2]::FgPid() -eq $gp){[KS2]::Tap(0x0D); Write-Output 'Enter->game'} else {Write-Output ('跳过按键：前台 pid=' + [KS2]::FgPid())}
Start-Sleep -Seconds 7
$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height)
$g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($b.Location,[System.Drawing.Point]::Empty,$b.Size)
$bmp.Save((Join-Path (Get-Location) $Out),[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose();$bmp.Dispose()
Start-Sleep -Seconds 1
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $d $Save) -Force
Write-Output "shot=$Out"
