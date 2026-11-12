# 试验 I：原版 binkw32org.dll 顶替 hook 版 → 能否真正进游戏
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

# 收尾残留
Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Test-Path "$game\binkw32.dll.off") {
  if (Test-Path "$game\binkw32.dll") { Remove-Item "$game\binkw32.dll" -Force }
  Move-Item "$game\binkw32.dll.off" "$game\binkw32.dll" -Force
}
# hook 版已备份过（backup\binkw32.dll.before-test），现在用原版顶位
Copy-Item "$game\binkw32org.dll" "$game\binkw32.dll" -Force
Write-Output ("binkw32.dll 已换成原版（" + (Get-Item "$game\binkw32.dll").Length + " 字节，hook 版备份在 Games\CoD8\backup）")

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    Write-Output ("t+$($i*10)s 活着 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding + " CPU=" + [Math]::Round($gp.CPU,1) + "s 线程=" + $gp.Threads.Count)
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
# 活着就截图取证
$gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gp) {
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev 'repro-origbink.png') -WaitSeconds 5 2>$null | Out-Null
  Write-Output '截图已存 repro-origbink.png'
  Start-Sleep -Seconds 3
  Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
  Write-Output '已结束游戏进程'
} else {
  Write-Output ("退出码=" + $p.ExitCode)
}
# 还原 hook 版（交付状态）
Copy-Item 'D:\AI\legacy-game-compat\Games\CoD8\backup\binkw32.dll.before-test' "$game\binkw32.dll" -Force
Write-Output ("binkw32.dll 已还原为交付状态（" + (Get-Item "$game\binkw32.dll").Length + " 字节）")
