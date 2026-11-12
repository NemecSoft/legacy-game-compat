# CoD8 原生档 --go 决定性验证：bink 修复 + 首次规范 + 键位写入 + 分时截图
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$log = "$g\运行日志.txt"
if (Test-Path $log) { Remove-Item $log -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $proc.Id)
Start-Sleep -Seconds 12
# 找游戏进程（启动器拉起的子进程）
$game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($game) {
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $game.Id -OutFile "$ev\native-final-t12s.png" 2>$null
  Write-Output ("t=12s 游戏进程在 pid=" + $game.Id)
} else { Write-Output 't=12s 无 iw5sp 进程' }

Start-Sleep -Seconds 28
$g2 = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
$alive40 = ($null -ne $g2)
if ($alive40) { & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $g2.Id -OutFile "$ev\native-final-t40s.png" 2>$null }
Write-Output ("t=40s 存活=" + $alive40)

Start-Sleep -Seconds 40
$g3 = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
$alive80 = ($null -ne $g3)
if ($alive80) {
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $g3.Id -OutFile "$ev\native-final-t80s.png" 2>$null
  Write-Output 't=80s 仍存活 >>> 20 秒自退已根治，收尾杀进程'
  Start-Sleep -Seconds 2
  Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
  Get-Process game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
} else {
  Write-Output 't=80s 游戏已退出 <<< 仍需排查'
}
Write-Output 'done'
