param(
  [string]$GameDir = 'D:\YunGame\F\GodofWarIIIRemastered',
  [int]$BootWaitSec = 55
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class CloseWin {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
'@
$fail = 0
function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) } else { Write-Output ('FAIL ' + $name); $script:fail++ }
}

$p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $GameDir -PassThru
"starter pid=$($p.Id)"
Start-Sleep -Seconds $BootWaitSec

$runner = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'SpartanPort.exe' -and $_.ParentProcessId -eq $p.Id }
$probe = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'spartanport-probe.exe' }
Check ($runner -ne $null) '游戏由启动器直接拉起（SpartanPort.exe --run 是子进程）'
Check ($probe -ne $null) '移植器渲染进程在跑'

# 关窗：只隐藏陪跑，不杀游戏
$hwnd = (Get-Process -Id $p.Id).MainWindowHandle
$null = [CloseWin]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
Start-Sleep -Seconds 4
$still = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
Check ($still -ne $null) '关窗后启动器仍在陪跑'
$probeAlive = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $probe.ProcessId) -ErrorAction SilentlyContinue
Check ($probeAlive -ne $null) '关窗后游戏没被杀'

# 结束游戏：启动器应随之退出并回传退出码
Stop-Process -Id $probe.ProcessId -Force
$exited = $p.WaitForExit(30000)
Check $exited '游戏退出后启动器自动退出'
$code = if ($exited) { $p.ExitCode } else { -1 }
Check ($code -ne 0) ("退出码回传（0x{0:X8}，强杀应为非 0）" -f $code)
$left = Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'SpartanPort|spartanport-probe' }
Check ($left -eq $null) '没有残留的移植器进程'

Write-Output ('FAILURES=' + $fail)
exit $fail
