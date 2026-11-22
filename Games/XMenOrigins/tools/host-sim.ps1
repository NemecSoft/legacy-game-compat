<#
X战警前传：金刚狼 game_starter.exe 托管契约 + 注册表 / 初始化回归（全程只跑临时目录里的假游戏，
不启动真游戏、不碰真游戏目录任何文件）。假游戏 = dump 命令行/工作目录后睡 8 秒退出码 7；
假 init = 往自己目录追加一行时间后退出 0。测试改的注册表值在 finally 里按原值还原。

断言：
  A1 游戏父 pid == 启动器 pid（直接子进程）
  A2 玩家关启动器窗口后：启动器仍活着（托盘陪跑）、游戏不受影响
  A3 游戏退出后启动器跟着退出，且退出码 == 游戏码 7
  A4 游戏命令行只有 exe 全路径、无参数（与自带 运行游戏.bat 一致）
  A5 游戏工作目录 == 游戏根目录
  R1 注册表 HKLM\SOFTWARE\WOW6432Node\Activision\Wolverine\EXEString == 带引号的当前 exe 路径
  R2 日志自证「注册表：EXEString =」
  I1 init.exe 默认执行（假 init 写出的 init-ran.txt 有记录）
  B1 强杀启动器后，平台仍能从游戏句柄读到游戏自己的退出码 7
  R3 值已对时幂等：日志「已是当前路径，不改动」
  I2 init 每次启动都执行（第二次启动后再多一行）
  R4 注册表是旧路径时被纠正回当前路径
  R5 纠正日志自证「（原值 …）」

