$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Game = 'D:\YunGame\F\KingdomHeroes8'
$Repo = (Resolve-Path (Join-Path $Root '..\..')).Path
$fail = 0

function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) } else { Write-Output ('FAIL ' + $name); $script:fail++ }
}
function HashOf([string]$p) { if (Test-Path $p) { (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant() } else { '' } }

# 1) 交付完整性
foreach ($f in 'game_starter.exe', 'game_starter.bat', 'launcher.ini', 'game_starter.sha256') {
  Check (Test-Path (Join-Path $Game $f)) ("游戏目录有 $f")
}
Check (Test-Path (Join-Path $Game '_tools\nircmd\nircmdc.exe')) 'bat 需要的 _tools\nircmd\nircmdc.exe 在位（名字逐字一致）'
$exe = Join-Path $Game 'game_starter.exe'
Check ((Get-Item $exe).Length -le 60KB) ("exe 体积 " + [int]((Get-Item $exe).Length / 1KB) + "KB <= 60KB")
$side = (Get-Content (Join-Path $Game 'game_starter.sha256') -First 1).Split(' ')[0]
Check ($side -eq (HashOf $exe)) 'game_starter.sha256 与 exe 一致'
Check ((HashOf (Join-Path $Repo 'Games\KingdomHeroes8\out\game_starter.exe')) -eq (HashOf $exe)) 'out 与游戏目录是同一个 exe'
$bytes = [IO.File]::ReadAllBytes((Join-Path $Game 'game_starter.bat'))
Check (([regex]::Matches([Text.Encoding]::ASCII.GetString($bytes), '\r\n').Count) -gt 10) 'bat 是 CRLF'
Check ($bytes[0] -eq 0x40 -and $bytes[1] -eq 0x65) 'bat 无 BOM（以 @ 开头）'
$gbk = [Text.Encoding]::GetEncoding(936).GetString($bytes)
Check ($gbk.Contains('三国群英传')) 'bat 用 GBK 能正确解出中文'
$src = Get-ChildItem $Game -Recurse -Include *.cs, *.ps1 -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notlike '*_legacy_compat_backup*' }
Check ($null -eq $src -or @($src).Count -eq 0) '游戏目录没有源码文件'

# 2) 包内关键文件没被我们改动
$keep = Join-Path $Repo 'Games\KingdomHeroes8\backup\untouched.sha256'
if (Test-Path $keep) {
  $lines = @{}
  foreach ($l in (Get-Content $keep)) {
    $parts = $l -split '\s+'
    if ($parts.Count -ge 2) { $lines[$parts[0]] = $parts[1] }
  }
  foreach ($k in $lines.Keys) {
    Check ((HashOf (Join-Path $Game $k)) -eq $lines[$k]) ("包内文件未被改动：$k")
  }
} else { Write-Output 'SKIP backup\untouched.sha256 还没生成（跑一次 build.ps1）' }

# 3) 缺离线启动器 -> 退出码 2
$Sandbox = Join-Path $Root 'test-sandbox'
if (Test-Path $Sandbox) { try { Remove-Item $Sandbox -Recurse -Force } catch { Write-Output ('  清理旧沙箱失败：' + $_.Exception.Message); exit 9 } }
New-Item -ItemType Directory -Force -Path $Sandbox | Out-Null
Copy-Item $exe (Join-Path $Sandbox 'game_starter.exe') -Force
$c2 = (Start-Process -FilePath (Join-Path $Sandbox 'game_starter.exe') -ArgumentList '/go' `
  -WorkingDirectory $Sandbox -Wait -PassThru).ExitCode
Check ($c2 -eq 2) "缺 SG8Launcher.exe 时退出码 2（实际 $c2）"

# 4) 等不到原版窗口 -> 退出码 4（假启动器：开 3 秒就退，不建窗口）
# 假离线启动器用 ping（无参数会立刻退出、永不建窗口），不会留下挂住的进程
Copy-Item "$env:WINDIR\System32\ping.exe" (Join-Path $Sandbox 'SG8Launcher.exe') -Force
$ini = "[Launcher]`r`nOfflineLauncher=SG8Launcher.exe`r`nGameName=SG8`r`nStartButtonText=启动游戏`r`nCoverPad=6`r`n"
[IO.File]::WriteAllText((Join-Path $Sandbox 'launcher.ini'), $ini, (New-Object Text.UTF8Encoding $false))
$sw = [Diagnostics.Stopwatch]::StartNew()
$c4 = (Start-Process -FilePath (Join-Path $Sandbox 'game_starter.exe') -ArgumentList '/go' `
  -WorkingDirectory $Sandbox -Wait -PassThru).ExitCode
$sw.Stop()
Check ($c4 -eq 4) "等不到原版窗口时退出码 4（实际 $c4，用时 " + [int]$sw.Elapsed.TotalSeconds + 's）'
Check ($sw.Elapsed.TotalSeconds -lt 60) '失败路径不会长时间挂住（<60s 返回）'
Remove-Item $Sandbox -Recurse -Force

# 5) 交付包应用 / 撤回
$Rel = Join-Path $Root 'release'
if (Test-Path (Join-Path $Rel '一键应用.bat')) {
  $Sandbox2 = Join-Path $Root 'test-release'
  if (Test-Path $Sandbox2) { Remove-Item $Sandbox2 -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $Sandbox2 | Out-Null
  Set-Content -Path (Join-Path $Sandbox2 'SG8Launcher.exe') -Value 'stub' -Encoding ASCII
  New-Item -ItemType Directory -Force -Path (Join-Path $Sandbox2 'GAME') | Out-Null
  Set-Content -Path (Join-Path $Sandbox2 'GAME\SG8.exe') -Value 'stub' -Encoding ASCII
  $a = (Start-Process -FilePath (Join-Path $Rel '一键应用.bat') -ArgumentList "`"$Sandbox2`"" `
    -WorkingDirectory $Rel -Wait -PassThru).ExitCode
  Check ($a -eq 0) "一键应用退出码 0（实际 $a）"
  Check (Test-Path (Join-Path $Sandbox2 'game_starter.exe')) '应用后装上 game_starter.exe'
  Check (Test-Path (Join-Path $Sandbox2 'game_starter.bat')) '应用后装上 game_starter.bat'
  Check (Test-Path (Join-Path $Sandbox2 '_tools\nircmd\nircmdc.exe')) '应用后装上 _tools\nircmd\nircmdc.exe'
  Check (Test-Path (Join-Path $Sandbox2 '_legacy_compat_backup\KingdomHeroes8\交付记录.txt')) '写了交付记录'
  $b = (Start-Process -FilePath (Join-Path $Rel '一键撤回.bat') -ArgumentList "`"$Sandbox2`"" `
    -WorkingDirectory $Rel -Wait -PassThru).ExitCode
  Check ($b -eq 0) "一键撤回退出码 0（实际 $b）"
  Check (-not (Test-Path (Join-Path $Sandbox2 'game_starter.exe'))) '撤回删掉 game_starter.exe'
  Check (Test-Path (Join-Path $Sandbox2 'SG8Launcher.exe')) '撤回不碰包内文件（SG8Launcher.exe 还在）'
  Check (Test-Path (Join-Path $Sandbox2 'GAME\SG8.exe')) '撤回不碰游戏文件（GAME\SG8.exe 还在）'
  Remove-Item $Sandbox2 -Recurse -Force
} else { Write-Output 'SKIP 交付包测试（先跑 build-release.ps1）' }

Write-Output ('FAILURES=' + $fail)
exit $fail
