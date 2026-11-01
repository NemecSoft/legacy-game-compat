param([string]$Out='e2e-autoload.png')
$d='D:\YunGame\Race\RoadRash'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class E2{
 [DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte sc,uint fl,UIntPtr ex);
 [DllImport("user32.dll")]public static extern void mouse_event(uint f,uint dx,uint dy,uint d,UIntPtr e);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 public struct RECT{public int L,T,R,B;}
 public static uint FgPid(){uint p;GetWindowThreadProcessId(GetForegroundWindow(),out p);return p;}
 public static void Tap(byte vk){keybd_event(vk,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(90);keybd_event(vk,0,2,UIntPtr.Zero);}
 public static void ClickInto(IntPtr h){RECT r;if(!GetWindowRect(h,out r))return;if(r.R-r.L<50)return;
  SetCursorPos((r.L+r.R)/2,(r.T+r.B)/2);System.Threading.Thread.Sleep(120);
  mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(400);}
}
"@
$lp=Start-Process -FilePath (Join-Path $d 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $d -PassThru
Start-Sleep -Seconds 9
$g=Get-Process Roadrash -ErrorAction SilentlyContinue | Select-Object -First 1
if(-not $g){ Write-Output '游戏没起来'; exit 1 }
$gp=[uint32]$g.Id
for($i=0;$i -lt 4 -and [E2]::FgPid() -ne $gp;$i++){ if($g.MainWindowHandle -ne [IntPtr]::Zero){[E2]::ClickInto($g.MainWindowHandle)}; Start-Sleep -Milliseconds 600 }
if([E2]::FgPid() -eq $gp){[E2]::Tap(0x0D); Write-Output 'Enter->game'} else {Write-Output 'no focus'}
Start-Sleep -Seconds 9
$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height)
$gg=[System.Drawing.Graphics]::FromImage($bmp);$gg.CopyFromScreen($b.Location,[System.Drawing.Point]::Empty,$b.Size)
$bmp.Save((Join-Path (Get-Location) $Out),[System.Drawing.Imaging.ImageFormat]::Png);$gg.Dispose();$bmp.Dispose()
Stop-Process -Name Roadrash,game_starter -Force -ErrorAction SilentlyContinue
Write-Output "shot=$Out"
