# 试验 B：无 config 对照（引擎自建默认）+ DXVK 文件日志
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$cfg = Join-Path $game 'players2\config.cfg'

# 备份并移走 config（玩家可用 backup 恢复，引擎也能自建）
Copy-Item $cfg 'D:\AI\legacy-game-compat\Games\CoD8\backup\config.cfg.before-noconfig-test' -Force
Remove-Item $cfg -Force
Write-Output 'config.cfg 已移走（备份在 Games\CoD8\backup\config.cfg.before-noconfig-test）'

Remove-Item "$game\iw5sp_d3d9.log" -Force -ErrorAction SilentlyContinue
$env:DXVK_LOG_LEVEL = 'info'
$env:DXVK_LOG_PATH = $game
$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-noconfig-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(15000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode)
Write-Output '----- DXVK 日志尾部 -----'
if (Test-Path "$game\iw5sp_d3d9.log") { Get-Content "$game\iw5sp_d3d9.log" -Tail 30 -Encoding UTF8 } else { Get-ChildItem "$game\*d3d9*.log","$game\*.log" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName } }
Write-Output '----- 新 config 是否生成 -----'
if (Test-Path $cfg) { Write-Output ('已生成，大小=' + (Get-Item $cfg).Length); Select-String -Path $cfg -Pattern 'r_mode|r_displayRefresh|r_displayMode|sys_gpu' -Encoding Latin1 | ForEach-Object { $_.Line } } else { Write-Output '未生成' }
