Write-Output '=== signatures ==='
foreach($p in 'C:\Windows\System32\d3d9.dll','C:\Windows\SysWOW64\d3d9.dll','C:\Windows\System32\d3d11.dll','C:\Windows\SysWOW64\d3d11.dll','C:\Windows\System32\dxgi.dll'){
  $i=Get-Item $p -EA SilentlyContinue
  if($i){
    $s=Get-AuthenticodeSignature $p
    Write-Output ("{0}  {1,10}  {2}  ver={3}  sig={4}  signer={5}" -f $i.FullName,$i.Length,$i.LastWriteTime.ToString('yyyy-MM-dd HH:mm'),$i.VersionInfo.FileVersion,$s.Status,$(try{$s.SignerCertificate.Subject}catch{'-'}))
  }
}
Write-Output '=== AppInit / AppCert DLLs ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows'){
  $v=Get-ItemProperty $k -EA SilentlyContinue
  if($v){ Write-Output ("{0}  AppInit_DLLs=[{1}]  LoadAppInit={2}" -f $k,$v.AppInit_DLLs,$v.LoadAppInit_DLLs) }
}
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name AppCertDlls -EA SilentlyContinue | Format-List | Out-String | Write-Output
Write-Output '=== services (AMD / remote / game) ==='
Get-Service -EA SilentlyContinue | Where-Object { $_.Name -match 'AMD|amdk|GameViewer|Sunlogin|Oray|RTCore|Afterburner|RTSS' } | Select-Object Status,StartType,Name,DisplayName | Format-Table -AutoSize | Out-String | Write-Output
Write-Output '=== files modified today under System32 / SysWOW64 / drivers ==='
$today=(Get-Date).Date
foreach($dir in "$env:SystemRoot\System32","$env:SystemRoot\SysWOW64","$env:SystemRoot\System32\drivers"){
  Get-ChildItem $dir -File -EA SilentlyContinue | Where-Object { $_.LastWriteTime -ge $today } | Sort-Object LastWriteTime |
    Select-Object -First 40 | ForEach-Object { Write-Output ("{0}  {1,10}  {2}" -f $_.LastWriteTime.ToString('HH:mm'),$_.Length,$_.FullName) }
}
Write-Output '=== display class registry (D3D related values) ==='
$cls='HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
Get-ChildItem $cls -EA SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
  $v=Get-ItemProperty $_.PSPath
  Write-Output ("SUB " + $_.PSChildName + "  DriverDesc=" + $v.DriverDesc + "  ProviderName=" + $v.ProviderName)
  foreach($n in 'D3D9','DisableD3D9','OpenGLFlags','OpenGLVersion','UMD','D3D9_UMD','DriverVersion','UserModeDriverName'){
    if($v.PSObject.Properties.Name -contains $n){ Write-Output ("   " + $n + " = " + $v.$n) }
  }
}
Write-Output '=== GraphicsDrivers key ==='
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -EA SilentlyContinue | Format-List | Out-String | Write-Output