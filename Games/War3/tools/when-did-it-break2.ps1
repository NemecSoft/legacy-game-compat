Write-Output '=== restore points ==='
Get-ComputerRestorePoint -EA SilentlyContinue | Select-Object -Last 5 | Format-Table -AutoSize | Out-String | Write-Output
Write-Output '=== Windows Update history (last 10) ==='
try {
  $s = New-Object -ComObject Microsoft.Update.Session
  $searcher = $s.CreateUpdateSearcher()
  $count = $searcher.GetTotalHistoryCount()
  if($count -gt 0){ $searcher.QueryHistory(0, [Math]::Min(10,$count)) | ForEach-Object { Write-Output ($_.Date.ToString('MM-dd HH:mm') + ' | ' + $_.Title + ' | result=' + $_.ResultCode) } }
} catch { Write-Output ('update history failed: ' + $_.Exception.Message) }
Write-Output '=== AMD Software (CN) ==='
foreach($k in 'HKLM:\SOFTWARE\AMD\CN','HKLM:\SOFTWARE\WOW6432Node\AMD\CN'){
  if(Test-Path $k){ Write-Output ('KEY ' + $k); Get-ItemProperty $k | Select-Object * -ExcludeProperty PS* | Format-List | Out-String | Write-Output }
}
Write-Output '=== AMD driver store files ==='
$dir='C:\Windows\System32\DriverStore\FileRepository\u0201039.inf_amd64_89c3272f19eab06b\B025980'
if(Test-Path $dir){
  Get-ChildItem $dir -File | Where-Object { $_.Name -match 'atiumd|aticfx|atidxx|amdihk|amdxc|amf' } | Sort-Object Name |
    ForEach-Object { $s=Get-AuthenticodeSignature $_.FullName; Write-Output ("{0,-22} {1,10} {2} sig={3}" -f $_.Name,$_.Length,$_.LastWriteTime.ToString('yyyy-MM-dd'),$s.Status) }
} else { Write-Output 'driver store dir missing' }
Write-Output '=== d3d9 log from War3 (current) ==='
if(Test-Path 'D:\Games\War3\War3_d3d9.log'){ Get-Content 'D:\Games\War3\War3_d3d9.log' -Tail 5 }