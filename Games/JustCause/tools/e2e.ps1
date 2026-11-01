param([string]$GameDir='D:\YunGame\Z\JustCause1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class E {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$fail=0
function Check([string]$n,[bool]$ok,[string]$d=''){
  if($ok){Write-Output ("PASS  $n  $d")}else{$script:fail++;Write-Output ("FAIL  $n  $d")}
}
$p=Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds 12
$p.Refresh()
$game=Get-Process JustCause -EA SilentlyContinue | Select-Object -First 1
Check '启动器 /go 后游戏在跑' ([bool]$game) ("pid=" + $(if($game){$game.Id}else{0}))
if($game){
  $wmi=Get-CimInstance Win32_Process -Filter ("ProcessId=" + $game.Id)
  Check '游戏父进程 = game_starter.exe' ($wmi.ParentProcessId -eq $p.Id) ("parent=" + $wmi.ParentProcessId + " launcher=" + $p.Id)
  $h=$game.MainWindowHandle
  if($h -ne 0){
    $r=New-Object E+RECT
    [E]::GetWindowRect($h,[ref]$r) | Out-Null
    $w=$r.Right-$r.Left; $hh=$r.Bottom-$r.Top
    if($w -gt 100){
      $bmp=New-Object System.Drawing.Bitmap($w,$hh)
      $g=[System.Drawing.Graphics]::FromImage($bmp)
      $hdc=$g.GetHdc(); [E]::PrintWindow($h,$hdc,2) | Out-Null; $g.ReleaseHdc($hdc)
      $bmp.Save('D:\AI\legacy-game-compat\Games\JustCause\evidence\e2e-launch.png',[System.Drawing.Imaging.ImageFormat]::Png)
      $g.Dispose();$bmp.Dispose()
      Write-Output "saved e2e-launch.png"
    }
  }
  $lh=$p.MainWindowHandle
  if($lh -ne 0){ [E]::PostMessage($lh,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null }
  Start-Sleep -Seconds 2
  Check '关启动器窗口后启动器仍陪跑' ([bool](Get-Process -Id $p.Id -EA SilentlyContinue))
  Check '关启动器窗口后游戏仍活着' ([bool](Get-Process -Id $game.Id -EA SilentlyContinue))
  # 强杀游戏，验证启动器跟着退出并把异常码交回
  Stop-Process -Id $game.Id -Force -EA SilentlyContinue
  $p.WaitForExit(30000) | Out-Null
  Check '游戏退出后启动器自动退出' $p.HasExited
  Check '异常退出码已回传（非 0）' ($p.ExitCode -ne 0) ("exit=" + $p.ExitCode + " (0x" + (([int64]$p.ExitCode) -band 0xFFFFFFFF).ToString('X8') + ")")
}
Get-Process JustCause -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Get-Process game_starter -EA SilentlyContinue | Where-Object { $_.Path -like "$GameDir*" } | Stop-Process -Force -EA SilentlyContinue
Write-Output ("FAILURES=" + $fail)
exit $fail
