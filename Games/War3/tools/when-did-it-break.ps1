Write-Output '=== Documents\Warcraft III (last run traces) ==='
foreach($p in "$env:USERPROFILE\Documents\Warcraft III","$env:USERPROFILE\Documents\Warcraft III\Replays"){
  if(Test-Path $p){
    Write-Output ('DIR ' + $p)
    Get-ChildItem $p -Force -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 8 Name,Length,LastWriteTime | Format-Table -AutoSize | Out-String | Write-Output
  } else { Write-Output ('(missing) ' + $p) }
}
Write-Output '=== Reliability records (last 5 days) ==='
Get-CimInstance Win32_ReliabilityRecords -EA SilentlyContinue |
  Where-Object { $_.TimeGenerated -ge (Get-Date).AddDays(-5) } |
  Sort-Object TimeGenerated -Descending | Select-Object -First 25 |
  ForEach-Object { Write-Output ($_.TimeGenerated.ToString('MM-dd HH:mm') + ' | ' + $_.SourceName + ' | ' + $_.ProductName + ' | ' + $_.EventIdentifier + ' | ' + $_.Message) }
Write-Output '=== Reliability stability records (install/update) ==='
Get-CimInstance Win32_ReliabilityStabilityMetrics -EA SilentlyContinue | Where-Object { $_.StartMeasurementTime -ge (Get-Date).AddDays(-3) } |
  Sort-Object StartMeasurementTime -Descending | Select-Object -First 5 | ForEach-Object { Write-Output ($_.StartMeasurementTime.ToString() + ' score=' + $_.SystemStabilityIndex) }
Write-Output '=== recent app installs (registry, last 3 days) ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'){
  Get-ItemProperty $k -EA SilentlyContinue | Where-Object { $_.InstallDate -and ([datetime]::ParseExact($_.InstallDate,'yyyyMMdd',$null) -ge (Get-Date).AddDays(-3)) } |
    Select-Object DisplayName,DisplayVersion,InstallDate,Publisher | Format-Table -AutoSize | Out-String | Write-Output
}