Write-Output '=== graphics DLL timestamps ==='
foreach($f in 'd3d9.dll','d3d8.dll','opengl32.dll','dxgi.dll','vulkan-1.dll','d3d11.dll','d3d12.dll'){
  foreach($dir in @("$env:SystemRoot\System32","$env:SystemRoot\SysWOW64")){
    $p=Join-Path $dir $f
    if(Test-Path $p){ $i=Get-Item $p; Write-Output ("{0}  {1,10}  {2}  ver={3}" -f $i.FullName,$i.Length,$i.LastWriteTime.ToString('yyyy-MM-dd HH:mm'),$i.VersionInfo.FileVersion) }
  }
}
Write-Output '=== WER reports for war3 (last 3 days) ==='
Get-ChildItem 'C:\ProgramData\Microsoft\Windows\WER\ReportArchive','C:\ProgramData\Microsoft\Windows\WER\ReportQueue' -Directory -EA SilentlyContinue |
  Where-Object { $_.Name -match 'War3|Warcraft' } | Sort-Object LastWriteTime -Descending | Select-Object -First 6 |
  ForEach-Object { Write-Output ($_.LastWriteTime.ToString('yyyy-MM-dd HH:mm') + '  ' + $_.Name) }
Write-Output '=== Smart App Control / CI policy ==='
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -EA SilentlyContinue | Format-List | Out-String | Write-Output
Write-Output '=== CodeIntegrity operational (last 30 min) ==='
Get-WinEvent -LogName 'Microsoft-Windows-CodeIntegrity/Operational' -MaxEvents 20 -EA SilentlyContinue |
  Where-Object { $_.TimeCreated -gt (Get-Date).AddMinutes(-30) } | Select-Object -First 8 TimeCreated,Id,Message |
  ForEach-Object { Write-Output ($_.TimeCreated.ToString('HH:mm:ss') + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== AppLocker / SmartScreen events ==='
Get-WinEvent -LogName 'Microsoft-Windows-AppLocker/EXE and DLL' -MaxEvents 10 -EA SilentlyContinue | Select-Object -First 5 TimeCreated,Id,Message |
  ForEach-Object { Write-Output ($_.TimeCreated.ToString() + ' #' + $_.Id + ' ' + (($_.Message -split "`r?`n")[0])) }
Write-Output '=== debuggers available ==='
foreach($p in @('C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe','C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe','D:\Tools\re-toolkit')){ if(Test-Path $p){ Write-Output ('FOUND ' + $p) } }
Write-Output '=== War3.exe manifest ==='
$m = Select-String -Path 'D:\Games\War3\War3.exe' -Pattern 'requestedExecutionLevel|compatibility|supportedOS' -Encoding Byte -AllMatches -EA SilentlyContinue
if($m){ Write-Output 'manifest strings found' } else { Write-Output 'no plain manifest strings' }