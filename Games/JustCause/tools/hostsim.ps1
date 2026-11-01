param([string]$GameDir='D:\YunGame\Z\JustCause1')
$ErrorActionPreference='Stop'
Add-Type @"
using System;using System.Runtime.InteropServices;
public class HW {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@
$dir=$GameDir
$ini=Join-Path $dir 'launcher.ini'
$bak=Join-Path $dir 'launcher.ini.hostsim-bak'
Copy-Item $ini $bak -Force
$fail=0
function Check([string]$name,[bool]$ok,[string]$detail=''){
  if($ok){ Write-Output ("PASS  " + $name + "  " + $detail) }
  else { $script:fail++; Write-Output ("FAIL  " + $name + "  " + $detail) }
}
try{
  $fake = "[Launcher]`r`nTitle=HostSim 正当防卫`r`nGameExe=C:\Windows\System32\cmd.exe`r`nGameArgs=/c `"ping -n 9 127.0.0.1 >nul & exit 7`"`r`nGameProcess=cmd`r`nLayerDll=d3d9.dll`r`nLayerIni=dxvk.conf`r`nLayerBackupDir=_dxvk`r`nTextFixAsi=scripts\JCTextFix.asi`r`n"
  [IO.File]::WriteAllText($ini,$fake,(New-Object Text.UTF8Encoding($false)))
  $p=Start-Process -FilePath (Join-Path $dir 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $dir -PassThru
  Start-Sleep -Seconds 3
  $kids=Get-CimInstance Win32_Process -Filter ("ParentProcessId=" + $p.Id) | Where-Object { $_.Name -eq 'cmd.exe' }
  Check '游戏是启动器的直接子进程' ([bool]$kids) ("children=" + (($kids | ForEach-Object { $_.Name + ':' + $_.ProcessId }) -join ','))
  $childPid = if($kids){ @($kids)[0].ProcessId } else { 0 }
  $p.Refresh()
  $h=$p.MainWindowHandle
  if($h -ne 0){ [HW]::PostMessage($h,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null }
  Start-Sleep -Seconds 2
  $alive=[bool](Get-Process -Id $p.Id -EA SilentlyContinue)
  $childAlive= if($childPid){ [bool](Get-Process -Id $childPid -EA SilentlyContinue) } else { $false }
  Check '关启动器窗口后启动器仍活着（陪跑）' $alive
  Check '关启动器窗口后游戏不受影响' $childAlive
  $p.WaitForExit(30000) | Out-Null
  $code=$p.ExitCode
  Check '游戏退出后启动器跟着退出' $p.HasExited
  Check '启动器回传的退出码 == 游戏退出码 7' ($code -eq 7) ("exit=" + $code)
} finally {
  Copy-Item $bak $ini -Force
  Remove-Item $bak -Force -EA SilentlyContinue
  Get-Process game_starter -EA SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $dir 'game_starter.exe') } | Stop-Process -Force -EA SilentlyContinue
}
Write-Output ("FAILURES=" + $fail)
exit $fail
