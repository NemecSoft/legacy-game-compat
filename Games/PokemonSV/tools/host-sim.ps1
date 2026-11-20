<#
宝可梦朱紫 game_starter.exe 托管契约回归（全程只跑临时目录里的假游戏，不启动真游戏、
不碰真游戏目录任何文件）。假游戏 = 睡 8 秒退出码 7 的小程序，编译成
hostsim-run\runtime\scarlet\karpur.exe 与 runtime\violet\karpur.exe，其余 8 个必需文件用空占位。

断言：
  A1 游戏父 pid == 启动器 pid（直接子进程）
  A2 玩家关启动器窗口后：启动器仍活着（托盘陪跑）、游戏不受影响
  A3 游戏退出后启动器跟着退出，且退出码 == 游戏码 7
  B1 强杀启动器后，平台仍能从游戏句柄读到游戏自己的退出码 7
  C1 --edition=violet 真的拉起 runtime\violet\karpur.exe，日志自证「版本：紫」
  C2 紫版同样回传退出码 7

用法：pwsh -NoProfile -File Games\PokemonSV\tools\host-sim.ps1
结论另存 evidence\host-sim-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\Z\PokemonScarletandViolet'
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
  Get-CimInstance Win32_Process -Filter "Name='karpur.exe'" -ErrorAction SilentlyContinue |
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

$tmp = Join-Path $PSScriptRoot 'hostsim-run'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
try {
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  foreach ($ed in 'scarlet', 'violet') {
    $rt = Join-Path $tmp "runtime\$ed"
    New-Item -ItemType Directory -Force -Path $rt, (Join-Path $rt 'exefs'), (Join-Path $rt 'romfs') | Out-Null
    foreach ($f in 'SDL3.dll', 'libopus-0.dll', 'libiconv-2.dll', 'totk_xlate.exe', 'romfs-index.bin') {
      [IO.File]::WriteAllBytes((Join-Path $rt $f), [byte[]]@())
    }
    [IO.File]::WriteAllBytes((Join-Path $rt 'exefs\main'), [byte[]]@())
    [IO.File]::WriteAllBytes((Join-Path $rt 'exefs\main.npdm'), [byte[]]@())
    [IO.File]::WriteAllBytes((Join-Path $rt 'romfs\romfs.bin'), [byte[]]@())
  }
  Copy-Item (Join-Path $GameDir 'game_starter.exe') (Join-Path $tmp 'game_starter.exe') -Force
  Copy-Item (Join-Path $GameDir 'launcher.ini') (Join-Path $tmp 'launcher.ini') -Force
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
  foreach ($ed in 'scarlet', 'violet') {
    & $csc -nologo -target:winexe -optimize+ -out:(Join-Path $tmp "runtime\$ed\karpur.exe") (Join-Path $PSScriptRoot 'fake-game.cs')
    if ($LASTEXITCODE -ne 0) { throw "假游戏编译失败（$ed）" }
  }
  Write-Host "临时假游戏目录就绪：$tmp"

  # ---- 场景 A：朱版 --go，关窗不杀游戏，退出码原样回传 ----
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
    $gp = Get-Process -Id $g.ProcessId -ErrorAction SilentlyContinue
    if ($gp) { $gp.WaitForExit(15000) | Out-Null }
    $exited = $p.WaitForExit(15000)
    Assert 'A3 退出码回传' ($exited -and $p.ExitCode -eq 7) ("启动器退出=$exited 退出码=$($p.ExitCode)（期望 7）")
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

  # ---- 场景 C：--edition=violet 走紫版目录 ----
  $p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList '--go', '--edition=violet' -WorkingDirectory $tmp -PassThru
  $g = Wait-Game $tmp
  if (-not $g) { Assert 'C0 游戏被拉起' $false '25s 内没出现假游戏进程' }
  else {
    $isViolet = $g.ExecutablePath -like '*\runtime\violet\karpur.exe'
    $logText = ''
    $logPath = Join-Path $tmp '运行日志.txt'
    if (Test-Path $logPath) { $logText = [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) }
    $selfProof = $logText.Contains('启动参数：--go --edition=violet') -and $logText.Contains('版本：紫')
    Assert 'C1 紫版路径与日志自证' ($isViolet -and $selfProof) ("路径=$($g.ExecutablePath) 日志自证=$selfProof")
    $gp = Get-Process -Id $g.ProcessId -ErrorAction SilentlyContinue
    if ($gp) { $gp.WaitForExit(15000) | Out-Null }
    $exited = $p.WaitForExit(15000)
    Assert 'C2 紫版退出码回传' ($exited -and $p.ExitCode -eq 7) ("启动器退出=$exited 退出码=$($p.ExitCode)（期望 7）")
  }
}
finally {
  # 收尾：只清理本脚本临时目录里的进程与文件，不碰真游戏
  Get-CimInstance Win32_Process -Filter "Name='karpur.exe' OR Name='game_starter.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($tmp, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep -Milliseconds 500
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
  $summary = "host-sim 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'host-sim-result.txt'), $script:lines, (New-Object Text.UTF8Encoding($false)))
  Write-Host $summary
}
exit $script:fails