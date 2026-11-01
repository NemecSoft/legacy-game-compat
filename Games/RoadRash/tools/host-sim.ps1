<#
主 GUI（启动平台）模拟器：验证 game_starter.exe 的托管契约链路
    主GUI 启动 game_starter.exe → game_starter.exe 起游戏 → 玩家关启动器 → 游戏退出 → 主GUI 拿到**游戏的**退出码

用法：
  pwsh -File host-sim.ps1                     打开模拟主 GUI，手动点按钮走流程
  pwsh -File host-sim.ps1 -Auto               自动跑两条场景（假游戏，不占屏）并断言
  pwsh -File host-sim.ps1 -Auto -RealGame     用真游戏跑（全屏几秒，脚本到时自己结束游戏）

假游戏 = 临时把 launcher.ini 指到 cmd.exe：睡 N 秒后以指定退出码结束；脚本退出时自动还原 ini。
#>
param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [switch]$Auto,
  [switch]$RealGame,
  [int]$FakeExitCode = 7,
  [int]$FakeSeconds = 8,
  [int]$RealGameKillAfter = 25
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Name W -Namespace H -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool GetExitCodeProcess(IntPtr h, out uint code);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr h);
'@

$PROCESS_QUERY = 0x0400 -bor 0x1000
$STILL_ACTIVE = 259
$script:box = $null

$s = @{
  go = $null; goH = [IntPtr]::Zero; goCode = $null
  gameH = [IntPtr]::Zero; gameCode = $null; gamePid = 0; gameName = ''
  parentPid = 0; iniPath = $null; iniBak = $null
}

function Open-H([int]$procId) { return [H.W]::OpenProcess($PROCESS_QUERY, $false, $procId) }

function Get-Code([IntPtr]$h) {
  if ($h -eq [IntPtr]::Zero) { return $null }
  $c = [uint32]0
  if (-not [H.W]::GetExitCodeProcess($h, [ref]$c)) { return $null }
  return $c
}

function Fmt-Code($c) {
  if ($null -eq $c) { return '未退出' }
  $u = [uint32]$c
  $signed = [BitConverter]::ToInt32([BitConverter]::GetBytes($u), 0)
  return ('{0} (0x{1:X8})' -f $signed, $u)
}

# 从 launcher.ini 读 GameExe，用来认出哪个子进程是游戏（game_starter.exe 还会先起 keymap.exe）
function Get-GameName {
  $path = Join-Path $GameDir 'launcher.ini'
  if (Test-Path $path) {
    foreach ($line in [IO.File]::ReadAllLines($path)) {
      if ($line -match '^\s*GameExe\s*=\s*(.+)$') {
        return [IO.Path]::GetFileNameWithoutExtension($matches[1].Trim())
      }
    }
  }
  return ''
}

function Log([string]$m) {
  $line = (Get-Date).ToString('HH:mm:ss') + '  ' + $m
  Write-Host $line
  if ($script:box) { $script:box.AppendText($line + "`r`n") }
}

function Use-FakeGame {
  $path = Join-Path $GameDir 'launcher.ini'
  $bak = Join-Path $GameDir 'launcher.ini.hostsim-bak'
  if (-not (Test-Path $path)) { Log "找不到 $path"; return }
  if (-not $s.iniPath) {                     # 只在第一次换之前备份，别把假配置当真配置存下来
    Copy-Item $path $bak -Force
    $s.iniPath = $path; $s.iniBak = $bak
  }
  $ini = @"
[Launcher]
Title=托管链路测试（假游戏）
GameExe=C:\Windows\System32\cmd.exe
GameArgs=/c ping -n $FakeSeconds 127.0.0.1 >nul & exit $FakeExitCode
LayerDll=ddraw.dll
LayerIni=ddraw.ini
LayerBackupDir=_cnc-ddraw
SavesDir=各选手1-5级存档
"@
  [IO.File]::WriteAllText($path, $ini, (New-Object System.Text.UTF8Encoding($false)))
  Log "launcher.ini 已换成假游戏：睡 $FakeSeconds 秒 → 退出码 $FakeExitCode"
}

