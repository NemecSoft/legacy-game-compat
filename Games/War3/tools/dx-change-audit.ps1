Write-Output '=== AMD driver package info ==='
Get-CimInstance Win32_PnPSignedDriver -EA SilentlyContinue | Where-Object { $_.DeviceName -match 'Radeon|AMD' } |
  Select-Object DeviceName,DriverVersion,DriverDate,InstallDate,InfName | Format-List | Out-String | Write-Output
Write-Output '=== display / driver events today (System log) ==='
$today = (Get-Date).Date
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=$today} -EA SilentlyContinue |
  Where-Object { $_.ProviderName -match 'Display|amd|Kernel-PnP|Kernel-Power|WudfUsbccidDriver' -or $_.Id -in 4101,20001,20003,7045,1,2,13,14 } |
  Select-Object -First 25 | ForEach-Object { Write-Output ($_.TimeCreated.ToString('HH:mm:ss') + ' #' + $_.Id + ' ' + $_.ProviderName + ' | ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== setupapi.dev.log tail (driver installs) ==='
Get-Content "$env:SystemRoot\INF\setupapi.dev.log" -Tail 60 -EA SilentlyContinue | Select-String -Pattern '>>>|<<<|Driver|inf' | Select-Object -Last 25 | ForEach-Object { Write-Output $_.Line }
Write-Output '=== pending reboot ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired','HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager'){ if(Test-Path $k){ Write-Output ('exists: ' + $k) } }
Write-Output '=== D3D9 debug / global settings ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Direct3D','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D'){ if(Test-Path $k){ Write-Output ('KEY ' + $k); Get-ItemProperty $k | Format-List | Out-String | Write-Output } else { Write-Output ('no key ' + $k) } }
Write-Output '=== driver store d3d9 UMD files ==='
$uds = Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Directory -EA SilentlyContinue | Where-Object { $_.Name -match 'u0201039|ati' } | Select-Object -First 3
foreach($d in $uds){
  Write-Output ('DIR ' + $d.FullName + '  ' + $d.LastWriteTime)
  foreach($f in 'atiumdag.dll','atiumdva.dll','aticfx32.dll','amdihk32.dll','atidxx32.dll','atidxx64.dll','atiumd64.dll'){
    $p=Join-Path $d.FullName ($f -replace '32','32' )
    Get-ChildItem $d.FullName -Filter $f -EA SilentlyContinue | ForEach-Object { Write-Output ('   ' + $_.Name + '  ' + $_.Length + '  ' + $_.LastWriteTime + '  ver=' + $_.VersionInfo.FileVersion) }
  }
}
Write-Output '=== amdihk32 in SysWOW64 ==='
Get-Item 'C:\Windows\SysWOW64\amdihk32.dll' -EA SilentlyContinue | ForEach-Object { Write-Output ($_.FullName + '  ' + $_.Length + '  ' + $_.LastWriteTime + '  ver=' + $_.VersionInfo.FileVersion) }