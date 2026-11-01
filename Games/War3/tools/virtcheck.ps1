$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== COMPUTER / HYPERVISOR ==='
$cs = Get-CimInstance Win32_ComputerSystem
W ('Manufacturer=' + $cs.Manufacturer + ' Model=' + $cs.Model + ' HypervisorPresent=' + $cs.HypervisorPresent)
W ('TotalPhysicalMemory=' + [math]::Round($cs.TotalPhysicalMemory/1GB,1) + 'GB LogicalProcs=' + $cs.NumberOfLogicalProcessors)
Get-CimInstance Win32_BIOS | ForEach-Object { W ('BIOS ' + $_.Manufacturer + ' ' + $_.SMBIOSBIOSVersion + ' ' + $_.ReleaseDate) }
Get-CimInstance Win32_BaseBoard | ForEach-Object { W ('BOARD ' + $_.Manufacturer + ' ' + $_.Product) }

W '=== HYPER-V REQUIREMENTS (ComputerInfo) ==='
$ci = Get-ComputerInfo
W ('HyperVisorPresent=' + $ci.HyperVisorPresent)
W ('HyperVRequirementVirtualizationFirmwareEnabled=' + $ci.HyperVRequirementVirtualizationFirmwareEnabled)
W ('HyperVRequirementSecondLevelAddressTranslation=' + $ci.HyperVRequirementSecondLevelAddressTranslation)
W ('HyperVRequirementVMMonitorModeExtensions=' + $ci.HyperVRequirementVMMonitorModeExtensions)
W ('DeviceGuardVirtualizationBasedSecurity=' + $ci.DeviceGuardVirtualizationBasedSecurity)
W ('DeviceGuardCodeIntegrityPolicyEnforcementStatus=' + $ci.DeviceGuardCodeIntegrityPolicyEnforcementStatus)

W '=== BCD ==='
(bcdedit /enum '{current}' 2>&1) | Select-String -Pattern 'hypervisorlaunchtype|vsmlaunchtype|description|identifier|isolatedcontext|vsm' | ForEach-Object { W $_.Line }
W '--- all bcd values with hyper/vsm ---'
(bcdedit /enum all 2>&1) | Select-String -Pattern 'hypervisorlaunchtype|vsmlaunchtype|vmBus|hypervisor' | ForEach-Object { W $_.Line }

W '=== OPTIONAL FEATURES ==='
Get-WindowsOptionalFeature -Online | Where-Object { $_.FeatureName -match 'Hyper-V|VirtualMachinePlatform|HypervisorPlatform|Containers|Sandbox|WindowsSandbox|WSL|Linux' } | ForEach-Object { W ($_.FeatureName + ' = ' + $_.State) }

W '=== SERVICES ==='
foreach($s in 'vmcompute','HvHost','vmms','vmickvpexchange','WslService','LxssManager','Docker'){
  $svc = Get-Service $s -ErrorAction SilentlyContinue
  if($svc){ W ($s + ' status=' + $svc.Status + ' start=' + (Get-CimInstance Win32_Service -Filter ("Name='" + $s + "'")).StartMode) }
}

W '=== VBS / DEVICE GUARD REGISTRY ==='
foreach($k in 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard','HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity','HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\CredentialGuard','HKLM:\SYSTEM\CurrentControlSet\Control\Lsa'){
  if(Test-Path $k){ W ('KEY ' + $k); (Get-ItemProperty $k).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' -and $_.Name -match 'Virtualization|Hypervisor|Enable|LsaCfg|Locked' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + $_.Value) } }
}

W '=== INSTALLED VM / EMULATOR SOFTWARE ==='
$paths = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*','HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
Get-ItemProperty $paths | Where-Object { $_.DisplayName -match 'VMware|VirtualBox|BlueStacks|LDPlayer|雷电|MuMu|Nox|夜神|Genymotion|Docker|WSL|Windows Subsystem|Hyper-V|Sandbox|QEMU|UTM|Parallels|Emulator|模拟器' } | ForEach-Object { W ($_.DisplayName + ' | ' + $_.DisplayVersion + ' | ' + $_.InstallDate) }

W '=== GPU-P / PARAVIRTUALIZATION REGISTRY ==='
foreach($k in 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\State','HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers'){
  if(Test-Path $k){ W ('KEY ' + $k); (Get-ItemProperty $k).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) } }
}
W '--- services with 'vGPU'/'Gpu' names ---'
Get-Service | Where-Object { $_.Name -match 'vgpu|gpu|hvd' } | ForEach-Object { W ($_.Name + ' ' + $_.Status) }

W '=== DISPLAY ADAPTER DEVICE PROPERTIES (DEVPKEY) ==='
Get-PnpDevice -Class Display | ForEach-Object {
  $id = $_.InstanceId
  W ('DEVICE ' + $_.FriendlyName + ' ' + $id + ' status=' + $_.Status + ' problem=' + $_.Problem)
  foreach($p in 'DEVPKEY_Device_DriverVersion','DEVPKEY_Device_LocationPaths','DEVPKEY_Device_Parent','DEVPKEY_Device_ProblemCode'){
    $v = (Get-PnpDeviceProperty -InstanceId $id -KeyName $p -ErrorAction SilentlyContinue).Data
    if($v){ W ('    ' + $p + ' = ' + ($v -join ';')) }
  }
}
