param(
  [string]$GameDir = 'D:\YunGame\F\OnimushaWotS',
  [int]$WaitSec = 18
)

$ErrorActionPreference = 'Stop'
$p = Start-Process -FilePath 'C:\Windows\System32\cmd.exe' -ArgumentList @('/c', 'game_starter.bat /go') -WorkingDirectory $GameDir -PassThru
Write-Output ('CMD_PID=' + $p.Id)
Start-Sleep -Seconds $WaitSec
$game = Get-Process -Name 'OnimushaWotS' -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $game) { Write-Output 'NO_GAME'; exit 2 }
$parent = (Get-CimInstance Win32_Process -Filter "ProcessId = $($game.Id)").ParentProcessId
$starter = Get-Process -Id $parent -ErrorAction SilentlyContinue
Write-Output ('GAME_PID=' + $game.Id + ' PARENT_PID=' + $parent + ' PARENT_NAME=' + $(if ($starter) { $starter.ProcessName } else { '?' }))
Stop-Process -Id $game.Id -Force
if ($p.WaitForExit(15000)) {
  Write-Output ('BAT_EXIT=' + $p.ExitCode)
} else {
  Write-Output 'BAT_DID_NOT_EXIT'
}
