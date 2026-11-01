$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }
$now = Get-Date
W '=== SESSION ==='
W ('now=' + $now.ToString('yyyy-MM-dd HH:mm:ss'))
W ('agent pid=' + $PID + ' session=' + (Get-Process -Id $PID).SessionId + ' user=' + [Environment]::UserName)
Get-Process explorer | ForEach-Object { W ('explorer pid=' + $_.Id + ' session=' + $_.SessionId) }
W ((query session 2>&1) -join ' | ')

W '=== OS BUILD ==='
$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
W ('ProductName=' + $cv.ProductName + ' Edition=' + $cv.EditionID + ' DisplayVersion=' + $cv.DisplayVersion + ' Build=' + $cv.CurrentBuild + '.' + $cv.UBR + ' Lab=' + $cv.BuildLabEx)
W ('InstallDate=' + ([DateTimeOffset]::FromUnixTimeSeconds($cv.InstallDate)).LocalDateTime)
$os = Get-CimInstance Win32_OperatingSystem
W ('LastBoot=' + $os.LastBootUpTime + ' Version=' + $os.Version)

W '=== PENDING REBOOT ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired','HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending','HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootInProgress'){
  W ($k + ' = ' + (Test-Path $k))
}
$pfro = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager').PendingFileRenameOperations
W ('PendingFileRename=' + ($pfro -ne $null))

W '=== HOTFIX INSTALLED (last 10) ==='
Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 10 | ForEach-Object { W ($_.InstalledOn.ToString('yyyy-MM-dd') + ' ' + $_.HotFixID + ' ' + $_.Description) }

W '=== CBS PACKAGES INSTALLED IN LAST 3 DAYS ==='
$cut = $now.AddDays(-3).ToUniversalTime()
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages' | ForEach-Object {
  $p = Get-ItemProperty $_.PSPath
  if($p.InstallTimeHigh -ne $null -and $p.InstallTimeLow -ne $null){
    $ft = ([long]$p.InstallTimeHigh -shl 32) -bor [long]$p.InstallTimeLow
    if($ft -gt 1){ $t = [DateTime]::FromFileTimeUtc($ft); if($t -gt $cut){ W ($t.ToString('yyyy-MM-dd HH:mm') + ' ' + $_.PSChildName) } }
  }
}

W '=== WINDOWS UPDATE HISTORY (last 15) ==='
try {
  $s = New-Object -ComObject Microsoft.Update.Session
  $se = $s.CreateUpdateSearcher()
  $n = $se.GetTotalHistoryCount()
  if($n -gt 0){ $se.QueryHistory(0,[Math]::Min(15,$n)) | ForEach-Object { W ($_.Date.ToString('yyyy-MM-dd HH:mm') + ' | rc=' + $_.ResultCode + ' | ' + $_.Title) } }
} catch { W ('wu history failed: ' + $_.Exception.Message) }

W '=== WU / PNP EVENTS TODAY ==='
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=$now.Date} | Where-Object { $_.ProviderName -match 'WindowsUpdateClient|Kernel-PnP|Display|amdkmdag' -or $_.Id -in 19,20,43,44,4101,400,410 } | Select-Object -First 40 | ForEach-Object { W ($_.TimeCreated.ToString('HH:mm:ss') + ' #' + $_.Id + ' ' + $_.ProviderName + ' | ' + (($_.Message -split "`r?`n")[0])) }

W '=== SETUPAPI TODAY (driver install/uninstall) ==='
Get-Content "$env:SystemRoot\INF\setupapi.dev.log" -Tail 4000 | Select-String -Pattern '^>>>|^<<<|Device Install|Driver Install|Uninstall' | Where-Object { $_.Line -match '2026|11/01|11-01' } | Select-Object -Last 60 | ForEach-Object { W $_.Line }

W '=== RESTORE POINTS ==='
$rp = Get-ComputerRestorePoint
if($rp){ $rp | Select-Object -Last 10 | ForEach-Object { W ($_.CreationTime + ' seq=' + $_.SequenceNumber + ' ' + $_.Description) } } else { W 'none' }
W ('SR enabled C: ' + ((Get-CimInstance -Namespace root/default -ClassName SystemRestoreConfig).DiskPercent) )
W ('vss shadows: ' + ((vssadmin list shadows 2>&1 | Select-String 'Shadow Copy Volume|creation time') -join ' | '))

W '=== VIDEO CONTROLLERS ==='
Get-CimInstance Win32_VideoController | ForEach-Object { W ($_.Name + ' | ver=' + $_.DriverVersion + ' date=' + $_.DriverDate + ' status=' + $_.Status + ' mode=' + $_.VideoModeDescription + ' hz=' + $_.CurrentRefreshRate + ' inf=' + $_.InfFilename) }

W '=== DISPLAY PNP DEVICES (incl hidden) ==='
Get-PnpDevice -Class Display | ForEach-Object { W ($_.Status + ' | ' + $_.FriendlyName + ' | ' + $_.InstanceId + ' | problem=' + $_.Problem) }
Get-PnpDevice -Class Monitor | ForEach-Object { W ('MON ' + $_.Status + ' | ' + $_.FriendlyName + ' | ' + $_.InstanceId) }

