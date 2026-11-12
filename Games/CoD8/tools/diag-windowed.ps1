# 黑屏劈因：窗口模式对照试验（试验后自动还原 config）+ 虚拟显示器状态
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$cfg = "$g\players2\config.cfg"
$raw = [Text.Encoding]::GetEncoding(28591)

# 0) 虚拟显示器/显示设备现状
Write-Output '===== 显示设备清单 ====='
Get-CimInstance Win32_PnPEntity -Filter "PNPClass='Monitor'" -ErrorAction SilentlyContinue | ForEach-Object { Write-Output ($_.Name + "  [" + $_.Status + "]  " + $_.PNPDeviceID.Substring(0, [Math]::Min(60, $_.PNPDeviceID.Length))) }

# 1) 备份 config，改窗口模式
Copy-Item $cfg "$cfg.windowed-test-bak" -Force
$txt = $raw.GetString([IO.File]::ReadAllBytes($cfg))
$txt = [regex]::Replace($txt, '(?m)^(seta[ \t]+r_displayMode[ \t]+)"[^"]*"', '$1"windowed"')
[IO.File]::WriteAllBytes($cfg, $raw.GetBytes($txt))
Write-Output 'config 已临时改为 windowed'

# 2) 启动（绕开启动器，直接跑游戏，变量最少）
if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }
$proc = Start-Process "$g\iw5sp.exe" -WorkingDirectory $g -PassThru
Write-Output ("iw5sp pid=" + $proc.Id)

foreach ($t in @(8, 15)) {
  Start-Sleep -Seconds $(if ($t -eq 8) { 8 } else { 7 })
  $p = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { Write-Output ("t=$t`s 已退出"); break }
  # GPU 占用（进程是否在渲染）
  $gpu = (Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples |
    Where-Object { $_.InstanceName -match 'iw5sp' -and $_.CookedValue -gt 0 } |
    Measure-Object CookedValue -Sum
  Write-Output ("t=$t`s 存活 pid=" + $p.Id + " 主窗=" + $p.MainWindowHandle + " GPU利用率=" + [Math]::Round($gpu.Sum, 1) + "%")
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\windowed-t$($t)s.png" -WaitSeconds 2 2>$null
}

# 3) 收尾：杀进程 + 还原 config
Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Copy-Item "$cfg.windowed-test-bak" $cfg -Force
Remove-Item "$cfg.windowed-test-bak" -Force
Write-Output 'config 已还原 fullscreen'
Write-Output 'done'
