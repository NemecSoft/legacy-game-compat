$d='D:\Games\War3'
foreach($exe in @('War3.exe','Warcraft III.exe')){
  $p=Start-Process -FilePath (Join-Path $d $exe) -WorkingDirectory $d -PassThru
  $ok=$p.WaitForExit(15000)
  if($ok){ Write-Output ($exe + ' -> exit 0x' + ((([int64]$p.ExitCode) -band 0xFFFFFFFF).ToString('X8'))) }
  else { Write-Output ($exe + ' -> still running'); Stop-Process -Id $p.Id -Force }
  Get-Process War3,'Warcraft III' -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
}
Write-Output '--- event log ---'
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-20)} -EA SilentlyContinue |
  Where-Object { $_.Id -in 1000,1001,1002 } | Select-Object -First 4 | ForEach-Object {
    $msg = $_.Message
    $first = ($msg -split "`r?`n")[0..3] -join ' / '
    Write-Output ($_.TimeCreated.ToString() + ' | ' + $_.Id + ' | ' + $first)
  }
Write-Output '--- crashdumps ---'
Get-ChildItem "$env:LOCALAPPDATA\CrashDumps" -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 5 Name,Length,LastWriteTime | Format-Table -AutoSize