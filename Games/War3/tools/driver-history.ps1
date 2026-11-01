$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== OEM DRIVERS (display related) ==='
$out = pnputil /enum-drivers 2>&1
$cur = @()
foreach($line in $out){
  if($line -match '^发布名称|^Published Name|^原始名称|^Original Name|^提供程序|^Provider|^类名|^Class Name|^驱动程序版本|^Driver Version'){
    $cur += $line
  } elseif($line -match '^\s*$'){
    if($cur.Count -gt 0){
      $t = $cur -join ' | '
      if($t -match 'Display|amd|ati'){ W $t }
      $cur = @()
    }
  }
}
if($cur.Count -gt 0 -and (($cur -join ' ') -match 'Display|amd|ati')){ W ($cur -join ' | ') }

W '=== AMD DRIVER STORE PACKAGES ==='
Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Directory | Where-Object { $_.Name -match '^(u0|ati)' } | Sort-Object LastWriteTime -Descending | Select-Object -First 8 | ForEach-Object {
  W ($_.Name + '  ' + $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))
}

W '=== ACTIVE DISPLAY DRIVER FILE VERSIONS ==='
$root = 'C:\Windows\System32\DriverStore\FileRepository\u0201039.inf_amd64_89c3272f19eab06b'
Get-ChildItem $root -Recurse -File | Where-Object { $_.Name -match '^(atiumd64|atiumdag|atidxx64|atidxx32|amdkmdag|amdhdl64|amdhdl32|aticfx64|aticfx32|amdxc64|amdxc32)\.' } | Sort-Object Name | ForEach-Object {
  W ('{0,-16} {1,10} {2} ver={3}' -f $_.Name,$_.Length,$_.LastWriteTime.ToString('yyyy-MM-dd'),$_.VersionInfo.FileVersion)
}

W '=== REGISTRY UMD PATHS -> FILE CHECK ==='
$p = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000'
foreach($n in 'D3DVendorName','D3DVendorNameWoW','UserModeDriverName','UserModeDriverNameWow','OpenGLDriverName','OpenGLDriverNameWow'){
  $v = $p.$n
  if($v){ foreach($f in ($v -split ',')){ $f=$f.Trim(); if($f){ W ($n + ' -> ' + $f + ' exists=' + (Test-Path $f)) } } }
}

W '=== DIRECTDRAW / DIRECT3D SWITCHES ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\DirectDraw','HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw','HKCU:\SOFTWARE\Microsoft\DirectDraw','HKLM:\SOFTWARE\Microsoft\Direct3D','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D','HKCU:\SOFTWARE\Microsoft\Direct3D'){
  if(Test-Path $k){
    W ('KEY ' + $k)
    (Get-ItemProperty $k).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) }
  } else { W ('(none) ' + $k) }
}

W '=== PROBE ARCHITECTURES ==='
function Arch($p){
  if(-not (Test-Path $p)){ return 'missing' }
  $fs = [IO.File]::OpenRead($p)
  $br = New-Object IO.BinaryReader($fs)
  $fs.Position = 0x3C
  $off = $br.ReadInt32()
  $fs.Position = $off + 4
  $m = $br.ReadUInt16()
  $br.Close(); $fs.Close()
  switch($m){ 0x14c {'x86'} 0x8664 {'x64'} default {'other:' + $m} }
}
Get-ChildItem 'D:\AI\legacy-game-compat\Games\War3\tools\*.exe' | ForEach-Object { W ('{0,-18} {1,8} {2}' -f $_.Name,$_.Length,(Arch $_.FullName)) }
Get-ChildItem 'D:\AI\legacy-game-compat\scripts\*.exe' | ForEach-Object { W ('{0,-18} {1,8} {2}' -f $_.Name,$_.Length,(Arch $_.FullName)) }

W '=== AMD INSTALL LOGS ==='
foreach($d in 'C:\AMD','C:\Program Files\AMD\CIM\Log','C:\Windows\Temp'){
  if(Test-Path $d){
    W ('DIR ' + $d)
    Get-ChildItem $d -File -Recurse -Depth 1 | Where-Object { $_.Extension -match '\.(log|txt)$' -and $_.LastWriteTime -gt (Get-Date).AddDays(-3) } | Sort-Object LastWriteTime -Descending | Select-Object -First 12 | ForEach-Object { W ('   ' + $_.FullName + '  ' + $_.Length + '  ' + $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm')) }
  }
}

W '=== SETUPAPI: DISPLAY DRIVER INSTALLS (last 40 matches) ==='
Get-Content "$env:SystemRoot\INF\setupapi.dev.log" | Select-String -Pattern 'Device Install \(DiShowUpdateDevice\)|dvi:|Driver Version|oem\d+\.inf|u0201039|ati|amd' | Select-Object -Last 80 | ForEach-Object { W $_.Line }
