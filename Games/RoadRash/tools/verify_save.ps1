# 只在「前台窗口确实属于目标进程」时按键，避免把按键打进桌面/别的程序
param([string]$Src='存档-Milwaukee Jon(荘臣)-5级.rrs',[string]$Save='slot5.rrs',[string]$Out='save-verify.png')
$d='D:\YunGame\Race\RoadRash'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class KS{
 [DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte sc,uint fl,UIntPtr ex);
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
 public static uint FgPid(){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return p;}
 public static bool Mine(uint want){return FgPid()==want;}
 public static void Tap(byte vk){keybd_event(vk,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(90);keybd_event(vk,0,2,UIntPtr.Zero);}
}
"@
Copy-Item (Join-Path $d $Src) (Join-Path $d $Save) -Force
$p=Start-Process -FilePath (Join-Path $d 'Roadrash.exe') -ArgumentList $Save -WorkingDirectory $d -PassThru
$gp=[uint32]$p.Id
function TapIf([string]$why){
  if([KS]::Mine($gp)){[KS]::Tap(0x0D);Write-Output ("  Enter -> Roadrash ($why)")}
  else{
    [KS]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 400
    if([KS]::Mine($gp)){[KS]::Tap(0x0D);Write-Output ("  Enter -> Roadrash ($why, 拉回前台后)")}
    else{Write-Output ("  跳过按键 ($why)：前台是 pid=" + [KS]::FgPid())}
  }
}
Start-Sleep -Seconds 6
TapIf 'skip intro'; Start-Sleep -Seconds 5
TapIf 'menu';       Start-Sleep -Seconds 5
TapIf 'confirm';    Start-Sleep -Seconds 6
$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height)
$g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($b.Location,[System.Drawing.Point]::Empty,$b.Size)
$bmp.Save((Join-Path (Get-Location) $Out),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $d $Save) -Force
Write-Output "shot=$Out"
