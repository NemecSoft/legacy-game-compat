<#
沙丘2000 重制版 game_starter.exe 托管契约 + dataDir 便携化回归（全程只跑临时目录里的假游戏，
不启动真游戏、不碰真游戏目录任何文件）。假游戏 = dump 命令行/工作目录后睡 8 秒退出码 7，
编译成 hostsim-run\D2kRemake_TrustBox.exe，其余必需文件用空占位。

断言：
  A1 游戏父 pid == 启动器 pid（直接子进程）
  A2 玩家关启动器窗口后：启动器仍活着（托盘陪跑）、游戏不受影响
  A3 游戏退出后启动器跟着退出，且退出码 == 游戏码 7
  A4 游戏收到的命令行没有额外参数（本作直接启动）
  A5 假游戏的工作目录 == 游戏目录
  B1 强杀启动器后，平台仍能从游戏句柄读到游戏自己的退出码 7
  C1 游戏写回的绝对路径 dataDir=<游戏目录>\data → 退出后改回相对 data
  C2 只改 dataDir 这一行：其余行逐字不动、LF 换行保留、无 BOM
  C3 日志自证「绝对路径已改回相对 data」
  D1 dataDir 指向别的目录（绝对路径）→ 保留不动，不误改玩家设置
  D2 日志自证「保留不动」
  E1 dataDir 已是相对路径 → settings.ini 字节一个不动（幂等）
  E2 日志自证「已是相对路径」

