# 暂停 Windows 更新 35 天，并禁止 Windows Update 自动装显卡驱动。
# 复原：运行 scripts\恢复Windows更新.bat
$ErrorActionPreference = 'Stop'
function W($s){ Write-Output $s }

$now = [DateTime]::UtcNow
$end = $now.AddDays(35)
$fmt = 'yyyy-MM-ddTHH:mm:ssZ'

$ux = 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
New-Item -Path $ux -Force | Out-Null
New-ItemProperty -Path $ux -Name PauseUpdatesExpiryTime -Value $end.ToString($fmt) -PropertyType String -Force | Out-Null
New-ItemProperty -Path $ux -Name PauseFeatureUpdatesStartTime -Value $now.ToString($fmt) -PropertyType String -Force | Out-Null
New-ItemProperty -Path $ux -Name PauseFeatureUpdatesEndTime -Value $end.ToString($fmt) -PropertyType String -Force | Out-Null
New-ItemProperty -Path $ux -Name PauseQualityUpdatesStartTime -Value $now.ToString($fmt) -PropertyType String -Force | Out-Null
New-ItemProperty -Path $ux -Name PauseQualityUpdatesEndTime -Value $end.ToString($fmt) -PropertyType String -Force | Out-Null

$wuBase = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows'
foreach ($sub in 'WindowsUpdate','WindowsUpdate\AU') {
  $p = Join-Path $wuBase $sub
  if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
}
$au = Join-Path $wuBase 'WindowsUpdate\AU'
New-ItemProperty -Path $au -Name NoAutoUpdate -Value 1 -PropertyType DWord -Force | Out-Null

$wu = Join-Path $wuBase 'WindowsUpdate'
New-ItemProperty -Path $wu -Name ExcludeWUDriversInQualityUpdate -Value 1 -PropertyType DWord -Force | Out-Null

$ds = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching'
New-Item -Path $ds -Force | Out-Null
New-ItemProperty -Path $ds -Name SearchOrderConfig -Value 0 -PropertyType DWord -Force | Out-Null

W '=== WU PAUSE / DRIVER POLICY SET ==='
foreach($k in $ux,$au,$wu,$ds){
  $p = Get-ItemProperty $k
  W ('--- ' + $k)
  $p.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + $_.Value) }
}
W '=== STAGED UPDATE (downloaded, not installed) ==='
Get-ChildItem 'C:\Windows\SoftwareDistribution\Download' -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'b2cbcbe5bb93901c' } | ForEach-Object { W ($_.Name + ' ' + $_.LastWriteTime) }
W '=== CBS reboot pending ==='
W ('RebootPending=' + (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'))
