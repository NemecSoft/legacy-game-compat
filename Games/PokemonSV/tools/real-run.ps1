<#
宝可梦朱紫 真游戏单次启动验证（门禁：必须显式 -AllowGameLaunch，否则 exit 90 什么都不做）。
只启动一次朱版 --go：等游戏窗口出现（首次进入编译着色器，最多等 120s）→
capture-window.ps1 截游戏窗口自身矩形 → 结束游戏进程（测试收尾，不是窗口摆布）→
断言启动器回传游戏退出码、日志自证行齐全、无残留进程。

用法：pwsh -NoProfile -File Games\PokemonSV\tools\real-run.ps1 -AllowGameLaunch
结论另存 evidence\real-run-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\Z\PokemonScarletandViolet',
  [switch]$AllowGameLaunch
)
if (-not $AllowGameLaunch) {
  Write-Host '门禁：本脚本会真启动游戏。确认要跑请加 -AllowGameLaunch。'
  exit 90
}
$ErrorActionPreference = 'Stop'
Add-Type -Name W -Namespace H -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool GetExitCodeProcess(IntPtr h, out uint code);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr h);
'@
$script:fails = 0
$script:lines = New-Object System.Collections.Generic.List[string]
function Assert([string]$name, [bool]$cond, [string]$detail) {
  $tag = if ($cond) { 'PASS' } else { $script:fails++; 'FAIL' }
  $line = "$tag  $name  $detail"
  Write-Host $line
  $script:lines.Add($line)
}
$exe = Join-Path $GameDir 'game_starter.exe'
$logPath = Join-Path $GameDir '运行日志.txt'
$evidence = Join-Path $PSScriptRoot '..\evidence'
$shot = Join-Path $PSScriptRoot '..\..\..\scripts\capture-window.ps1'
$logBefore = if (Test-Path $logPath) { [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) } else { '' }
$p = Start-Process -FilePath $exe -ArgumentList '--go' -WorkingDirectory $GameDir -PassThru
$gameProc = $null
$gameH = [IntPtr]::Zero
try {
  $t0 = Get-Date
  while (((Get-Date) - $t0).TotalSeconds -lt 120) {
    $gameProc = Get-Process -Name karpur -ErrorAction SilentlyContinue |
      Where-Object { $_.Path -and $_.Path.StartsWith($GameDir, [StringComparison]::OrdinalIgnoreCase) -and $_.MainWindowHandle -ne 0 } |
      Select-Object -First 1
    if ($gameProc) { break }
    Start-Sleep -Milliseconds 500
  }
  if (-not $gameProc) {
    Assert '游戏窗口出现' $false '120s 内 karpur 没有可见窗口（首次编译着色器也可能更久，看运行日志）'
  }
  else {
    $gameH = [H.W]::OpenProcess(0x0400 -bor 0x1000, $false, $gameProc.Id)
    Assert '游戏窗口出现' $true ("pid=$($gameProc.Id) 窗口标题=$($gameProc.MainWindowTitle) 用时 $([int]((Get-Date)-$t0).TotalSeconds)s")
    & $shot -Process karpur -Out (Join-Path $evidence 'game-first-window.png') -WaitSeconds 10 -KeepOpen
    Assert '游戏窗口截图' (Test-Path (Join-Path $evidence 'game-first-window.png')) 'evidence\game-first-window.png'
    # 测试收尾：结束游戏进程（终止进程，不是对窗口做摆布）
    Stop-Process -Id $gameProc.Id -Force
    $code = [uint32]0
    $got = [H.W]::GetExitCodeProcess($gameH, [ref]$code)
    $exited = $p.WaitForExit(15000)
    # 退出码按 uint 读回（强杀是 0xFFFFFFFF），转 int 必须走字节reinterpret，直接 [int] 会溢出抛异常
    $signed = [BitConverter]::ToInt32([BitConverter]::GetBytes($code), 0)
    Assert '启动器回传游戏退出码' ($exited -and $got -and $p.ExitCode -eq $signed) ("启动器退出=$exited 启动器码=$($p.ExitCode) 游戏码=$signed")
  }
  $logAfter = if (Test-Path $logPath) { [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) } else { '' }
  $newLog = $logAfter.Substring([Math]::Min($logBefore.Length, $logAfter.Length))
  $proof = $newLog.Contains('启动参数：--go') -and $newLog.Contains('零交互：是') -and
    $newLog.Contains('（本进程的 direct child）') -and $newLog.Contains('profile 目录已就绪') -and
    $newLog.Contains('游戏退出码：')
  Assert '日志自证行齐全' $proof '启动参数/零交互/direct child/profile/退出码'
  $noWarn = -not $newLog.Contains('参数疑似被 shell 路径转换')
  Assert '无 shell 路径转换警告' $noWarn ''
}
catch {
  Assert '脚本执行无异常' $false $_.Exception.Message
}
finally {
  if ($gameH -ne [IntPtr]::Zero) { [H.W]::CloseHandle($gameH) | Out-Null }
  Get-Process -Name karpur, game_starter -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($GameDir, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
  if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  $summary = "real-run 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'real-run-result.txt'), $script:lines, (New-Object Text.UTF8Encoding($false)))
  Write-Host $summary
}
exit $script:fails