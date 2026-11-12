# CoD5 启动器 --go 实测：观察存档检查/画面规范/重定向/AppData 零写入
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$g = 'D:\YunGame\CoD\CoD5'
$ev = 'D:\AI\legacy-game-compat\Games\CoD5\evidence'
$log = "$g\运行日志.txt"
Remove-Item $log -Force -ErrorAction SilentlyContinue
$appCfg = 'C:\Users\Administrator\AppData\Local\Activision\CoDWaW\players\profiles\$$$\config.cfg'
$appBefore = (Get-Item -LiteralPath $appCfg).LastWriteTime

$p = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $p.Id)
for ($i = 1; $i -le 4; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process CoDWaW -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process CoDWaW -Out (Join-Path $ev ("go-t$($i*10)s.png")) -WaitSeconds 2 -KeepOpen 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 游戏活着 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding)
  } else { Write-Output ("t+$($i*10)s 游戏进程没了"); break }
}
$gp = Get-Process CoDWaW -ErrorAction SilentlyContinue
if ($gp) {
  Write-Output ("40 秒存活，测试结束，10 秒后收尾杀掉测试进程")
  Start-Sleep -Seconds 10
  Get-Process CoDWaW, game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
}
$appAfter = (Get-Item -LiteralPath $appCfg).LastWriteTime
Write-Output ("AppData config 最后写入：测试前 $appBefore → 测试后 $appAfter（相同 = 零写入）")
Write-Output '----- 运行日志 -----'
Get-Content $log -Encoding UTF8 -ErrorAction SilentlyContinue
