Write-Output '=== IFEO (映像劫持/debugger) ==='
$ifeo='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'
Get-ChildItem $ifeo -EA SilentlyContinue | Where-Object { $_.PSChildName -match 'war3|warcraft' } | ForEach-Object {
  Write-Output ("KEY " + $_.PSChildName)
  Get-ItemProperty $_.PSPath | Format-List | Out-String | Write-Output
}
if(Test-Path 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'){
  Get-ChildItem 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Image File Execution Options' -EA SilentlyContinue | Where-Object { $_.PSChildName -match 'war3|warcraft' } | ForEach-Object {
    Write-Output ("KEY32 " + $_.PSChildName)
    Get-ItemProperty $_.PSPath | Format-List | Out-String | Write-Output
  }
}
Write-Output '=== AppCompatFlags Layers ==='
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -EA SilentlyContinue | Format-List | Out-String | Write-Output
Write-Output '=== Blizzard Warcraft III registry ==='
Get-ChildItem 'HKCU:\Software\Blizzard Entertainment\Warcraft III' -EA SilentlyContinue | ForEach-Object { Write-Output ("SUB " + $_.PSChildName); Get-ItemProperty $_.PSPath | Format-List | Out-String | Write-Output }
Get-ItemProperty 'HKCU:\Software\Blizzard Entertainment\Warcraft III\Video' -EA SilentlyContinue | Format-List | Out-String | Write-Output
Write-Output '=== Defender detections (last 7 days) ==='
Get-MpThreatDetection -EA SilentlyContinue | Sort-Object InitialDetectionTime -Descending | Select-Object -First 15 InitialDetectionTime,ThreatID,Resources | Format-List | Out-String | Write-Output
Write-Output '=== Defender threats ==='
Get-MpThreat -EA SilentlyContinue | Select-Object -First 10 ThreatName,SeverityID,IsActive,Resources | Format-List | Out-String | Write-Output
Write-Output '=== Recent hotfixes ==='
Get-HotFix -EA SilentlyContinue | Sort-Object InstalledOn -Descending | Select-Object -First 5 HotFixID,InstalledOn | Format-Table -AutoSize | Out-String | Write-Output
Write-Output '=== System graphics DLLs ==='
foreach($f in 'd3d9.dll','d3d8.dll','opengl32.dll','dxgi.dll','vulkan-1.dll','d3d11.dll'){
  foreach($dir in $env:SystemRoot+'\System32', $env:SystemRoot+'\SysWOW64'){
    $p=Join-Path $dir $f
    if(Test-Path $p){ $i=Get-Item $p; Write-Output ("{0}  {1,10}  {2}  ver={3}" -f $i.FullName,$i.Length,$i.LastWriteTime, $i.VersionInfo.FileVersion) }
  }
}
Write-Output '=== War3 folder files (sorted by mtime) ==='
Get-ChildItem 'D:\Games\War3' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 8 Name,Length,LastWriteTime | Format-Table -AutoSize | Out-String | Write-Output