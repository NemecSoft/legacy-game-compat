$ErrorActionPreference = 'Stop'
function W($s){ Write-Output $s }
$key = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000'
$regKey = 'HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000'
$backup = 'D:\AI\legacy-game-compat\Games\War3\evidence\display-class-before.reg'

W '=== BACKUP ==='
& reg.exe export $regKey $backup /y | Out-Null
W ('backup=' + $backup + ' exists=' + (Test-Path $backup) + ' size=' + (Get-Item $backup).Length)

W '=== BEFORE ==='
$before = (Get-ItemProperty -Path $key -Name KMD_EnableDisplayableSupport -ErrorAction SilentlyContinue).KMD_EnableDisplayableSupport
W ('KMD_EnableDisplayableSupport = ' + $before)

W '=== SET TO 1 ==='
Set-ItemProperty -Path $key -Name KMD_EnableDisplayableSupport -Value 1 -Type DWord
$after = (Get-ItemProperty -Path $key -Name KMD_EnableDisplayableSupport).KMD_EnableDisplayableSupport
W ('KMD_EnableDisplayableSupport = ' + $after)

W '=== RESTART GPU DEVICE ==='
$id = 'PCI\VEN_1002&DEV_6FDF&SUBSYS_E3531DA2&REV_EF\4&9D5262B&0&0019'
$r = pnputil /restart-device $id 2>&1
$r | ForEach-Object { W $_ }
Start-Sleep -Seconds 8

W '=== DISPLAY STATE ==='
Get-CimInstance Win32_VideoController | ForEach-Object { W ($_.Name + ' ver=' + $_.DriverVersion + ' mode=' + $_.VideoModeDescription + ' hz=' + $_.CurrentRefreshRate + ' status=' + $_.Status) }

W '=== D3D9 RETEST ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | ForEach-Object { W $_ }
