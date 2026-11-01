Write-Output '=== DxgKrnl-Operational (recent 20) ==='
Get-WinEvent -LogName 'Microsoft-Windows-DxgKrnl-Operational' -MaxEvents 20 -EA SilentlyContinue |
  ForEach-Object { Write-Output ($_.TimeCreated.ToString('MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== DxgKrnl-Admin ==='
Get-WinEvent -LogName 'Microsoft-Windows-DxgKrnl-Admin' -MaxEvents 10 -EA SilentlyContinue |
  ForEach-Object { Write-Output ($_.TimeCreated.ToString('MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== Application log: display/directx errors last 2 days ==='
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddDays(-2)} -EA SilentlyContinue |
  Where-Object { $_.Message -match 'd3d9|Direct3D|DXGI|amdkmd|amdxx|display driver|显示驱动' } |
  Select-Object -First 10 | ForEach-Object { Write-Output ($_.TimeCreated.ToString('MM-dd HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== TDR events (last 7 days) ==='
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).AddDays(-7); Id=4101} -EA SilentlyContinue |
  Select-Object -First 5 | ForEach-Object { Write-Output ($_.TimeCreated.ToString() + ' ' + (($_.Message -split "`r?`n")[0])) }