W '=== DISPLAY CLASS REGISTRY ==='
$cls = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
Get-ChildItem $cls | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
  $p = Get-ItemProperty $_.PSPath
  W ('--- ' + $_.PSChildName + ' ' + $p.DriverDesc + ' ver=' + $p.DriverVersion + ' inf=' + $p.InfPath)
  $p.PSObject.Properties | Where-Object { $_.Name -match 'Driver|UMD|D3D|OpenGL|Vulkan|Graphics|Acceleration|UserMode' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) }
}

W '=== CONTROL VIDEO KEYS ==='
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\Video' | ForEach-Object {
  $g = $_.PSChildName
  Get-ChildItem $_.PSPath | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath
    W ('VIDEO ' + $g + '\' + $_.PSChildName + ' desc=' + $p.DriverDesc + ' provider=' + $p.ProviderName + ' version=' + $p.DriverVersion)
    foreach($n in 'Attach.ToDesktop','DefaultSettings.BitsPerPel','DefaultSettings.XResolution','DefaultSettings.YResolution','DefaultSettings.VRefresh','Attach.RelativeX','Attach.RelativeY','Acceleration.Level','D3DVendorName','D3DVendorNameWow'){ if($p.PSObject.Properties.Name -contains $n){ W ('    ' + $n + ' = ' + ($p.$n -join ',')) } }
  }
}

W '=== USER GPU PREFERENCES ==='
Get-ChildItem 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' | ForEach-Object { $p=Get-ItemProperty $_.PSPath; $p.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ($_.Name + ' = ' + $_.Value) } }

W '=== D3D9 / DX STACK FILES ==='
foreach($f in 'C:\Windows\System32\d3d9.dll','C:\Windows\SysWOW64\d3d9.dll','C:\Windows\System32\d3d9on12.dll','C:\Windows\SysWOW64\d3d9on12.dll','C:\Windows\System32\d3d11.dll','C:\Windows\System32\dxgi.dll','C:\Windows\System32\dxgkrnl.sys','C:\Windows\System32\drivers\dxgkrnl.sys'){
  $i = Get-Item $f
  if($i){ $sig = Get-AuthenticodeSignature $f; W ($f + ' | ' + $i.Length + ' | ' + $i.LastWriteTime.ToString('yyyy-MM-dd HH:mm') + ' | ver=' + $i.VersionInfo.FileVersion + ' | sig=' + $sig.Status) } else { W ($f + ' | MISSING') }
}

W '=== AMD UMD / KMD FILES (loaded from driver store) ==='
$amd = Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Directory | Where-Object { $_.Name -match '^u\d+\.inf_amd64' -or $_.Name -match '^ati' } | Sort-Object LastWriteTime -Descending | Select-Object -First 4
foreach($d in $amd){
  W ('DIR ' + $d.Name + ' ' + $d.LastWriteTime)
  Get-ChildItem $d.FullName -File | Where-Object { $_.Name -match '^(atiumd|atiumdag|atidxx|amdkmdag|aticfx|amdihk|amdxc)' } | ForEach-Object { W ('   ' + $_.Name + ' ' + $_.Length + ' ' + $_.LastWriteTime.ToString('yyyy-MM-dd') + ' ver=' + $_.VersionInfo.FileVersion) }
}
foreach($f in 'C:\Windows\System32\drivers\amdkmdag.sys','C:\Windows\System32\atiumd64.dll','C:\Windows\SysWOW64\atiumdag.dll'){
  $i = Get-Item $f
  if($i){ W ($f + ' | ' + $i.Length + ' | ' + $i.LastWriteTime.ToString('yyyy-MM-dd HH:mm') + ' | ver=' + $i.VersionInfo.FileVersion) } else { W ($f + ' | MISSING') }
}

W '=== GRAPHICS DRIVERS STATE ==='
foreach($k in 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers','HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\State'){
  if(Test-Path $k){ W ('KEY ' + $k); (Get-ItemProperty $k).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) } }
}
W ('Configuration child count=' + (Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration').Count)
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration' | ForEach-Object { W ('CFG ' + $_.PSChildName + ' ' + (Get-ItemProperty $_.PSPath).LastUpdateTime) }

W '=== DESKTOP MODE (GDI) ==='
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | ForEach-Object { W ($_.DeviceName + ' primary=' + $_.Primary + ' bounds=' + $_.Bounds + ' depth=' + $_.BitsPerPixel) }
$gp = Get-ItemProperty 'HKCU:\Control Panel\Desktop'
W ('Desktop DPI=' + $gp.LogPixels)

W '=== HDR / ADVANCED COLOR ==='
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration' -Recurse | ForEach-Object {
  $p = Get-ItemProperty $_.PSPath
  foreach($n in $p.PSObject.Properties.Name){ if($n -match 'AdvancedColor|HDR|ColorSpace'){ W ($_.PSPath + ' :: ' + $n + ' = ' + $p.$n) } }
}
