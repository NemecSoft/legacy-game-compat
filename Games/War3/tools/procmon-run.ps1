$pm = 'D:\Temp\procmon\Procmon64.exe'
$pml = 'D:\Temp\procmon\proc.pml'
$csv = 'D:\Temp\procmon\proc.csv'
Remove-Item $pml, $csv -ErrorAction SilentlyContinue
Start-Process -FilePath $pm -ArgumentList '/AcceptEula','/Quiet','/Minimized','/BackingFile',$pml
Start-Sleep -Seconds 5
& 'D:\AI\legacy-game-compat\Games\War3\tools\dxprobe.exe' 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 2
& $pm /Terminate 2>&1 | Out-Null
Start-Sleep -Seconds 4
Write-Output ('pml exists: ' + (Test-Path $pml) + ' size=' + (Get-Item $pml -EA SilentlyContinue).Length)
& $pm /OpenLog $pml '/SaveAs' $csv 2>&1 | Out-Null
Start-Sleep -Seconds 8
Get-Process Procmon64 -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Output ('csv exists: ' + (Test-Path $csv) + ' size=' + (Get-Item $csv -EA SilentlyContinue).Length)