# 回滚恢复操作：把 _prev-20261119 里的项移回游戏目录（恢复操作的可逆回退）
$ErrorActionPreference = 'Stop'
$g = 'D:\YunGame\G\Raiden4'
$prev = Join-Path $g '_prev-20261119'
if (-not (Test-Path $prev)) { Write-Output '无 _prev，无需回滚'; exit 0 }
if (@(Get-Process -Name game,game_starter,TeknoParrotUi -ErrorAction SilentlyContinue).Count -gt 0) {
  Write-Output '有进程在跑，停手不回滚'; exit 1
}
$n = 0
Get-ChildItem $prev -Force | ForEach-Object {
  $dst = Join-Path $g $_.Name
  if ((Test-Path $dst) -and ($_.Name -ne 'teknoparrot.ini')) { Write-Output ('目标已存在，跳过：' + $_.Name) }
  else { Move-Item -LiteralPath $_.FullName -Destination $dst -Force; $n++ }
}
Write-Output ('已移回 ' + $n + ' 项')
if (@(Get-ChildItem $prev -Force).Count -eq 0) { Remove-Item $prev -Force; Write-Output '_prev 已空，删除' }
Write-Output '==== 回滚后顶层 ===='
Get-ChildItem $g | ForEach-Object { Write-Output $_.Name }