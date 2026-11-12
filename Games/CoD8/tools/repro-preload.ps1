# 试验 1：r_preloadShaders 0/1 对照复现（hang 嫌疑变量）
param([switch]$Revert)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$cfg = 'D:\YunGame\CoD\CoD8\players2\config.cfg'
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$log = Join-Path $game '运行日志.txt'

if ($Revert) {
  Copy-Item 'D:\AI\legacy-game-compat\Games\CoD8\backup\config.cfg.before-preloadtest' $cfg -Force
  Write-Output '已还原玩家 config'
  return
}

# 把 r_preloadShaders 改成 0（Latin1 字节级替换，坑 95）
$raw = [Text.Encoding]::GetEncoding(28591)
$txt = $raw.GetString([IO.File]::ReadAllBytes($cfg))
$txt = [Regex]::Replace($txt, '(?m)^seta[ \t]+r_preloadShaders[ \t]+"[^"]*"', 'seta r_preloadShaders "0"')
[IO.File]::WriteAllBytes($cfg, $raw.GetBytes($txt))
Write-Output ("已改 r_preloadShaders=0，当前行: " + ([Regex]::Match($txt, '(?m)^seta r_preloadShaders.*$').Value))

$before = [Regex]::Matches($txt, 'x').Count # 占位，下面用日志增量
$logLinesBefore = (Get-Content $log).Count

$p = Start-Process -FilePath (Join-Path $game 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 15
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $resp = $gp.Responding
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-preload0-t$($i*15)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*15)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$resp")
  } else { Write-Output "t+$($i*15)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(30000) | Out-Null }
Write-Output ("启动器退出码=" + $p.ExitCode)
Write-Output '----- 本轮新增日志 -----'
Get-Content $log -Encoding UTF8 | Select-Object -Skip $logLinesBefore
