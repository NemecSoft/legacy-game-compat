$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== D3D / display acceleration switches (targeted) ==='
foreach($root in 'HKLM:\SYSTEM\CurrentControlSet\Control\Video','HKLM:\SYSTEM\CurrentControlSet\Hardware Profiles\Current\System\CurrentControlSet\Control\VIDEO'){
  if(Test-Path $root){
    Get-ChildItem $root -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
      $p = Get-ItemProperty $_.PSPath
      foreach($n in $p.PSObject.Properties.Name){
        if($n -match 'Acceleration|Emulation|Direct3D|D3D9|SoftwareOnly|HardwareInformation'){ W ($_.PSPath + ' :: ' + $n + ' = ' + ($p.$n -join ',')) }
      }
    }
  }
}

W '=== AppInit_DLLs ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows'){
  if(Test-Path $k){ $p=Get-ItemProperty $k; W ($k + ' AppInit_DLLs=' + $p.AppInit_DLLs + ' LoadAppInit=' + $p.LoadAppInit_DLLs) }
}

W '=== AppCompat layers / custom / IFEO for war3 & d3d9 & probes ==='
foreach($k in 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers','HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'){
  if(Test-Path $k){ W ('KEY ' + $k); (Get-ItemProperty $k).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) } }
}
foreach($base in 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'){
  if(Test-Path $base){
    Get-ChildItem $base | Where-Object { $_.PSChildName -match 'war3|d3d9|d3ddev|d3did|d3don12|dxprobe|d3dsteps|Warcraft' } | ForEach-Object {
      W ('IFEO ' + $_.PSChildName); (Get-ItemProperty $_.PSPath).PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { W ('    ' + $_.Name + ' = ' + ($_.Value -join ',')) }
    }
  }
}

W '=== Display driver install history (setupapi sections with ati2mtag) ==='
$txt = Get-Content "$env:SystemRoot\INF\setupapi.dev.log" -Raw
$parts = [regex]::Split($txt, '(?=>>>  Section start)')
$seen = 0
foreach($p in $parts){
  if($p -match 'ati2mtag_Polaris10'){
    $start = ($p -split "`r?`n" | Select-String -Pattern '^>>>  Section start' | Select-Object -First 1).Line
    $ver = ($p -split "`r?`n" | Select-String -Pattern 'Driver Version - ' | Select-Object -First 1).Line
    $inf = ($p -split "`r?`n" | Select-String -Pattern 'Driver INF\s+- ' | Select-Object -First 1).Line
    W ('SECTION ' + $start + ' | ' + $inf + ' | ' + $ver)
    $seen++
  }
}
W ('sections=' + $seen)

W '=== unique display driver versions in setupapi ==='
$txt -split "`r?`n" | Select-String -Pattern 'Driver Version - (\d\d/\d\d/\d{4}),(\d+\.\d+\.\d+\.\d+)' | ForEach-Object { $_.Matches[0].Groups[2].Value } | Group-Object | Sort-Object Count -Descending | Select-Object -First 20 | ForEach-Object { W ($_.Count.ToString().PadLeft(4) + '  ' + $_.Name) }

W '=== SoftwareDistribution staged packages (AMD/driver/CU) ==='
$sd = 'C:\Windows\SoftwareDistribution\Download'
Get-ChildItem $sd -Directory -ErrorAction SilentlyContinue | ForEach-Object {
  $d = $_
  $hit = Get-ChildItem $d.FullName -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'ati2mtag|u0\d+\.inf|amdkmdag|amd-win|Package_for_RollupFix' } | Select-Object -First 3
  if($hit){ W ('DIR ' + $d.Name + ' ' + $d.LastWriteTime + ' files=' + ($hit.Name -join ',')) }
}

W '=== DriverStore AMD display packages on disk ==='
Get-ChildItem 'C:\Windows\System32\DriverStore\FileRepository' -Directory | Where-Object { $_.Name -match '^u0|^ati' } | Sort-Object LastWriteTime -Descending | ForEach-Object { W ($_.Name + ' ' + $_.LastWriteTime) }

W '=== Windows Update pending installs ==='
$wu = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update'
foreach($k in (Get-ChildItem $wu -ErrorAction SilentlyContinue)){ W ($k.PSChildName + ' -> ' + ((Get-ChildItem $k.PSPath -ErrorAction SilentlyContinue).PSChildName -join ',')) }
