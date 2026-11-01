$keys = @(
  'HKLM:\SOFTWARE\Microsoft\Direct3D',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D',
  'HKLM:\SOFTWARE\Microsoft\DirectDraw',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw',
  'HKCU:\Software\Microsoft\Direct3D',
  'HKCU:\Software\Microsoft\DirectDraw'
)
foreach ($k in $keys) {
  if (Test-Path $k) {
    $v = Get-ItemProperty $k -EA SilentlyContinue
    $props = $v.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' }
    if ($props) {
      Write-Output ("KEY " + $k)
      foreach ($p in $props) { Write-Output ("   " + $p.Name + " = " + $p.Value) }
    } else {
      Write-Output ("KEY " + $k + "  (no values)")
    }
  } else {
    Write-Output ("(none) " + $k)
  }
}
Write-Output '=== reg search SoftwareOnly / EmulationOnly (SOFTWARE hive) ==='
cmd /c "reg query HKLM\SOFTWARE /s /f SoftwareOnly 2>nul" | Select-Object -First 10 | ForEach-Object { Write-Output $_ }
cmd /c "reg query HKLM\SOFTWARE /s /f EmulationOnly 2>nul" | Select-Object -First 10 | ForEach-Object { Write-Output $_ }
Write-Output '=== Acceleration.Level search (SYSTEM Control) ==='
cmd /c "reg query HKLM\SYSTEM\CurrentControlSet\Control /s /f Acceleration.Level 2>nul" | Select-Object -First 10 | ForEach-Object { Write-Output $_ }
Write-Output '=== 已启用的 Windows 旧版功能 ==='
Get-WindowsOptionalFeature -Online -EA SilentlyContinue | Where-Object { $_.FeatureName -match 'DirectPlay|Legacy|DirectX' } | Select-Object FeatureName,State | Format-Table -AutoSize | Out-String | Write-Output