# 试验 J：用 CoD7 的原版 binkw32.dll 顶进 CoD8（决定交付方案）
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

# 收尾残留 + Trojan 壳归位（保证起点一致）
Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Test-Path "$game\binkw32.dll.off") {
  if (Test-Path "$game\binkw32.dll") { Remove-Item "$game\binkw32.dll" -Force }
  Move-Item "$game\binkw32.dll.off" "$game\binkw32.dll" -Force
}

# 用 CoD7 原版 bink 顶替 Trojan 壳（Trojan 壳备份在 Games\CoD8\backup）
Copy-Item 'D:\YunGame\CoD\CoD7\binkw32.dll' "$game\binkw32.dll" -Force
Write-Output ("已用 CoD7 原版 bink 顶替（" + (Get-Item "$game\binkw32.dll").Length + " 字节）")

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
$last = ''
for ($i = 1; $i -le 9; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $last = ("t+$($i*10)s 活着 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding + " CPU=" + [Math]::Round($gp.CPU,1) + "s 线程=" + $gp.Threads.Count)
    Write-Output $last
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
$gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gp) {
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev 'repro-cod7bink.png') -WaitSeconds 8 2>$null | Out-Null
  Write-Output '截图已存 repro-cod7bink.png'
  Start-Sleep -Seconds 3
  Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
  Write-Output '已结束游戏进程（游戏能活 90 秒+ = 试验成功）'
}
# 交付状态：保持 CoD7 原版 bink（这是修复，不还原 Trojan 壳）；Trojan 壳另存
if (Test-Path 'D:\AI\legacy-game-compat\Games\CoD8\backup\binkw32.dll.before-test') {
  Move-Item 'D:\AI\legacy-game-compat\Games\CoD8\backup\binkw32.dll.before-test' 'D:\AI\legacy-game-compat\Games\CoD8\backup\binkw32.trojan-shell.dll' -Force
}
Write-Output 'Trojan 壳已另存为 backup\binkw32.trojan-shell.dll；游戏目录现在用 CoD7 原版 bink'
