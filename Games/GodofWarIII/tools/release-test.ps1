$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Rel = Join-Path $Root 'release'
$Game = 'D:\YunGame\F\GodofWarIIIRemastered'
$Test = Join-Path $Root 'test-release'
if (Test-Path $Test) { Remove-Item $Test -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Test | Out-Null

# 造一个假游戏目录：主程序 + 玩家自己的设置
Set-Content -Path (Join-Path $Test 'SpartanPort.exe') -Value 'fake-exe' -NoNewline
Copy-Item (Join-Path $Game 'settings.json') (Join-Path $Test 'settings.json')
Copy-Item (Join-Path $Game 'spartanport.ini') (Join-Path $Test 'spartanport.ini')
$origJson = Get-Content (Join-Path $Test 'settings.json') -Raw
$origIni = Get-Content (Join-Path $Test 'spartanport.ini') -Raw
Copy-Item -Recurse $Rel (Join-Path $Test 'package')

$fail = 0
function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) } else { Write-Output ('FAIL ' + $name); $script:fail++ }
}
function RunBat([string]$bat, [string]$arg) {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'cmd.exe'
  $psi.Arguments = '/c ""' + $bat + '" "' + $arg + '""'
  $psi.WorkingDirectory = (Split-Path $bat)
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $o = $p.StandardOutput.ReadToEnd()
  $p.WaitForExit(120000) | Out-Null
  return @{ code = $p.ExitCode; out = $o }
}

$r = RunBat (Join-Path $Test 'package\一键应用.bat') $Test
Check ($r.code -eq 0) ('一键应用退出码 0（实际 ' + $r.code + '）')
Check (Test-Path (Join-Path $Test 'game_starter.exe')) '应用后落地 game_starter.exe'
Check (Test-Path (Join-Path $Test 'game_starter.bat')) '应用后落地 game_starter.bat'
Check (Test-Path (Join-Path $Test 'launcher.ini')) '应用后落地 launcher.ini'
Check (Test-Path (Join-Path $Test '_tools\nircmd\nircmdc.exe')) '应用后落地 nircmdc.exe（名字与 bat 一致）'
Check (Test-Path (Join-Path $Test '_legacy_compat_backup\GodofWarIII\settings.json')) '应用前备份 settings.json'
Check (Test-Path (Join-Path $Test '_legacy_compat_backup\GodofWarIII\spartanport.ini')) '应用前备份 spartanport.ini'
Check (Test-Path (Join-Path $Test '_legacy_compat_backup\GodofWarIII\交付记录.txt')) '写了交付记录'

# 模拟启动器改设置，再撤回
(Get-Content (Join-Path $Test 'settings.json') -Raw).Replace('"key": {', '"key": {"square":"J",') |
  Set-Content (Join-Path $Test 'settings.json') -NoNewline
(Get-Content (Join-Path $Test 'spartanport.ini') -Raw).Replace('display_mode=windowed', 'display_mode=borderless') |
  Set-Content (Join-Path $Test 'spartanport.ini') -NoNewline

$r2 = RunBat (Join-Path $Test 'package\一键撤回.bat') $Test
Check ($r2.code -eq 0) ('一键撤回退出码 0（实际 ' + $r2.code + '）')
Check (-not (Test-Path (Join-Path $Test 'game_starter.exe'))) '撤回删除 game_starter.exe'
Check (-not (Test-Path (Join-Path $Test 'game_starter.bat'))) '撤回删除 game_starter.bat'
Check ((Get-Content (Join-Path $Test 'settings.json') -Raw) -eq $origJson) '撤回恢复原 settings.json'
Check ((Get-Content (Join-Path $Test 'spartanport.ini') -Raw) -eq $origIni) '撤回恢复原 spartanport.ini'
Check (Test-Path (Join-Path $Test 'SpartanPort.exe')) '撤回不碰游戏主程序'

Write-Output ('FAILURES=' + $fail)
if ($fail -eq 0) { Remove-Item $Test -Recurse -Force }
exit $fail