function Restore-Game {
  if ($s.iniPath -and $s.iniBak -and (Test-Path $s.iniBak)) {
    Move-Item $s.iniBak $s.iniPath -Force
    Log 'launcher.ini 已还原'
    $s.iniPath = $null
  }
}

function Start-Launcher([bool]$zeroClick) {
  Stop-All
  $argList = @(); if ($zeroClick) { $argList = @('/go') }
  $s.go = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList $argList `
        -WorkingDirectory $GameDir -PassThru
  $s.goH = Open-H $s.go.Id
  $s.goCode = $null; $s.gameH = [IntPtr]::Zero; $s.gameCode = $null
  $s.gamePid = 0; $s.parentPid = 0
  Log ("已启动 game_starter.exe  pid=" + $s.go.Id + $(if ($zeroClick) { '  (/go 零交互)' } else { '' }))
}

function Stop-All {
  Get-Process game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
  if (-not $RealGame) { return }
  Get-Process Roadrash -ErrorAction SilentlyContinue | Stop-Process -Force
}

# 找 game_starter.exe 的直接子进程 = 游戏（按 launcher.ini 的 GameExe 认，跳过 keymap.exe 这类工具）
function Find-Game {
  if ($s.gamePid -ne 0) { return }
  if (-not $s.go) { return }
  $want = Get-GameName
  $kids = Get-CimInstance Win32_Process -Filter "ParentProcessId=$($s.go.Id)" -ErrorAction SilentlyContinue
  foreach ($k in $kids) {
    $base = [IO.Path]::GetFileNameWithoutExtension($k.Name)
    if ($base -in @('game_starter', 'conhost', 'keymap')) { continue }
    if ($want -and $base -ne $want) { continue }
    $s.gamePid = [int]$k.ProcessId
    $s.gameName = $k.Name
    $s.parentPid = [int]$k.ParentProcessId
    $s.gameH = Open-H $s.gamePid
    Log ("发现子进程（游戏）：" + $k.Name + " pid=" + $s.gamePid + " 父 pid=" + $k.ParentProcessId)
    return
  }
}

function Poll {
  Find-Game
  if ($s.gamePid -ne 0 -and $null -eq $s.gameCode) {
    $c = Get-Code $s.gameH
    if ($null -ne $c -and $c -ne $STILL_ACTIVE) {
      $s.gameCode = $c
      Log ("游戏已退出，退出码 = " + (Fmt-Code $c))
      [H.W]::CloseHandle($s.gameH) | Out-Null; $s.gameH = [IntPtr]::Zero
    }
  }
  if ($null -eq $s.goCode) {
    $c = Get-Code $s.goH
    if ($null -ne $c -and $c -ne $STILL_ACTIVE) {
      $s.goCode = $c
      Log ("game_starter.exe 已退出，退出码 = " + (Fmt-Code $c))
      [H.W]::CloseHandle($s.goH) | Out-Null; $s.goH = [IntPtr]::Zero
    }
  }
}

function Wait-For([scriptblock]$cond, [int]$seconds) {
  $t = 0
  while ($t -lt $seconds * 10) {
    Poll
    if (& $cond) { return $true }
    Start-Sleep -Milliseconds 100
    $t++
  }
  return $false
}

function Show-Status {
  if (-not $script:lbl) { return }
  $script:lbl.Text = "game_starter.exe pid=" + $(if ($s.go) { $s.go.Id } else { '-' }) +
    "  退出码=" + (Fmt-Code $s.goCode) + "`r`n" +
    "游戏 pid=" + $(if ($s.gamePid) { $s.gamePid.ToString() + '(' + $s.gameName + ')' } else { '未发现' }) +
    "  父 pid=" + $s.parentPid + "  退出码=" + (Fmt-Code $s.gameCode)
}

# 等游戏退出；真游戏不会自己退，到点由脚本结束它
function Wait-GameEnd {
  $limit = if ($RealGame) { $RealGameKillAfter } else { $FakeSeconds + 25 }
  $t = 0
  while ($null -eq $s.gameCode -and $t -lt $limit * 10) {
    Poll
    if ($null -eq $s.gameCode) { Start-Sleep -Milliseconds 100; $t++ }
  }
  if ($null -eq $s.gameCode -and $RealGame -and $s.gamePid) {
    Log "真游戏不会自己退，到点结束它（pid=$($s.gamePid)）"
    Get-Process -Id $s.gamePid -ErrorAction SilentlyContinue | Stop-Process -Force
    $t = 0
    while ($null -eq $s.gameCode -and $t -lt 50) { Poll; Start-Sleep -Milliseconds 100; $t++ }
  }
  return ($null -ne $s.gameCode)
}

function Run-Auto {
  $fail = 0
  if (-not $RealGame) { Use-FakeGame }

  Log '—— 场景 A：关启动器窗口（隐藏陪跑）→ 游戏退出 → 主 GUI 应拿到游戏退出码 ——'
  Start-Launcher $true
  if (-not (Wait-For { $s.gamePid -ne 0 } 30)) { Log 'FAIL 30 秒内没等到游戏进程'; $fail = 1 }
  if ($s.parentPid -ne $s.go.Id) { Log "FAIL 父进程 $($s.parentPid) != game_starter.exe $($s.go.Id)"; $fail = 1 }
  else { Log 'OK   游戏是 game_starter.exe 的直接子进程（平台可按树跟踪）' }

  $s.go.CloseMainWindow() | Out-Null
  Start-Sleep -Seconds 2
  Poll
  if ($null -eq $s.goCode -and (Get-Process -Id $s.go.Id -ErrorAction SilentlyContinue)) {
    Log 'OK   关窗后 game_starter.exe 仍在（隐藏陪跑），游戏未受影响'
  } else { Log 'FAIL 关窗把 game_starter.exe 或游戏一起带走了'; $fail = 1 }

  if (-not (Wait-GameEnd)) { Log 'FAIL 没等到游戏退出'; $fail = 1 }
  if (-not (Wait-For { $null -ne $s.goCode } 10)) { Log 'FAIL 游戏退出后 game_starter.exe 没跟着退出'; $fail = 1 }
  if ($s.goCode -eq $s.gameCode) { Log ('OK   主 GUI 收到的 game_starter.exe 退出码 == 游戏退出码 = ' + (Fmt-Code $s.gameCode)) }
  else { Log ('FAIL go=' + (Fmt-Code $s.goCode) + ' game=' + (Fmt-Code $s.gameCode)); $fail = 1 }

  Log '—— 场景 B：强杀 game_starter.exe → 游戏继续 → 主 GUI 仍按 pid 抓到游戏退出码 ——'
  if (-not $RealGame) { Use-FakeGame }
  Start-Launcher $true
  if (-not (Wait-For { $s.gamePid -ne 0 } 30)) { Log 'FAIL 没等到游戏'; $fail = 1 }
  $gamePid = $s.gamePid
  Get-Process -Id $s.go.Id -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
  Poll
  if (Get-Process -Id $gamePid -ErrorAction SilentlyContinue) { Log 'OK   杀掉 game_starter.exe 后游戏仍在跑' }
  else { Log 'FAIL 杀 game_starter.exe 把游戏也带走了'; $fail = 1 }
  if (-not (Wait-GameEnd)) { Log 'FAIL 没等到游戏退出'; $fail = 1 }
  if ($null -ne $s.gameCode) { Log ('OK   主 GUI 仍拿到游戏退出码 = ' + (Fmt-Code $s.gameCode) + '（game_starter.exe 的码是 ' + (Fmt-Code $s.goCode) + '，别拿它判异常）') }

  if (-not $RealGame) {
    if ([int]$s.gameCode -ne $FakeExitCode) { Log "FAIL 期望退出码 $FakeExitCode，实得 $(Fmt-Code $s.gameCode)"; $fail = 1 }
    else { Log "OK   退出码与假游戏约定一致（$FakeExitCode）" }
  }
  Stop-All
  Restore-Game
  if ($fail -eq 0) { Log 'PASS 托管链路两条场景全通过' } else { Log "FAIL 共 $fail 项不通过" }
  exit $fail
}

if ($Auto) { Run-Auto }

# ---------------- 手动 GUI ----------------
$form = New-Object System.Windows.Forms.Form
$form.Text = '主 GUI 模拟器 — game_starter.exe 托管链路'
$form.ClientSize = New-Object System.Drawing.Size(560, 420)
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true

$script:lbl = New-Object System.Windows.Forms.Label
$script:lbl.Location = New-Object System.Drawing.Point(12, 10)
$script:lbl.Size = New-Object System.Drawing.Size(536, 40)
$form.Controls.Add($script:lbl)

function Add-Button([string]$text, [int]$x, [int]$y, [int]$w, [scriptblock]$act) {
  $b = New-Object System.Windows.Forms.Button
  $b.Text = $text; $b.Location = New-Object System.Drawing.Point($x, $y); $b.Size = New-Object System.Drawing.Size($w, 34)
  $b.Add_Click($act)
  $form.Controls.Add($b)
  return $b
}

$fake = New-Object System.Windows.Forms.CheckBox
$fake.Text = '用假游戏（改 launcher.ini，退出时还原）'
$fake.Location = New-Object System.Drawing.Point(12, 56)
$fake.AutoSize = $true
$form.Controls.Add($fake)

Add-Button '启动 game_starter.exe（交互）' 12 88 170 { if ($fake.Checked) { Use-FakeGame }; Start-Launcher $false } | Out-Null
Add-Button '启动 game_starter.exe /go（零交互）' 192 88 190 { if ($fake.Checked) { Use-FakeGame }; Start-Launcher $true } | Out-Null
Add-Button '关启动器窗口（点 X）' 12 130 170 { if ($s.go) { $s.go.CloseMainWindow() | Out-Null; Log '已请求关窗（应转为隐藏陪跑）' } } | Out-Null
Add-Button '强杀 game_starter.exe' 192 130 90 { if ($s.go) { Get-Process -Id $s.go.Id | Stop-Process -Force; Log '已强杀 game_starter.exe（游戏应继续）' } } | Out-Null
Add-Button '结束游戏进程' 292 130 110 { if ($s.gamePid) { Get-Process -Id $s.gamePid -ErrorAction SilentlyContinue | Stop-Process -Force; Log '已结束游戏进程' } } | Out-Null
Add-Button '还原 launcher.ini' 412 130 136 { Restore-Game } | Out-Null

$script:box = New-Object System.Windows.Forms.TextBox
$script:box.Multiline = $true; $script:box.ReadOnly = $true
$script:box.ScrollBars = 'Vertical'
$script:box.Location = New-Object System.Drawing.Point(12, 176)
$script:box.Size = New-Object System.Drawing.Size(536, 232)
$script:box.Font = New-Object System.Drawing.Font('Consolas', 9)
$form.Controls.Add($script:box)

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 300
$timer.Add_Tick({ Poll; Show-Status })
$timer.Start()

$form.Add_FormClosed({ $timer.Stop(); Restore-Game })
Log '点「启动 game_starter.exe /go」→ 等游戏出现 → 点「关启动器窗口」→ 等游戏退出，看两个退出码是否一致'
if (-not $RealGame) { Log '勾上「用假游戏」可以不开真游戏就把链路走一遍（8 秒后退出码 7）' }
[System.Windows.Forms.Application]::Run($form)
