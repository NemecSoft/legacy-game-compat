# 验证：CoDWaW.exe +set fs_localAppData / fs_BaseGame 是否把 players 目录重定向进游戏目录
# 观察 40 秒：游戏目录是否出现 players\；AppData 是否被再次写入；截图必须 -KeepOpen（坑 111）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD5'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD5\evidence'
New-Item -ItemType Directory -Force -Path $ev | Out-Null

$exe  = Join-Path $game 'CoDWaW.exe'
$args = '+set fs_localAppData "' + $game + '" +set fs_BaseGame "' + $game + '"'
$si = New-Object System.Diagnostics.ProcessStartInfo
$si.FileName = $exe
$si.Arguments = $args
$si.WorkingDirectory = $game
$si.UseShellExecute = $false
Write-Output ("启动参数: " + $si.Arguments)
$p = [System.Diagnostics.Process]::Start($si)

for ($i = 1; $i -le 4; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process CoDWaW -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process CoDWaW -Out (Join-Path $ev ("fsrepro-t$($i*10)s.png")) -WaitSeconds 2 -KeepOpen 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding)
  } else { Write-Output ("t+$($i*10)s 进程退出"); break }
}

"== 游戏目录 players =="; Test-Path "$game\players"
if (Test-Path "$game\players") { Get-ChildItem "$game\players" -Recurse | Select-Object FullName, Length | Format-Table -AutoSize | Out-String -Width 200 }
"== AppData profiles 最后写入时间（对照之前 05:37-05:40） =="
Get-ChildItem "$env:LOCALAPPDATA\Activision\CoDWaW\players\profiles" -Recurse -File | Select-Object Name, LastWriteTime | Format-Table -AutoSize | Out-String

$gp = Get-Process CoDWaW -ErrorAction SilentlyContinue
if ($gp) {
  Write-Output ("观察结束，测试进程还在（pid=" + $gp.Id + "），10 秒后自动结束它")
  Start-Sleep -Seconds 10
  Get-Process CoDWaW -ErrorAction SilentlyContinue | Stop-Process -Force
  Write-Output '测试进程已结束'
}
