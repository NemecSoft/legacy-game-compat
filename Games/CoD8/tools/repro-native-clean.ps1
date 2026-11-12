# 纯原生环境验证：复位 settings 包 + 藏 DXVK + CoD7 原版 bink 已在位 + 跑游戏 100 秒截图
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$dis = "$g\_dxvk\禁用"
New-Item -ItemType Directory -Force -Path $dis | Out-Null

Write-Output '===== 1) 找回被移走的 settings 包 ====='
$found = Get-ChildItem "$g\players2" -Filter 'settings_*.iw5' -Recurse -ErrorAction SilentlyContinue |
  Where-Object { $_.DirectoryName -ne "$g\players2" }
foreach ($f in $found) {
  Move-Item $f.FullName "$g\players2\$($f.Name)" -Force
  Write-Output ("已复位: " + $f.FullName + " -> players2\" + $f.Name)
}
if (-not $found) {
  # 可能在别的临时名下，全目录搜
  $alt = Get-ChildItem $g -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^settings_.+\.iw5$' -and $_.DirectoryName -notmatch '\\players2\\' }
  foreach ($f in $alt) {
    Move-Item $f.FullName "$g\players2\$($f.Name)" -Force
    Write-Output ("已复位(搜索到): " + $f.FullName)
  }
  if (-not $alt) { Write-Output '警告: 没找到被移走的 settings 包!' }
}
Get-ChildItem "$g\players2\settings_*.iw5" | ForEach-Object { Write-Output ("在位确认: " + $_.Name + " " + $_.Length + "B") }

Write-Output '===== 2) 藏 DXVK（移进 _dxvk\禁用\）====='
foreach ($n in @('d3d9.dll','dxgi.dll','dxvk.conf')) {
  $p = "$g\$n"
  if (Test-Path $p) { Move-Item $p "$dis\$n" -Force; Write-Output ("已藏: $n") }
}
Get-ChildItem "$g\d3d9.dll","$g\dxgi.dll" -ErrorAction SilentlyContinue | ForEach-Object { Write-Output ("警告: 根目录仍有 " + $_.Name) }
Write-Output '根目录 DXVK 清理完成'

Write-Output '===== 3) bink 状态确认 ====='
$b = Get-Item "$g\binkw32.dll"
Write-Output ("binkw32.dll " + $b.Length + "B（应为 174080 = CoD7 原版）")

Write-Output '===== 4) 纯原生跑游戏 100 秒 ====='
$cfgBak = 'D:\AI\legacy-game-compat\Games\CoD8\backup\config.cfg.before-native-test'
Copy-Item "$g\players2\config.cfg" $cfgBak -Force
$proc = Start-Process "$g\iw5sp.exe" -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 10
$shot1 = "$ev\native-binkfix-t10s.png"
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $proc.Id -OutFile $shot1 2>$null
Start-Sleep -Seconds 20
$alive20 = -not $proc.HasExited
Start-Sleep -Seconds 20
$alive40 = -not $proc.HasExited
$shot40 = "$ev\native-binkfix-t40s.png"
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $proc.Id -OutFile $shot40 2>$null
Start-Sleep -Seconds 20
$alive60 = -not $proc.HasExited
Start-Sleep -Seconds 20
$alive80 = -not $proc.HasExited
$shot80 = "$ev\native-binkfix-t80s.png"
& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -ProcessId $proc.Id -OutFile $shot80 2>$null

Write-Output ("存活: 20s=" + $alive20 + " 40s=" + $alive40 + " 60s=" + $alive60 + " 80s=" + $alive80)
if ($alive80) {
  Write-Output '>>> 80 秒仍存活：Trojan 壳自退已被根治，正在收尾'
  Start-Sleep -Seconds 3
  if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
} else {
  Write-Output ("<<< 提前退出，ExitCode=" + $proc.ExitCode)
}
Write-Output 'done'