用法：pwsh -NoProfile -File Games\Dune2000Remake\tools\host-sim.ps1
结论另存 evidence\host-sim-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\F\dune2000Remake'
)
$ErrorActionPreference = 'Stop'
Add-Type -Name W -Namespace H -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool GetExitCodeProcess(IntPtr h, out uint code);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool EnumWindows(EnumProc cb, IntPtr l);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool IsWindowVisible(IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
public delegate bool EnumProc(IntPtr h, IntPtr l);
'@
$PROCESS_QUERY = 0x0400 -bor 0x1000
$script:fails = 0
$script:lines = New-Object System.Collections.Generic.List[string]
function Assert([string]$name, [bool]$cond, [string]$detail) {
  $tag = if ($cond) { 'PASS' } else { $script:fails++; 'FAIL' }
  $line = "$tag  $name  $detail"
  Write-Host $line
  $script:lines.Add($line)
}
function Find-Game([string]$root) {
  Get-CimInstance Win32_Process -Filter "Name='D2kRemake_TrustBox.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
}
function Wait-Game([string]$root, [int]$timeoutSec = 25) {
  $t0 = Get-Date
  while (((Get-Date) - $t0).TotalSeconds -lt $timeoutSec) {
    $g = Find-Game $root
    if ($g) { return $g }
    Start-Sleep -Milliseconds 400
  }
  return $null
}
function Close-LauncherWindow([int]$procId) {
  $script:target = [uint32]$procId
  $script:hit = [IntPtr]::Zero
  [H.W]::EnumWindows([H.W+EnumProc] {
    param($h, $l)
    $pid2 = [uint32]0
    [H.W]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
    if ($pid2 -eq $script:target -and [H.W]::IsWindowVisible($h)) { $script:hit = $h; return $false }
    return $true
  }, [IntPtr]::Zero) | Out-Null
  if ($script:hit -ne [IntPtr]::Zero) {
    [H.W]::PostMessageW($script:hit, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    return $true
  }
  return $false
}
function Wait-Both($p, $g) {
  if ($g) {
    $gp = Get-Process -Id $g.ProcessId -ErrorAction SilentlyContinue
    if ($gp) { $gp.WaitForExit(15000) | Out-Null }
  }
  $p.WaitForExit(15000) | Out-Null
}
function Log-Text([string]$root) {
  $lp = Join-Path $root '运行日志.txt'
  if (Test-Path $lp) { [IO.File]::ReadAllText($lp, [Text.Encoding]::UTF8) } else { '' }
}
function DataDir-Value([string]$path) {
  foreach ($l in [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)) {
    if ($l.StartsWith('dataDir=')) { return $l.Substring(8).Trim() }
  }
  return ''
}
function Run-Starter([string]$root, [string]$settingsPath, [string]$content) {
  [IO.File]::WriteAllText($settingsPath, $content, (New-Object Text.UTF8Encoding($false)))
  $p = Start-Process -FilePath (Join-Path $root 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $root -PassThru
  $g = Wait-Game $root
  if ($g) { Wait-Both $p $g } else { $p.WaitForExit(30000) | Out-Null }
  return @{ P = $p; G = $g }
}

$tmp = Join-Path $PSScriptRoot 'hostsim-run'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$absData = Join-Path $tmp 'data'
try {
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $tmp, (Join-Path $tmp 'data'), (Join-Path $tmp 'lang\zh'),
    (Join-Path $tmp 'config\lang\zh'), (Join-Path $tmp 'artpacks\opend2k-redesign') | Out-Null
  foreach ($f in 'SDL3.dll', 'data\DATA.R16', 'lang\zh\Text.uib', 'artpacks\opend2k-redesign\pack.ini') {
    [IO.File]::WriteAllBytes((Join-Path $tmp $f), [byte[]]@())
  }
  Copy-Item (Join-Path $GameDir 'game_starter.exe') (Join-Path $tmp 'game_starter.exe') -Force
  Copy-Item (Join-Path $GameDir 'launcher.ini') (Join-Path $tmp 'launcher.ini') -Force
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  & $csc -nologo -target:winexe -optimize+ -out:(Join-Path $tmp 'D2kRemake_TrustBox.exe') (Join-Path $PSScriptRoot 'fake-game.cs')
  if ($LASTEXITCODE -ne 0) { throw '假游戏编译失败' }
  $settings = Join-Path $tmp 'config\settings.ini'
  Write-Host "临时假游戏目录就绪：$tmp"

  # ---- 场景 A：--go，关窗陪跑 + 退出码 + 无参数 + 工作目录 + dataDir 绝对→相对 ----
  $origA = "# d2k-mac settings`ndataDir=$absData`ngraphics=enhanced`nfullscreen=0`nresolution=1920x1080`n"
  [IO.File]::WriteAllText($settings, $origA, $utf8)
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'A0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Assert 'A1 直接子进程' ([int]$g.ParentProcessId -eq $p.Id) ("游戏 pid=$($g.ProcessId) 父 pid=$($g.ParentProcessId) 启动器 pid=$($p.Id)")
    $closed = Close-LauncherWindow $p.Id
    Start-Sleep -Milliseconds 1500
    $p.Refresh()
    $gAlive = [bool](Find-Game $tmp)
    Assert 'A2 关窗陪跑' ($closed -and -not $p.HasExited -and $gAlive) ("关窗=$closed 启动器活着=$(-not $p.HasExited) 游戏活着=$gAlive")
    Wait-Both $p $g
    Assert 'A3 退出码回传' ($p.HasExited -and $p.ExitCode -eq 7) ("退出码=$($p.ExitCode)（期望 7）")
    $dump = if (Test-Path (Join-Path $tmp 'args.txt')) { [IO.File]::ReadAllText((Join-Path $tmp 'args.txt'), [Text.Encoding]::UTF8) } else { '' }
    $argsLine = ($dump.Split("`n") | Where-Object { $_.StartsWith('ARGS=') } | Select-Object -First 1)
    $argsVal = if ($argsLine) { $argsLine.Substring(5).TrimEnd("`r").Trim() } else { '（没有 args.txt）' }
    Assert 'A4 直接启动无参数' ($argsVal -eq '') ("假游戏命令行=[$argsVal]")
    $cwdLine = ($dump.Split("`n") | Where-Object { $_.StartsWith('CWD=') } | Select-Object -First 1)
    $cwd = if ($cwdLine) { $cwdLine.Substring(4).TrimEnd("`r") } else { '' }
    Assert 'A5 工作目录 = 游戏目录' ($cwd -eq $tmp) ("CWD=$cwd 期望=$tmp")
    Assert 'C1 绝对路径改回相对 data' ((DataDir-Value $settings) -eq 'data') ("dataDir=$(DataDir-Value $settings)（期望 data）")
    $newText = [IO.File]::ReadAllText($settings, [Text.Encoding]::UTF8)
    $expect = $origA.Replace("dataDir=$absData", 'dataDir=data')
    Assert 'C2 只改 dataDir 一行' ($newText -ceq $expect) ("逐字一致=$($newText -ceq $expect) 长度 $($origA.Length)→$($newText.Length)")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'C3 便携化日志自证' ($newLog.Contains('绝对路径已改回相对 data')) ("新日志含便携化行=$($newLog.Contains('绝对路径已改回相对 data'))")
  }

  # ---- 场景 B：强杀启动器（不等游戏退出），平台自己读游戏退出码 ----
  [IO.File]::WriteAllText($settings, $origA, $utf8)
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'B0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    $h = [H.W]::OpenProcess($PROCESS_QUERY, $false, [int]$g.ProcessId)
    Stop-Process -Id $p.Id -Force
    $gp = Get-Process -Id $g.ProcessId -ErrorAction SilentlyContinue
    if ($gp) { $gp.WaitForExit(15000) | Out-Null }
    $code = [uint32]0
    $got = [H.W]::GetExitCodeProcess($h, [ref]$code)
    [H.W]::CloseHandle($h) | Out-Null
    Assert 'B1 强杀后仍读到游戏码' ($got -and $code -eq 7) ("读到=$got 游戏码=$code（期望 7，游戏自然退出未被连带杀掉）")
  }

  # ---- 场景 D：dataDir 指向别的目录（绝对路径）→ 保留不动 ----
  $other = Join-Path $env:SystemDrive 'DuneData-Elsewhere'
  $origD = "# d2k-mac settings`ndataDir=$other`ngraphics=enhanced`nfullscreen=0`n"
  [IO.File]::WriteAllText($settings, $origD, $utf8)
  $lenBefore = (Log-Text $tmp).Length
  $r = Run-Starter $tmp $settings $origD
  if (-not $r.G) { Assert 'D0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Assert 'D1 别的数据目录保留不动' ((DataDir-Value $settings) -eq $other) ("dataDir=$(DataDir-Value $settings)（期望 $other）")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'D2 保留日志自证' ($newLog.Contains('不是本目录 data），保留不动')) ("新日志含保留行=$($newLog.Contains('不是本目录 data），保留不动'))")
  }

  # ---- 场景 E：已是相对 data → 字节不动（幂等） ----
  $origE = "# d2k-mac settings`ndataDir=data`ngraphics=enhanced`nfullscreen=0`n"
  [IO.File]::WriteAllText($settings, $origE, $utf8)
  $bytesBefore = [IO.File]::ReadAllBytes($settings)
  $lenBefore = (Log-Text $tmp).Length
  $r = Run-Starter $tmp $settings $origE
  if (-not $r.G) { Assert 'E0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    $bytesAfter = [IO.File]::ReadAllBytes($settings)
    $same = ($bytesBefore.Length -eq $bytesAfter.Length)
    if ($same) { for ($i = 0; $i -lt $bytesBefore.Length; $i++) { if ($bytesBefore[$i] -ne $bytesAfter[$i]) { $same = $false; break } } }
    Assert 'E1 相对路径字节不动' $same ("长度 $($bytesBefore.Length)→$($bytesAfter.Length) 逐字节相同=$same")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'E2 相对路径日志自证' ($newLog.Contains('已是相对路径 data，不改动')) ("新日志含相对路径行=$($newLog.Contains('已是相对路径 data，不改动'))")
  }
}
finally {
  # 收尾：只清理本脚本临时目录里的进程与文件，不碰真游戏
  Get-CimInstance Win32_Process -Filter "Name='D2kRemake_TrustBox.exe' OR Name='game_starter.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($tmp, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep -Milliseconds 500
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
  $summary = "host-sim 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'host-sim-result.txt'), $script:lines, $utf8)
  Write-Host $summary
}
exit $script:fails