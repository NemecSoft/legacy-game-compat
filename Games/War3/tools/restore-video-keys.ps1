$ErrorActionPreference = 'Continue'
Write-Output '--- import backup'
& reg.exe import 'D:\Temp\dx-backup\VideoAMD.reg' 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 2
Write-Output '--- restart device'
& pnputil.exe /restart-device 'PCI\VEN_1002&DEV_6FDF&SUBSYS_E3531DA2&REV_EF\4&9D5262B&0&0019' 2>&1 | ForEach-Object { Write-Output $_ }
Start-Sleep -Seconds 8
Write-Output '--- check'
& 'D:\AI\legacy-game-compat\Games\War3\tools\modecheck.exe' 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Output $_ }
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3dsteps.exe' 2>&1 | Select-Object -First 5 | ForEach-Object { Write-Output $_ }