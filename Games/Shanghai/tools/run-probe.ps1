# 通过一次性计划任务执行 probe-once.ps1（工具会话结束会杀掉会话内进程树，必须脱离）。
# 按 AGENTS 规则 24 第 14 类：会真启动游戏的测试默认拒绝，必须显式 -AllowGameLaunch。
param(
  [ValidateSet('none','dxvk','dgvoodoo')][string]$Layer = 'none',
  [ValidateSet('keep','800')][string]$Desktop = 'keep',
  [string]$GameArgs = '-windows',
  [string]$Tag = 'probe',
  [switch]$AllowGameLaunch
)
$ErrorActionPreference = 'Continue'
if (-not $AllowGameLaunch) {
  throw '本脚本会真启动游戏，按 AGENTS 规则 24 第 14 类必须显式加 -AllowGameLaunch'
}
$game = 'D:\YunGame\IndieGame\shanghai'
$repo = 'D:\AI\legacy-game-compat'
$once = Join-Path $repo 'Games\Shanghai\tools\probe-once.ps1'
$evi  = Join-Path $repo 'Games\Shanghai\evidence'
$log  = Join-Path $evi ("probe-$Tag.txt")
$pwsh = 'C:\Program Files\PowerShell\7\pwsh.exe'
$taskName = 'YunGameShanghaiProbe'
if (-not (Test-Path $pwsh)) { $pwsh = (Get-Command pwsh.exe).Source }

function CurMode() {
  $v = Get-CimInstance Win32_VideoController |
       Where-Object { $_.CurrentHorizontalResolution -gt 0 -and $_.CurrentVerticalResolution -gt 0 } |
       Select-Object -First 1
  if ($v) { return @($v.CurrentHorizontalResolution, $v.CurrentVerticalResolution) }
  return @(1920, 1080)
}
$orig = CurMode
$argLine = '-NoProfile -ExecutionPolicy Bypass -File "' + $once + '"' +
           ' -Layer ' + $Layer + ' -Desktop ' + $Desktop +
           ' -GameArgs "' + $GameArgs + '" -Tag ' + $Tag
Write-Output ("任务命令行：" + $argLine)

try {
  $action = New-ScheduledTaskAction -Execute $pwsh -Argument $argLine
  Register-ScheduledTask -TaskName $taskName -Action $action -Force -Description 'YunGame 一次性实机实验' | Out-Null
  Start-ScheduledTask -TaskName $taskName
  Write-Output '任务已启动，等待实验体收尾…'
  $deadline = (Get-Date).AddSeconds(120)
  while ((Get-Date) -lt $deadline) {
    if ((Test-Path $log) -and (Select-String -LiteralPath $log -Pattern '清理完成' -Quiet)) { break }
    Start-Sleep -Seconds 2
  }
  if (Test-Path $log) { Get-Content -LiteralPath $log -Encoding UTF8 }
  else { Write-Output '实验体没有写出结果文件' }
}
finally {
  # 兜底：无论实验体有没有正常收尾，桌面/进程/层文件都回到实验前状态
  Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
  Stop-Process -Name 'shanghai' -Force -ErrorAction SilentlyContinue
  & (Join-Path $game 'SETRES.EXE') ("h$($orig[0])") ("v$($orig[1])") 2>&1 | Out-Null
  $layerDir = Join-Path $repo "Games\Shanghai\tools\probe\$Layer"
  if ($Layer -ne 'none' -and (Test-Path $layerDir)) {
    foreach ($f in (Get-ChildItem -LiteralPath $layerDir -File |
                    Where-Object { $_.Name -ne 'D3D8Probe.exe' -and $_.Extension -ne '.log' })) {
      Remove-Item -LiteralPath (Join-Path $game $f.Name) -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath (Join-Path $game 'dxvk.conf') -Force -ErrorAction SilentlyContinue
  }
  Write-Output ("兜底还原完成：桌面 {0}x{1}" -f $orig[0], $orig[1])
}
