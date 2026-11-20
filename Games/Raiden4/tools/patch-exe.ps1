param(
  [string]$Exe = 'D:\YunGame\G\Raiden4\game.exe',
  [string]$Offset,
  [string]$Bytes,
  [switch]$Show
)
$ErrorActionPreference = 'Stop'
$bak = $Exe + '.orig-bak'
if (-not (Test-Path $bak)) { throw "缺备份 $bak，拒绝写" }
$off = [Convert]::ToInt32($Offset, 16)
$data = [IO.File]::ReadAllBytes($Exe)
if ($Show) {
  $n = 32
  $seg = New-Object byte[] $n
  [Array]::Copy($data, $off, $seg, 0, $n)
  Write-Output (($off.ToString('X8') + ': ' + ([BitConverter]::ToString($seg) -replace '-', ' ')))
  return
}
$patch = $Bytes -split '[ ,]+' | ForEach-Object { [Convert]::ToByte($_, 16) }
if ($off + $patch.Length -gt $data.Length) { throw '越界' }
$old = New-Object byte[] $patch.Length
[Array]::Copy($data, $off, $old, 0, $patch.Length)
[Array]::Copy($patch, 0, $data, $off, $patch.Length)
[IO.File]::WriteAllBytes($Exe, $data)
$back = [IO.File]::ReadAllBytes($Exe)
$chk = New-Object byte[] $patch.Length
[Array]::Copy($back, $off, $chk, 0, $patch.Length)
Write-Output ('原: ' + ([BitConverter]::ToString($old) -replace '-', ' '))
Write-Output ('新: ' + ([BitConverter]::ToString($chk) -replace '-', ' '))
if ([BitConverter]::ToString($chk) -ne [BitConverter]::ToString($patch)) { throw '回读不一致' }
Write-Output 'patch 完成并回读自证'