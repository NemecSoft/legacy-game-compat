# 试验 E：查重启/远程会话事件 + 窗口模式跑游戏（因果试验）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$cfg = Join-Path $game 'players2\config.cfg'

Write-Output '===== 1. 最近重启/会话事件 ====='
Get-WinEvent -FilterHashtable @{LogName='System'; Id=6005,6008,6009,107,27,42,1074; StartTime=(Get-Date).AddHours(-3)} -ErrorAction SilentlyContinue |
  Select-Object -First 6 | ForEach-Object { Write-Output ($_.TimeCreated.ToString('HH:mm:ss') + " [" + $_.Id + "] " + $_.ProviderName) }
Write-Output '===== 2. 当前活动显示输出 ====='
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Screen]::AllScreens | ForEach-Object { Write-Output ($_.DeviceName + " " + $_.Bounds.ToString() + " 主=" + $_.Primary) }

Write-Output '===== 3. 窗口模式试验（r_displayMode=windowed）====='
$raw = [Text.Encoding]::GetEncoding(28591)
$txt = $raw.GetString([IO.File]::ReadAllBytes($cfg))
$txt = [Regex]::Replace($txt, '(?m)^seta[ \t]+r_displayMode[ \t]+"[^"]*"', 'seta r_displayMode "windowed"')
[IO.File]::WriteAllBytes($cfg, $raw.GetBytes($txt))
Write-Output '已改 r_displayMode=windowed'

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 5; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-windowed-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(10000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode)
# 还原 fullscreen
$txt2 = $raw.GetString([IO.File]::ReadAllBytes($cfg))
$txt2 = [Regex]::Replace($txt2, '(?m)^seta[ \t]+r_displayMode[ \t]+"[^"]*"', 'seta r_displayMode "fullscreen"')
[IO.File]::WriteAllBytes($cfg, $raw.GetBytes($txt2))
Write-Output 'r_displayMode 已还原 fullscreen'
