$etl = 'D:\Temp\dxgk.etl'
$csv = 'D:\Temp\dxgk.csv'
Remove-Item $etl, $csv -ErrorAction SilentlyContinue
$p = '{802EC45A-1E99-4B83-9920-87C98277BA9D}'
& logman.exe create trace dxgktrace -p $p 0xffffffffffffffff 5 -o $etl -ets 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 2
& 'D:\AI\legacy-game-compat\Games\War3\tools\dxprobe.exe' 2>&1 | Select-Object -First 1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 2
& logman.exe stop dxgktrace -ets 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 3
if (Test-Path $etl) {
  Write-Output ('etl=' + (Get-Item $etl).Length)
  & tracerpt.exe $etl -o $csv -of CSV -y 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Output $_ }
  if (Test-Path $csv) { Write-Output ('csv=' + (Get-Item $csv).Length) }
}