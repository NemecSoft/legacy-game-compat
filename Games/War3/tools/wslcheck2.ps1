$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== WSL / VM PROCESSES ==='
$procs = Get-Process vmmem*,wslhost*,wsl*,vmwp*,vmcompute* -ErrorAction SilentlyContinue
if($procs){ $procs | ForEach-Object { W ($_.ProcessName + ' pid=' + $_.Id + ' start=' + $_.StartTime) } } else { W 'none running' }

W '=== BCD TIMESTAMP ==='
Get-Item 'C:\Boot\BCD' | ForEach-Object { W ('BCD LastWrite=' + $_.LastWriteTime) }
Get-Item 'C:\Boot\BCD.LOG' | ForEach-Object { W ('BCD.LOG LastWrite=' + $_.LastWriteTime) }

W '=== HYPERVISOR EVENTS ==='
$hv = Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-Hyper-V-Hypervisor'} -MaxEvents 10
if($hv){ $hv | ForEach-Object { W ($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss') + ' #' + $_.Id) } } else { W 'none' }

W '=== EMULATIONONLY NOW ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\DirectDraw','HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw'){
  $v = Get-ItemProperty -Path $k -Name EmulationOnly -ErrorAction SilentlyContinue
  if($v){ W ($k + ' = ' + $v.EmulationOnly) } else { W ($k + ' = (absent)') }
}

W '=== D3D9 BEFORE WSL SHUTDOWN ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'CreateDevice|GetDeviceCaps|CheckDeviceType' | ForEach-Object { W $_.Line }

W '=== wsl --shutdown ==='
& wsl.exe --shutdown *> $null
Start-Sleep -Seconds 5
W ('vmmem after shutdown: ' + ((Get-Process vmmem* -ErrorAction SilentlyContinue | Measure-Object).Count))

W '=== D3D9 AFTER WSL SHUTDOWN ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'CreateDevice|GetDeviceCaps|CheckDeviceType' | ForEach-Object { W $_.Line }