用法：pwsh -NoProfile -File Games\XMenOrigins\tools\host-sim.ps1
结论另存 evidence\host-sim-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\M\X-MenOrigins-Wolverine'
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
$REG_SUB = 'SOFTWARE\Activision\Wolverine'
$script:fails = 0
$script:lines = New-Object System.Collections.Generic.List[string]
function Assert([string]$name, [bool]$cond, [string]$detail) {
  $tag = if ($cond) { 'PASS' } else { $script:fails++; 'FAIL' }
  $line = "$tag  $name  $detail"
  Write-Host $line
  $script:lines.Add($line)
}
function Get-ExeString {
  $b = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry32)
  try {
    $k = $b.OpenSubKey($REG_SUB)
    if (-not $k) { return $null }
    try { $v = $k.GetValue('EXEString', $null); return $v } finally { $k.Dispose() }
  } finally { $b.Dispose() }
}
function Set-ExeString([string]$v) {
  $b = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry32)
  try {
    $k = $b.CreateSubKey($REG_SUB)
    try { $k.SetValue('EXEString', $v, [Microsoft.Win32.RegistryValueKind]::String) } finally { $k.Dispose() }
  } finally { $b.Dispose() }
}
function Remove-ExeValue {
  $b = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry32)
  try {
    $k = $b.OpenSubKey($REG_SUB, $true)
    if ($k) { try { $k.DeleteValue('EXEString', $false) } finally { $k.Dispose() } }
  } finally { $b.Dispose() }
}
function Find-Game([string]$root) {
  Get-CimInstance Win32_Process -Filter "Name='Wolverine.exe'" -ErrorAction SilentlyContinue |
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
function Init-Runs([string]$root) {
  $f = Join-Path $root 'init-ran.txt'
  if (Test-Path $f) { @([IO.File]::ReadAllLines($f, [Text.Encoding]::UTF8)).Count } else { 0 }
}

$tmp = Join-Path $PSScriptRoot 'hostsim-run'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$hadOld = $false
$oldExe = $null
try {
  $oldExe = Get-ExeString
  $hadOld = ($null -ne $oldExe)
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  $bin = Join-Path $tmp 'Binaries'
  New-Item -ItemType Directory -Force -Path $bin, (Join-Path $tmp 'WGame\Config') | Out-Null
  foreach ($f in 'binkw32.dll', 'ogg.dll', 'vorbis.dll', 'vorbisfile.dll', 'NxCooking.dll', 'PhysXExtensions.dll') {
    [IO.File]::WriteAllBytes((Join-Path $bin $f), [byte[]]@())
  }
  [IO.File]::WriteAllBytes((Join-Path $tmp 'WGame\Config\DefaultEngine.ini'), [byte[]]@())
  Copy-Item (Join-Path $GameDir 'game_starter.exe') (Join-Path $tmp 'game_starter.exe') -Force
  Copy-Item (Join-Path $GameDir 'launcher.ini') (Join-Path $tmp 'launcher.ini') -Force
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  & $csc -nologo -target:winexe -optimize+ -out:(Join-Path $bin 'Wolverine.exe') (Join-Path $PSScriptRoot 'fake-game.cs')
  if ($LASTEXITCODE -ne 0) { throw '假游戏编译失败' }
  & $csc -nologo -target:exe -optimize+ -out:(Join-Path $tmp 'init.exe') (Join-Path $PSScriptRoot 'fake-init.cs')
  if ($LASTEXITCODE -ne 0) { throw '假 init 编译失败' }
  $wantReg = '"' + (Join-Path $bin 'Wolverine.exe') + '"'
  Write-Host "临时假游戏目录就绪：$tmp（注册表旧值：$(if ($hadOld) { $oldExe } else { '无' })）"

  # ---- 场景 A：--go，关窗陪跑 + 退出码 + 启动契约 + 注册表动态写 + init 默认执行 ----
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
    $argsPath = Join-Path $bin 'args.txt'
    $dump = if (Test-Path $argsPath) { [IO.File]::ReadAllText($argsPath, [Text.Encoding]::UTF8) } else { '' }
    $cmdLine = ($dump.Split("`n") | Where-Object { $_.StartsWith('CMD=') } | Select-Object -First 1)
    $cmd = if ($cmdLine) { $cmdLine.Substring(4).TrimEnd("`r") } else { '' }
    Assert 'A4 命令行只有 exe 路径' ($cmd -eq $wantReg) ("CMD=$cmd 期望=$wantReg")
    $cwdLine = ($dump.Split("`n") | Where-Object { $_.StartsWith('CWD=') } | Select-Object -First 1)
    $cwd = if ($cwdLine) { $cwdLine.Substring(4).TrimEnd("`r") } else { '' }
    Assert 'A5 工作目录 = 游戏根目录' ($cwd -eq $tmp) ("CWD=$cwd 期望=$tmp")
    $reg = Get-ExeString
    Assert 'R1 注册表 EXEString = 当前路径' ($reg -eq $wantReg) ("注册表=$reg 期望=$wantReg")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'R2 注册表日志自证' ($newLog.Contains('注册表：EXEString =')) ("新日志含写入行=$($newLog.Contains('注册表：EXEString ='))")
    Assert 'I1 init 默认执行' ((Init-Runs $tmp) -ge 1) ("init 执行记录=$((Init-Runs $tmp)) 行")
  }

  # ---- 场景 B：强杀启动器，平台自己读游戏退出码 ----
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

  # ---- 场景 C：值已对 → 幂等；init 仍每次执行 ----
  $runsBefore = Init-Runs $tmp
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'C0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'R3 注册表幂等日志' ($newLog.Contains('已是当前路径，不改动')) ("新日志含幂等行=$($newLog.Contains('已是当前路径，不改动'))")
    Assert 'I2 init 每次启动都执行' ((Init-Runs $tmp) -gt $runsBefore) ("执行记录 $runsBefore → $(Init-Runs $tmp) 行")
  }

  # ---- 场景 D：注册表是旧路径 → 启动时纠正回当前路径 ----
  $stale = '"C:\Old\Path\Binaries\Wolverine.exe"'
  Set-ExeString $stale
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'D0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $reg = Get-ExeString
    Assert 'R4 旧路径被纠正' ($reg -eq $wantReg) ("注册表=$reg 期望=$wantReg")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'R5 纠正日志自证' ($newLog.Contains('（原值 ' + $stale + '）')) ("新日志含原值行=$($newLog.Contains('（原值 ' + $stale + '）'))")
  }
}
finally {
  # 收尾：只清理本脚本临时目录里的进程与文件，不碰真游戏；注册表按测试前原值还原
  Get-CimInstance Win32_Process -Filter "Name='Wolverine.exe' OR Name='game_starter.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($tmp, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep -Milliseconds 500
  try { if ($hadOld) { Set-ExeString $oldExe } else { Remove-ExeValue } } catch { }
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
  $summary = "host-sim 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'host-sim-result.txt'), $script:lines, $utf8)
  Write-Host $summary
}
exit $script:fails
