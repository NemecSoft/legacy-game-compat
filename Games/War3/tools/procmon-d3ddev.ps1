$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }
$pm = 'D:\Temp\procmon\Procmon64.exe'
$pml = 'D:\Temp\procmon\d3d9.pml'
$csv = 'D:\Temp\procmon\d3d9.csv'
W ('procmon exists=' + (Test-Path $pm))
Remove-Item $pml,$csv -ErrorAction SilentlyContinue

W '=== start trace ==='
Start-Process -FilePath $pm -ArgumentList '/AcceptEula','/Quiet','/Minimized','/BackingFile',$pml,'/Runtime','9'
Start-Sleep -Seconds 3
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' *> $null
Start-Sleep -Seconds 9
Get-Process Procmon64 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

W ('pml exists=' + (Test-Path $pml) + ' size=' + (Get-Item $pml -ErrorAction SilentlyContinue).Length)
W '=== export csv ==='
Start-Process -FilePath $pm -ArgumentList '/OpenLog',$pml,'/SaveAs',$csv -Wait
Start-Sleep -Seconds 5
Get-Process Procmon64 -ErrorAction SilentlyContinue | Stop-Process -Force
W ('csv exists=' + (Test-Path $csv) + ' size=' + (Get-Item $csv -ErrorAction SilentlyContinue).Length)

W '=== interesting operations for d3ddev.exe ==='
$lines = Select-String -Path $csv -Pattern 'd3ddev\.exe' -ErrorAction SilentlyContinue |
  Where-Object { $_.Line -match 'd3d9|DirectDraw|Direct3D|\\Video\\|atiumd|amdihk|d3d9on12|GraphicsDrivers' } |
  ForEach-Object { $_.Line }
W ('matched lines=' + ($lines | Measure-Object).Count)
$lines | Select-Object -First 150 | ForEach-Object { W $_ }

W '=== NAME NOT FOUND / ACCESS DENIED for d3ddev.exe ==='
$bad = Select-String -Path $csv -Pattern 'd3ddev\.exe' -ErrorAction SilentlyContinue |
  Where-Object { $_.Line -match 'NAME NOT FOUND|ACCESS DENIED|PATH NOT FOUND' } |
  ForEach-Object { $_.Line }
W ('bad lines=' + ($bad | Measure-Object).Count)
$bad | Select-Object -First 80 | ForEach-Object { W $_ }
