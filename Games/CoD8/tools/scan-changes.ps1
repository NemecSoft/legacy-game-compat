# 还原 config + 扫描游戏目录 03:15-03:45 之间变更的文件
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$bak = 'D:\AI\legacy-game-compat\Games\CoD8\backup\config.cfg.before-noconfig-test'
$cfg = Join-Path $game 'players2\config.cfg'

if (-not (Test-Path $cfg) -and (Test-Path $bak)) {
  Copy-Item $bak $cfg -Force
  Write-Output 'config.cfg 已从备份还原'
} else { Write-Output ("config.cfg 现状: " + (Test-Path $cfg)) }

Write-Output '===== 03:15-03:45 之间变更的文件（全游戏目录，排除 evidence 类）====='
Get-ChildItem $game -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -ge [datetime]'2026-11-10 03:15:00' -and $_.LastWriteTime -le [datetime]'2026-11-10 03:45:00' } |
  Sort-Object LastWriteTime |
  ForEach-Object { Write-Output ($_.LastWriteTime.ToString('HH:mm:ss') + "  " + $_.FullName.Substring($game.Length+1) + "  " + $_.Length) }
