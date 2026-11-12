# 扫今天 00:00 以来游戏目录所有变更文件 + 主程序/关键DLL时间戳
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'

Write-Output '===== 主程序与关键 DLL ====='
foreach ($f in @('iw5sp.exe','binkw32.dll','binkw32org.dll','steam_api.dll','mss32.dll','SteamAPIUpdater.dll','UpdateDLLWrapper.dll')) {
  $p = Join-Path $game $f
  if (Test-Path $p) { $i = Get-Item $p; Write-Output ($f + "  " + $i.Length + "B  改于 " + $i.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')) }
}
Write-Output '===== 今天 00:00 以来变更的全部文件 ====='
Get-ChildItem $game -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -ge [datetime]'2026-11-10 00:00:00' } |
  Sort-Object LastWriteTime |
  ForEach-Object { Write-Output ($_.LastWriteTime.ToString('HH:mm:ss') + "  " + $_.FullName.Substring($game.Length+1) + "  " + $_.Length) }
