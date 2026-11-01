Write-Output '=== display device driver install info ==='
Get-PnpDevice -Class Display -EA SilentlyContinue | ForEach-Object {
  $id=$_.InstanceId
  Write-Output ("DEV " + $_.FriendlyName + "  status=" + $_.Status + "  id=" + $id)
  foreach($k in 'DEVPKEY_Device_DriverVersion','DEVPKEY_Device_DriverDate','DEVPKEY_Device_DriverProvider','DEVPKEY_Device_InstallDate','DEVPKEY_Device_LastArrivalDate','DEVPKEY_Device_DriverInfPath'){
    $p=Get-PnpDeviceProperty -InstanceId $id -KeyName $k -EA SilentlyContinue
    if($p){ Write-Output ("   " + $k + " = " + $p.Data) }
  }
}
Write-Output '=== AMD / display events today (Application + System) ==='
$today=(Get-Date).Date
foreach($log in 'Application','System'){
  Get-WinEvent -FilterHashtable @{LogName=$log; StartTime=$today} -EA SilentlyContinue |
    Where-Object { $_.ProviderName -match 'amd|AMD|Display|DXGI|DirectX' -or $_.Message -match 'amdkmd|amdxx|显示驱动程序|Direct3D' } |
    Select-Object -First 20 | ForEach-Object { Write-Output ($log + ' ' + $_.TimeCreated.ToString('HH:mm:ss') + ' #' + $_.Id + ' ' + $_.ProviderName + ' | ' + (($_.Message -split "`r?`n")[0])) }
}
Write-Output '=== AMD crash defender / software logs ==='
foreach($p in 'C:\ProgramData\AMD','C:\Program Files\AMD\CNext\CNext\Logs'){
  if(Test-Path $p){ Get-ChildItem $p -Recurse -File -Include *.log,*.txt -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 8 | ForEach-Object { Write-Output ($_.LastWriteTime.ToString('MM-dd HH:mm') + '  ' + $_.Length + '  ' + $_.FullName) } }
}
Write-Output '=== TDR / display driver reset events (last 3 days) ==='
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).AddDays(-3)} -EA SilentlyContinue |
  Where-Object { $_.Id -eq 4101 -or $_.ProviderName -match 'Display' } |
  Select-Object -First 10 | ForEach-Object { Write-Output ($_.TimeCreated.ToString('MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + $_.ProviderName + ' | ' + (($_.Message -split "`r?`n")[0])) }