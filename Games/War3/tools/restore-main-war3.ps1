$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== STOP CLEAN COPY GAME ==='
Get-Process War3 -ErrorAction SilentlyContinue | ForEach-Object { W ('stop ' + $_.Id + ' ' + $_.Path); Stop-Process -Id $_.Id -Force }
Start-Sleep -Seconds 2

W '=== BACKUP APPCOMPAT LAYERS ==='
& reg.exe export 'HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' 'D:\AI\legacy-game-compat\Games\War3\evidence\layers-before.reg' /y | Out-Null
W ('backup=' + (Test-Path 'D:\AI\legacy-game-compat\Games\War3\evidence\layers-before.reg'))

W '=== REMOVE DXVK FROM MAIN GAME ==='
foreach($f in 'D:\Games\War3\d3d9.dll','D:\Games\War3\dxvk.conf','D:\Games\War3\War3_d3d9.log','D:\Games\War3\War3.dxvk-cache'){
  if(Test-Path $f){ W ('remove ' + $f); Remove-Item $f -Force }
}
W ('d3d9.dll exists=' + (Test-Path 'D:\Games\War3\d3d9.dll') + ' _dxvk kept=' + (Test-Path 'D:\Games\War3\_dxvk'))

W '=== REMOVE APPCOMPAT LAYER (main War3 only) ==='
$layers = 'HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$v = Get-ItemProperty -Path $layers -Name 'D:\Games\War3\War3.exe' -ErrorAction SilentlyContinue
if($v){ W ('before=' + $v.'D:\Games\War3\War3.exe'); Remove-ItemProperty -Path $layers -Name 'D:\Games\War3\War3.exe' -Force; W ('after=' + (Get-ItemProperty -Path $layers -Name 'D:\Games\War3\War3.exe' -ErrorAction SilentlyContinue).'D:\Games\War3\War3.exe') } else { W 'no layer' }

W '=== D3D9 SANITY (32-bit) ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\D3D9Real32.exe' 2>&1 | Select-String -Pattern 'CreateDevice' | ForEach-Object { W $_.Line }

W '=== LAUNCH MAIN GAME ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\war3-shot.ps1' -Exe 'D:\Games\War3\War3.exe' -Wait 16 -Tag 'war3-main-fixed' 2>&1 | ForEach-Object { W $_ }
