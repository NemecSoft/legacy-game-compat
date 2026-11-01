$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }
$ev = 'D:\AI\legacy-game-compat\Games\War3\evidence'
New-Item -ItemType Directory -Path $ev -Force | Out-Null

W '=== BACKUP ==='
& reg.exe export 'HKLM\SOFTWARE\Microsoft\Direct3D' "$ev\direct3d64-before.reg" /y | Out-Null
& reg.exe export 'HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D' "$ev\direct3d32-before.reg" /y | Out-Null
W ('backup64=' + (Test-Path "$ev\direct3d64-before.reg") + ' backup32=' + (Test-Path "$ev\direct3d32-before.reg"))

W '=== BEFORE ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Direct3D\Drivers','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers'){
  if(Test-Path $k){
    $p = Get-ItemProperty $k
    W ($k + ' :: ' + (($p.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { $_.Name + '=' + $_.Value }) -join ' | '))
  } else { W ('(no key) ' + $k) }
}

W '=== DELETE SoftwareOnly (both views) ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Direct3D\Drivers','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers'){
  if(Test-Path $k){
    Remove-ItemProperty -Path $k -Name SoftwareOnly -Force -ErrorAction SilentlyContinue
    $v = Get-ItemProperty $k -Name SoftwareOnly -ErrorAction SilentlyContinue
    if($v){ W ($k + ' still SoftwareOnly=' + $v.SoftwareOnly) } else { W ($k + ' SoftwareOnly=(absent)') }
  }
}

W '=== D3D9 x64 REAL WINDOW ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'GetAdapterCount|GetAdapterDisplayMode|GetDeviceCaps|CheckDeviceType|CreateDevice' | ForEach-Object { W $_.Line }

W '=== D3D9 x86 REAL WINDOW ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\D3D9Real32.exe' 2>&1 | ForEach-Object { W $_ }

W '=== D3D9 On12 ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3don12.exe' 2>&1 | Select-String -Pattern 'GetDeviceCaps|CreateDevice' | ForEach-Object { W $_.Line }
