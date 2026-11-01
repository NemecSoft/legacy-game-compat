$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }
$cls = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000'
$vid = 'HKLM:\SYSTEM\CurrentControlSet\Control\Video\{40B326F1-D5A1-11F1-A7F9-D9F11FF66943}'
$keys = @($cls)
Get-ChildItem $vid | ForEach-Object { $keys += $_.PSPath }
W '=== RESTORE KMD ORIGINAL (DesktopTexture=1, DisplayableSupport=0) ==='
foreach($k in $keys){
  foreach($n in 'KMD_EnableDesktopTexture','KMD_EnableDisplayableSupport'){
    if((Get-ItemProperty $k -Name $n -ErrorAction SilentlyContinue) -ne $null){
      Set-ItemProperty -Path $k -Name $n -Value ($(if($n -eq 'KMD_EnableDesktopTexture'){1}else{0})) -Type DWord
    }
  }
}
foreach($k in @($cls)+@($vid+'\0000')){
  $p = Get-ItemProperty $k
  W ($k + ' DesktopTexture=' + $p.KMD_EnableDesktopTexture + ' DisplayableSupport=' + $p.KMD_EnableDisplayableSupport)
}
W '=== RESTART GPU ==='
$id = 'PCI\VEN_1002&DEV_6FDF&SUBSYS_E3531DA2&REV_EF\4&9D5262B&0&0019'
pnputil /restart-device $id 2>&1 | ForEach-Object { W $_ }
Start-Sleep -Seconds 8
Get-CimInstance Win32_VideoController | ForEach-Object { W ($_.Name + ' mode=' + $_.VideoModeDescription + ' hz=' + $_.CurrentRefreshRate + ' status=' + $_.Status) }
W '=== D3D9 SANITY AFTER RESTORE ==='
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe' 2>&1 | Select-String -Pattern 'GetDeviceCaps|CreateDevice\(' | ForEach-Object { W $_.Line }
