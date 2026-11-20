<#
命令与征服：红色警戒 game_starter.exe 托管契约 + 机器名回归（全程只跑临时目录里的假游戏，
不启动真游戏、不碰真游戏目录任何文件）。假游戏 = dump 命令行/工作目录后睡 8 秒退出码 7，
编译成 hostsim-run\Engine\RedAlert.exe，其余 12 个必需文件用空占位。

断言：
  A1 游戏父 pid == 启动器 pid（直接子进程）
  A2 玩家关启动器窗口后：启动器仍活着（托盘陪跑）、游戏不受影响
  A3 游戏退出后启动器跟着退出，且退出码 == 游戏码 7
  A4 假游戏收到的命令行含 Game.Mod=ra 与 Engine.LaunchPath=（启动契约）
  A5 假游戏的工作目录 == <root>\Engine
  A6 settings.yaml 的 Player: Name: 变成本机电脑名（用户定案 2026-11-20）
  A7 只改 Name 那一行：其余行逐字不动、行数不变、原 LF 换行保留
  B1 强杀启动器后，平台仍能从游戏句柄读到游戏自己的退出码 7
  C1 值已是机器名时日志自证「已是本机电脑名…不改动」
  C2 幂等：settings.yaml 字节一个不动
  D1 settings.yaml 缺失时自动创建默认设置且 Name = 机器名
  D2 日志自证「已创建默认设置」+「联机显示名设为本机电脑名」
  E1 Player 节缺 Name 行：紧跟节头补一行，值 = 机器名
  E2 日志自证「Player 节没有 Name 行，已补上」
  F1 整份没有 Player 节：末尾补节，值 = 机器名
  F2 日志自证「没有 Player 节，已补上」

用法：pwsh -NoProfile -File Games\CC1RedAlert\tools\host-sim.ps1
结论另存 evidence\host-sim-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\F\CC1-RedAlert'
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
  Get-CimInstance Win32_Process -Filter "Name='RedAlert.exe'" -ErrorAction SilentlyContinue |
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
function Name-Value([string]$text) {
  if ($text -match '(?m)^[ \t]*Name:[ \t]*(.+?)[ \t]*$') { $Matches[1] } else { '' }
}

