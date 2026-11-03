param(
  [string]$GameDir = 'D:\YunGame\F\OnimushaWotS',
  [int]$WaitSec = 12
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class GameCloseWin {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
'@

$game = Get-Process -Name 'OnimushaWotS' -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $game) { Write-Output 'NO_GAME_WINDOW'; exit 2 }
$starter = Get-Process -Name 'game_starter' -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Output ('GAME_PID=' + $game.Id + ' STARTER_PID=' + $(if ($starter) { $starter.Id } else { '?' }))
[GameCloseWin]::SendMessage($game.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds $WaitSec
$game.Refresh()
Write-Output ('GAME_ALIVE=' + (-not $game.HasExited))
if ($starter) {
  $exited = $starter.WaitForExit(10000)
  Write-Output ('STARTER_EXITED=' + $exited + $(if ($exited) { ' CODE=' + $starter.ExitCode } else { '' }))
}
Get-Content (Join-Path $GameDir '运行日志.txt') -Tail 3
