# 试验 A：绕开启动器直接跑 iw5sp.exe（DXVK 在位），DXVK_LOG_LEVEL=info 抓现场
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

Remove-Item "$game\iw5sp_d3d9.log" -Force -ErrorAction SilentlyContinue
$env:DXVK_LOG_LEVEL = 'info'
$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $resp = $gp.Responding
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-direct-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$resp 窗口标题=" + $gp.MainWindowTitle)
  } else { Write-Output "t+$($i*10)s 游戏已退出 退出码获取中"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(20000) | Out-Null; if (-not $p.HasExited) { Write-Output '30s 后仍在跑，本试验到此为止（保持观察）' } }
Write-Output ("进程已结束，退出码=" + $p.ExitCode)
if (Test-Path "$game\iw5sp_d3d9.log") {
  Write-Output '----- DXVK info 日志（尾部 40 行）-----'
  Get-Content "$game\iw5sp_d3d9.log" -Tail 40 -Encoding UTF8
} else {
  Write-Output '没有 iw5sp_d3d9.log（DXVK 层没被加载或没写日志）'
}
