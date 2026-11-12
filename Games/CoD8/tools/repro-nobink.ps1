# 试验 H：binkw32.dll（汉化 hook 层）移走对照 + CPU 采样
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$bk = 'D:\AI\legacy-game-compat\Games\CoD8\backup'

Copy-Item "$game\binkw32.dll" "$bk\binkw32.dll.before-test" -Force
Move-Item "$game\binkw32.dll" "$game\binkw32.dll.off" -Force
Write-Output 'binkw32.dll 已移开（原版 binkw32org.dll 未动）'

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $cpu = [Math]::Round($gp.CPU, 1)
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out ("D:\AI\legacy-game-compat\Games\CoD8\evidence\repro-nobink-t$($i*10)s.png") -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding) CPU累计=$($cpu)s 线程=$($gp.Threads.Count)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(10000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode)

# 无论结果都把 binkw32.dll 移回原位（交付状态不能变）
if (Test-Path "$game\binkw32.dll.off") { Move-Item "$game\binkw32.dll.off" "$game\binkw32.dll" -Force; Write-Output 'binkw32.dll 已移回原位' }
