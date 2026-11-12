# 对照试验：Start-Process（ShellExecuteEx 路，与双击/cmd start 同一条路）拉起 iw5sp.exe，
# 观察 40 秒。若存活，则证明「纯 CreateProcess 被壳杀、ShellExecuteEx 能活」。
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
Write-Output ("ShellExecuteEx 启动 pid=" + $p.Id)

for ($i = 1; $i -le 4; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("verify-shellexec-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 pid=" + $gp.Id + " 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding)
  } else {
    Write-Output "t+$($i*10)s 游戏进程没了"
    break
  }
}
$gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gp) {
  Write-Output '>>> 40 秒仍存活：ShellExecuteEx 路能活（与双击一致）'
  Write-Output '（进程留着不杀，交给用户处理）'
} else {
  Write-Output '<<< 40 秒内退出：ShellExecuteEx 路今天也死，需另找原因'
}
