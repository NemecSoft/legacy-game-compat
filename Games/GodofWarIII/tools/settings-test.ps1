$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Game = 'D:\YunGame\F\GodofWarIIIRemastered'
$Test = Join-Path $Root 'test-settings'
if (Test-Path $Test) { Remove-Item $Test -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Test | Out-Null

Copy-Item (Join-Path $Root 'out\game_starter.exe') (Join-Path $Test 'game_starter.exe') -Force
Copy-Item (Join-Path $Game 'settings.json') (Join-Path $Test 'settings.json') -Force
# 归零自定义按键，保证每次测试都从“移植器默认”开始（否则幂等改写不会产生备份）
$s = (Get-Content (Join-Path $Test 'settings.json') -Raw) -replace '"key":\s*\{[^}]*\}', '"key": {}'
[IO.File]::WriteAllText((Join-Path $Test 'settings.json'), $s, (New-Object Text.UTF8Encoding $false))
Copy-Item (Join-Path $Game 'spartanport.ini') (Join-Path $Test 'spartanport.ini') -Force
[IO.File]::WriteAllText((Join-Path $Test 'launcher.ini'),
  "[Launcher]`r`nTitle=设置测试`r`nGameExe=C:\Windows\System32\cmd.exe`r`n" +
  "GameArgs=/c `"set SPARTAN>spartan_env.txt&exit /b 0`"`r`nIntro=测试`r`nCenterNote=测试`r`n",
  [Text.Encoding]::UTF8)

$fail = 0
function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) } else { Write-Output ('FAIL ' + $name); $script:fail++ }
}
function Run([string]$args2) {
  Remove-Item (Join-Path $Test 'spartan_env.txt') -Force -ErrorAction SilentlyContinue
  $a = @('/go')
  if ($args2.Length -gt 0) { $a = @($args2.Split(' ')) + '/go' }
  $code = (Start-Process -FilePath (Join-Path $Test 'game_starter.exe') -ArgumentList $a -WorkingDirectory $Test -Wait -PassThru).ExitCode
  Start-Sleep -Milliseconds 300
  return $code
}
function IniVal([string]$text, [string]$key) {
  $m = [regex]::Match($text, '(?m)^' + [regex]::Escape($key) + '=(.*)$')
  if ($m.Success) { return $m.Groups[1].Value.Trim() }
  return $null
}
function KeyOf([string]$json, [string]$action) {
  $m = [regex]::Match($json, '"' + $action + '":"([^"]*)"')
  if ($m.Success) { return $m.Groups[1].Value }
  return $null
}

# 1) 默认 = 纯键盘 + 自动检测画面
$modeBefore = IniVal (Get-Content (Join-Path $Test 'spartanport.ini') -Raw) 'display_mode'
$code = Run ''
Check ($code -eq 0) '退出码 0'
$env1 = Get-Content (Join-Path $Test 'spartan_env.txt') -Raw
$ini1 = Get-Content (Join-Path $Test 'spartanport.ini') -Raw
$json1 = Get-Content (Join-Path $Test 'settings.json') -Raw
Check ((KeyOf $json1 'square') -eq 'J') '□ 轻攻击 = J'
Check ((KeyOf $json1 'triangle') -eq 'K') '△ 重攻击 = K'
Check ((KeyOf $json1 'circle') -eq 'L') '○ 抓取/确认 = L'
Check ((KeyOf $json1 'r2') -eq 'T') 'R2 射击/投掷 = T'
Check ((KeyOf $json1 'dodge') -eq 'Left Shift') '闪避 = 左 Shift'
Check ((KeyOf $json1 'weapon_next') -eq 'None') '关掉滚轮换武器（用 1234 直选）'
Check ((KeyOf $json1 'l1') -eq $null) '防御 Q 与移植器默认相同，不写进设置'
Check ($env1 -match 'SPARTAN_KEY_MAP=square=J') '传给游戏的是键盘按键'
Check ($env1 -match 'l1=Q,r1=E,l2=R,r2=T') '肩键/扳机 = Q E R + 射击 T'
Check ($env1 -match 'dodge=Left Shift') '传给游戏的闪避是左 Shift'
Check ($env1 -notmatch 'touchpad_left=P') '拍照键解绑（不占常用键）'
Check ((IniVal $ini1 'display_mode') -eq $modeBefore) '自动检测不改动画面设置'
Check ($env1 -match 'SPARTAN_GAME_DIR=game') 'SPARTAN_GAME_DIR 传给游戏'
Check ($env1 -match 'SPARTAN_PATCHES=Bug Fix') 'SPARTAN_PATCHES 传给游戏'
Check ($env1 -match 'SPARTAN_UI_LANGUAGE=zh') 'SPARTAN_UI_LANGUAGE 传给游戏'
Check ($env1 -match 'SPARTAN_LANGUAGE=10') 'SPARTAN_LANGUAGE 传给游戏'
Check ($env1 -match 'SPARTAN_PATCHES_ONLY=1') 'SPARTAN_PATCHES_ONLY 传给游戏'
$keys = ([regex]::Matches($env1, '(?m)^SPARTAN_[A-Z0-9_]+=')).Count
Check ($keys -ge 22) ("传给游戏的 SPARTAN_ 变量数量=$keys")
Check (Test-Path (Join-Path $Test '_legacy_compat_backup\GodofWarIII\settings.json')) '改前备份 settings.json'

# 2) 画面异常时（固定桌面分辨率）
$code = Run '/desktop'
Check ($code -eq 0) '固定画面方案退出码 0'
$env2 = Get-Content (Join-Path $Test 'spartan_env.txt') -Raw
$ini2 = Get-Content (Join-Path $Test 'spartanport.ini') -Raw
$json2 = Get-Content (Join-Path $Test 'settings.json') -Raw
Check ($env2 -notmatch 'Mouse') '整套键位不传任何鼠标键给游戏'
Check ((IniVal $ini2 'display_mode') -eq 'borderless') '固定画面改为 borderless'
Check ((IniVal $ini2 'render_resolution') -eq 'native') '固定画面 render_resolution=native'
Check ((IniVal $ini2 'fps_limit') -eq '60') '固定画面 fps_limit=60'

# 3) 一个按键不能挂两个动作（曾出现 R2 与拍照都是 P）
function DupKeys([string]$envText) {
  $m = [regex]::Match($envText, '(?m)^SPARTAN_KEY_MAP=(.*)$')
  if (-not $m.Success) { return 'NO_KEY_MAP' }
  $used = @{}
  foreach ($pair in $m.Groups[1].Value.Split(',')) {
    $kv = $pair.Split('=', 2)
    if ($kv.Length -ne 2) { continue }
    $v = $kv[1].Trim()
    if ($v -eq 'None' -or $v -eq '') { continue }
    if ($used.ContainsKey($v)) { $used[$v] = $used[$v] + '/' + $kv[0] } else { $used[$v] = $kv[0] }
  }
  $bad = @()
  foreach ($e in $used.GetEnumerator()) { if ($e.Value.Contains('/')) { $bad += ($e.Key + ':' + $e.Value) } }
  return ($bad -join ' ')
}
$d1 = DupKeys $env1
Check ($d1 -eq '') ('键位表没有重复按键' + $(if ($d1 -eq '') { '' } else { '，冲突：' + $d1 }))

# 4) JSON 仍可被解析（不能让移植器读坏）
try { $null = $json2 | ConvertFrom-Json; Check $true 'settings.json 仍是合法 JSON' }
catch { Check $false ('settings.json 解析失败：' + $_.Exception.Message) }

# 5) 命令行修复画面
Remove-Item (Join-Path $Test 'spartanport.ini') -Force
Copy-Item (Join-Path $Game 'spartanport.ini') (Join-Path $Test 'spartanport.ini') -Force
$fixCode = (Start-Process -FilePath (Join-Path $Test 'game_starter.exe') -ArgumentList '/fixdisplay' -WorkingDirectory $Test -Wait -PassThru).ExitCode
Check ($fixCode -eq 0) '/fixdisplay 退出码 0'
Check ((IniVal (Get-Content (Join-Path $Test 'spartanport.ini') -Raw) 'display_mode') -eq 'borderless') '/fixdisplay 改为无边框'

Write-Output ('FAILURES=' + $fail)
if ($fail -eq 0) { Remove-Item $Test -Recurse -Force }
exit $fail
