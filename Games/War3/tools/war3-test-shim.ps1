$key='HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$name='D:\Games\War3\War3.exe'
$bak='D:\AI\legacy-game-compat\Games\JustCause\tools\war3-appcompat-backup.txt'
$before=(Get-ItemProperty $key -Name $name -EA SilentlyContinue).$name
Write-Output ("before=[" + $before + "]")
if($before){
  Set-Content -Path $bak -Value $name -Encoding UTF8
  Add-Content -Path $bak -Value $before -Encoding UTF8
  Remove-ItemProperty -Path $key -Name $name -Force
  Write-Output 'removed (备份在 tools\war3-appcompat-backup.txt)'
}
Start-Sleep -Milliseconds 300
# 跑一次，抓对话框
& 'D:\AI\legacy-game-compat\Games\JustCause\tools\war3-dialog.ps1' 2>&1 | Select-Object -First 6
# 还原
if($before){
  Set-ItemProperty -Path $key -Name $name -Value $before
  $after=(Get-ItemProperty $key -Name $name -EA SilentlyContinue).$name
  Write-Output ("restored=[" + $after + "]")
}