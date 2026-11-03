param(
  [string]$GameDir = 'D:\YunGame\F\OnimushaWotS',
  [int]$WaitSec = 35
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class CloseWin {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
'@

$starter = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $GameDir -PassThru
Write-Output ('STARTER_PID=' + $starter.Id)
Start-Sleep -Seconds $WaitSec
$starter.Refresh()

$game = Get-Process -Name 'OnimushaWotS' -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $game) { Write-Output 'NO_GAME'; exit 2 }
$parent = (Get-CimInstance Win32_Process -Filter "ProcessId = $($game.Id)").ParentProcessId
Write-Output ('GAME_PID=' + $game.Id + ' PARENT_PID=' + $parent)

[CloseWin]::SendMessage($starter.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 3
$starter.Refresh()
$game.Refresh()
Write-Output ('AFTER_CLOSE_STARTER_ALIVE=' + (-not $starter.HasExited))
Write-Output ('AFTER_CLOSE_GAME_ALIVE=' + (-not $game.HasExited))

if (-not $game.HasExited) {
  Stop-Process -Id $game.Id -Force
  if ($starter.WaitForExit(15000)) {
    Write-Output ('STARTER_EXIT=' + $starter.ExitCode)
  } else {
    Write-Output 'STARTER_DID_NOT_EXIT'
  }
}
