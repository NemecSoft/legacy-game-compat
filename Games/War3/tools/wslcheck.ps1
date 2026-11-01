$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== WSL STATUS / VMs ==='
Get-Process vmmem*,wsl*,wslhost*,vmwp* | Select-Object Id,ProcessName,StartTime | ForEach-Object { W ($_.ProcessName + ' pid=' + $_.Id + ' start=' + $_.StartTime) }
W '--- wsl -l -v ---'
(wsl.exe -l -v 2>&1) | ForEach-Object { W $_ }
W '--- wsl --status ---'
(wsl.exe --status 2>&1) | ForEach-Object { W $_ }
W '--- Hyper-V VM list ---'
(Get-VM 2>&1) | ForEach-Object { W $_ }

W '=== BCD STORE TIMESTAMP ==='
Get-Item 'C:\Boot\BCD' | ForEach-Object { W ('BCD LastWrite=' + $_.LastWriteTime) }
Get-Item 'C:\Boot\BCD.LOG' | ForEach-Object { W ('BCD.LOG LastWrite=' + $_.LastWriteTime) }

W '=== HYPERVISOR EVENTS (oldest/newest) ==='
$hv = Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-Hyper-V-Hypervisor'} -MaxEvents 20
if($hv){ $hv | Select-Object -Last 5 | ForEach-Object { W ($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }; W '--- newest ---'; $hv | Select-Object -First 5 | ForEach-Object { W ($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) } } else { W 'none' }

W '=== QUICK FIX ATTEMPT: remove EmulationOnly value ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\DirectDraw','HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw'){
  $v = Get-ItemProperty -Path $k -Name EmulationOnly -ErrorAction SilentlyContinue
  if($v){ W ($k + ' before=' + $v.EmulationOnly); Remove-ItemProperty -Path $k -Name EmulationOnly -Force; W ($k + ' after=' + (Get-ItemProperty -Path $k -Name EmulationOnly -ErrorAction SilentlyContinue).EmulationOnly) }
}

W '=== RETEST AFTER REMOVAL ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | ForEach-Object { W $_ }
