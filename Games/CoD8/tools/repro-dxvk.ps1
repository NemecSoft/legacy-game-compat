# CoD8 DXVK 卡死复现取证：--go 启动 → 分时截图 → 收退出码与日志尾部
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\CoD\CoD8'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$log  = Join-Path $game '运行日志.txt'
$before = (Get-Item $log).Length

# 确认现在根目录 d3d9/dxgi 在位（DXVK 档）
Get-ChildItem "$game\d3d9.dll","$game\dxgi.dll" | ForEach-Object { Write-Output ("DXVK层: " + $_.Name + " " + $_.Length) }

$p = Start-Process -FilePath (Join-Path $game 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $game -PassThru
Write-Output ("启动器 pid=" + $p.Id)

for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 15
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  $stamp = Get-Date -Format 'HHmmss'
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-dxvk-$stamp.png")) -WaitSeconds 2 2>$null
    $resp = ''
    try { $resp = $gp.Responding } catch { $resp = '查不到' }
    Write-Output ("t+" + ($i*15) + "s 游戏进程活着 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $resp)
  } else {
    Write-Output ("t+" + ($i*15) + "s 游戏进程已退出")
    break
  }
}
# 等启动器收尾（最多再等 30s）
if (-not $p.HasExited) { $p.WaitForExit(30000) | Out-Null }
Write-Output ("启动器退出码=" + $p.ExitCode)
# 新增日志段
$txt = [IO.File]::ReadAllText($log, [Text.Encoding]::UTF8)
Write-Output '----- 本轮新增日志 -----'
Write-Output ($txt.Substring($before))