$tmp = Join-Path $PSScriptRoot 'hostsim-run'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$machine = $env:COMPUTERNAME
$utf8 = New-Object Text.UTF8Encoding($false)
try {
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  $engine = Join-Path $tmp 'Engine'
  New-Item -ItemType Directory -Force -Path $engine, (Join-Path $engine 'mods\ra'), (Join-Path $engine 'Support') | Out-Null
  foreach ($f in 'RedAlert.dll', 'RedAlert.runtimeconfig.json', 'OpenRA.Game.dll', 'OpenRA.Mods.Common.dll',
    'OpenRA.Mods.Cnc.dll', 'OpenRA.Platforms.Default.dll', 'SDL2.dll', 'soft_oal.dll',
    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'mods\ra\mod.yaml') {
    [IO.File]::WriteAllBytes((Join-Path $engine $f), [byte[]]@())
  }
  Copy-Item (Join-Path $GameDir 'game_starter.exe') (Join-Path $tmp 'game_starter.exe') -Force
  Copy-Item (Join-Path $GameDir 'launcher.ini') (Join-Path $tmp 'launcher.ini') -Force
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  & $csc -nologo -target:winexe -optimize+ -out:(Join-Path $engine 'RedAlert.exe') (Join-Path $PSScriptRoot 'fake-game.cs')
  if ($LASTEXITCODE -ne 0) { throw '假游戏编译失败' }
  $settings = Join-Path $engine 'Support\settings.yaml'
  Write-Host "临时假游戏目录就绪：$tmp（机器名 $machine）"

  # ---- 场景 A：--go，关窗陪跑 + 退出码 + 启动契约 + 机器名写入 ----
  $origA = "Graphics:`n`tMode: Windowed`n`tWindowedSize: 1024, 720`n`tCapFramerate: True`nPlayer:`n`tName: 指挥官`nGame:`n`tMouseControlStyle: Modern`n"
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
    $argsPath = Join-Path $engine 'args.txt'
    $dump = if (Test-Path $argsPath) { [IO.File]::ReadAllText($argsPath, [Text.Encoding]::UTF8) } else { '' }
    Assert 'A4 启动参数契约' ($dump.Contains('Game.Mod=ra') -and $dump.Contains('Engine.LaunchPath=')) ("假游戏命令行=$($dump.Split("`n")[0])")
    $cwdLine = ($dump.Split("`n") | Where-Object { $_.StartsWith('CWD=') } | Select-Object -First 1)
    $cwd = if ($cwdLine) { $cwdLine.Substring(4).TrimEnd("`r") } else { '' }
    Assert 'A5 工作目录 = Engine' ($cwd -eq $engine) ("CWD=$cwd 期望=$engine")
    $newText = [IO.File]::ReadAllText($settings, [Text.Encoding]::UTF8)
    Assert 'A6 联机名 = 机器名' ((Name-Value $newText) -eq $machine) ("Name=$(Name-Value $newText) 期望=$machine")
    $ol = $origA.Split("`n"); $nl2 = $newText.Split("`n")
    $diff = 0; $nameDiff = $false
    for ($i = 0; $i -lt [Math]::Max($ol.Count, $nl2.Count); $i++) {
      $a = if ($i -lt $ol.Count) { $ol[$i].TrimEnd("`r") } else { '' }
      $b = if ($i -lt $nl2.Count) { $nl2[$i].TrimEnd("`r") } else { '' }
      if ($a -ne $b) { $diff++; if ($b.TrimStart().StartsWith('Name:')) { $nameDiff = $true } }
    }
    Assert 'A7 只改 Name 一行' (($diff -eq 1) -and $nameDiff -and ($ol.Count -eq $nl2.Count) -and (-not $newText.Contains("`r`n"))) ("差异行数=$diff 行数 $($ol.Count)→$($nl2.Count) LF 保留=$(-not $newText.Contains("`r`n"))")
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

  # ---- 场景 C：值已是机器名 → 幂等，字节不动 ----
  $bytesBefore = [IO.File]::ReadAllBytes($settings)
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'C0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'C1 幂等日志自证' ($newLog.Contains("已是本机电脑名 $machine，不改动 settings.yaml")) ("新日志含幂等行=$($newLog.Contains('已是本机电脑名'))")
    $bytesAfter = [IO.File]::ReadAllBytes($settings)
    $same = ($bytesBefore.Length -eq $bytesAfter.Length)
    if ($same) { for ($i = 0; $i -lt $bytesBefore.Length; $i++) { if ($bytesBefore[$i] -ne $bytesAfter[$i]) { $same = $false; break } } }
    Assert 'C2 settings 字节不动' $same ("长度 $($bytesBefore.Length)→$($bytesAfter.Length) 逐字节相同=$same")
  }

  # ---- 场景 D：settings.yaml 缺失 → 建默认 + Name = 机器名 ----
  Remove-Item $settings -Force
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'D0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $newText = if (Test-Path $settings) { [IO.File]::ReadAllText($settings, [Text.Encoding]::UTF8) } else { '' }
    Assert 'D1 缺文件自动创建' ((Test-Path $settings) -and ($newText.Contains('Mode: Windowed')) -and ($newText.Contains('MouseControlStyle: Modern')) -and ((Name-Value $newText) -eq $machine)) ("存在=$(Test-Path $settings) Name=$(Name-Value $newText)")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'D2 创建日志自证' ($newLog.Contains('已创建默认设置') -and $newLog.Contains('联机显示名设为本机电脑名')) ("创建行=$($newLog.Contains('已创建默认设置')) 改名行=$($newLog.Contains('联机显示名设为本机电脑名'))")
  }

  # ---- 场景 E：Player 节缺 Name 行 → 紧跟节头补行 ----
  $origE = "Graphics:`n`tMode: Windowed`nPlayer:`n`tLanguage: zh`nGame:`n`tMouseControlStyle: Modern`n"
  [IO.File]::WriteAllText($settings, $origE, $utf8)
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'E0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $newText = [IO.File]::ReadAllText($settings, [Text.Encoding]::UTF8)
    $ls = $newText.Split("`n")
    $pi = -1
    for ($i = 0; $i -lt $ls.Count; $i++) { if ($ls[$i].TrimEnd("`r") -eq 'Player:') { $pi = $i; break } }
    $inserted = ($pi -ge 0) -and ($pi + 1 -lt $ls.Count) -and ($ls[$pi + 1].TrimStart().StartsWith('Name:')) -and ((Name-Value $newText) -eq $machine)
    Assert 'E1 节头后补 Name 行' $inserted ("Player 行=$pi 下一行=$(if ($pi -ge 0 -and $pi + 1 -lt $ls.Count) { $ls[$pi + 1].TrimEnd("`r") } else { '无' })")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'E2 补行日志自证' ($newLog.Contains('Player 节没有 Name 行，已补上')) ("新日志含补行行=$($newLog.Contains('Player 节没有 Name 行，已补上'))")
  }

  # ---- 场景 F：整份没有 Player 节 → 末尾补节 ----
  $origF = "Graphics:`n`tMode: Windowed`nGame:`n`tMouseControlStyle: Modern`n"
  [IO.File]::WriteAllText($settings, $origF, $utf8)
  $lenBefore = (Log-Text $tmp).Length
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'F0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    Wait-Both $p $g
    $newText = [IO.File]::ReadAllText($settings, [Text.Encoding]::UTF8)
    $tailOk = $newText.EndsWith("Player:`n`tName: $machine`n")
    Assert 'F1 末尾补 Player 节' ($tailOk -and ($newText.StartsWith($origF))) ("尾部匹配=$tailOk 原内容保留=$($newText.StartsWith($origF))")
    $all = Log-Text $tmp
    $newLog = $all.Substring([Math]::Min($lenBefore, $all.Length))
    Assert 'F2 补节日志自证' ($newLog.Contains('没有 Player 节，已补上')) ("新日志含补节行=$($newLog.Contains('没有 Player 节，已补上'))")
  }
}
finally {
  # 收尾：只清理本脚本临时目录里的进程与文件，不碰真游戏
  Get-CimInstance Win32_Process -Filter "Name='RedAlert.exe' OR Name='game_starter.exe'" -ErrorAction SilentlyContinue |
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