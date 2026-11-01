$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }

W '=== AMD PROCESSES ==='
Get-Process | Where-Object { $_.Name -match 'radeon|amd|atie|atid|cnext|cncmd' } | Select-Object Name,Id,Path | ForEach-Object { W ($_.Name + ' pid=' + $_.Id + ' ' + $_.Path) }
W '=== AMD SERVICES ==='
Get-Service | Where-Object { $_.Name -match 'amd|radeon|atie' } | ForEach-Object { W ($_.Name + ' ' + $_.Status + ' ' + $_.StartType) }

W '=== AMD SETTINGS (HKCU/HKLM) matching feature names ==='
foreach($root in 'HKCU:\Software\AMD','HKLM:\SOFTWARE\AMD','HKLM:\SOFTWARE\WOW6432Node\AMD'){
  if(Test-Path $root){
    Get-ChildItem $root -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
      $p = Get-ItemProperty $_.PSPath
      foreach($n in $p.PSObject.Properties.Name){
        if($n -notmatch '^PS' -and $n -match 'AntiLag|Chill|Boost|Sharp|Overlay|D3D9|Hook|Injection|Enhanced|Sync|Freesync|VirtualSuper|Image|Scaling|Record|ReLive|DVR'){
          W ($_.PSPath.Replace('Microsoft.PowerShell.Core\Registry::','') + ' :: ' + $n + ' = ' + ($p.$n -join ','))
        }
      }
    }
  }
}

W '=== RADEON SOFTWARE LOGS ==='
Get-ChildItem "$env:LOCALAPPDATA\AMD\Radeonsoftware" -Recurse -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object { W ($_.FullName + ' ' + $_.Length + ' ' + $_.LastWriteTime) }

W '=== TDR / DISPLAY ERRORS TODAY ==='
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).Date} -ErrorAction SilentlyContinue | Where-Object { $_.Id -in 4101,4102,4103,14,13 -or $_.ProviderName -match 'amdkmdag|Display' } | Select-Object -First 20 | ForEach-Object { W ($_.TimeCreated.ToString('HH:mm:ss') + ' #' + $_.Id + ' ' + $_.ProviderName + ' | ' + (($_.Message -split "`r?`n")[0])) }

W '=== D3D9 BEFORE KILLING AMD SOFTWARE ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'GetDeviceCaps|CreateDevice\(' | ForEach-Object { W $_.Line }

W '=== STOP AMD USER-MODE SOFTWARE (not the kernel driver) ==='
$victims = 'RadeonSoftware','AMDRSServ','amdow','amdfendrsr','cncmd','StartCN','atieclxx','atiedxx','AMDUserExperienceProgram'
foreach($n in $victims){
  $ps = Get-Process $n -ErrorAction SilentlyContinue
  foreach($p in $ps){ W ('kill ' + $p.Name + ' pid=' + $p.Id); Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
Start-Sleep -Seconds 3

W '=== D3D9 AFTER KILLING AMD SOFTWARE ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'GetDeviceCaps|CreateDevice\(' | ForEach-Object { W $_.Line }
