$etl = 'D:\Temp\d3d9.etl'
$csv = 'D:\Temp\d3d9.csv'
Remove-Item $etl, $csv -ErrorAction SilentlyContinue
$p = '{783ACA0A-790E-4D7F-8451-AA850511C6B9}'
Write-Output '--- start trace'
& logman.exe create trace d3d9trace -p $p 0xffffffffffffffff -o $etl -ets 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 1
Write-Output '--- run probe'
& 'D:\AI\legacy-game-compat\Games\War3\tools\dxprobe.exe' 2>&1 | Select-Object -First 3 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 1
Write-Output '--- stop trace'
& logman.exe stop d3d9trace -ets 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 2
if (Test-Path $etl) {
  Write-Output ('etl size = ' + (Get-Item $etl).Length)
  & tracerpt.exe $etl -o $csv -of CSV -y 2>&1 | Select-Object -First 3 | ForEach-Object { Write-Output $_ }
  if (Test-Path $csv) {
    Write-Output ('csv lines = ' + (Get-Content $csv | Measure-Object -Line).Lines)
    Get-Content $csv -TotalCount 40 | ForEach-Object { Write-Output $_ }
  }
} else {
  Write-Output 'no etl produced'
}