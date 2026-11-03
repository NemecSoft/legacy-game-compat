$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Test = Join-Path $Root 'test-modes'
if (Test-Path $Test) { Remove-Item $Test -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Test | Out-Null
Copy-Item (Join-Path $Root 'out\game_starter.exe') (Join-Path $Test 'game_starter.exe') -Force
[IO.File]::WriteAllText((Join-Path $Test 'launcher.ini'),
  "[Launcher]`r`nTitle=模式测试`r`nGameExe=C:\Windows\System32\cmd.exe`r`nGameArgs=/c exit 0`r`nIntro=测试`r`nCenterNote=测试`r`n",
  [Text.Encoding]::UTF8)
$marker = "[Graphics]`r`nDisplayType=Test`r`nNormalWindowResolution=(1280,720)`r`nWindowMode=FullScreen`r`n[RenderConfig]`r`nFullScreenMode=true`r`nNormalWindowResolution=(1280.000000,720.000000)`r`nWindowMode=FullScreen`r`n"
$config = Join-Path $Test 'config.ini'
$fail = 0
function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) } else { Write-Output ('FAIL ' + $name); $script:fail++ }
}
function Run-Mode([string]$mode) {
  $args = if ($mode.Length -gt 0) { @($mode, '/go') } else { @('/go') }
  return (Start-Process -FilePath (Join-Path $Test 'game_starter.exe') -ArgumentList $args -WorkingDirectory $Test -Wait -PassThru).ExitCode
}

[IO.File]::WriteAllText($config, $marker, [Text.Encoding]::UTF8)
$before = (Get-FileHash $config -Algorithm SHA256).Hash
$code = Run-Mode ''
Check ($code -eq 0) 'auto exit code 0'
Check ((Get-FileHash $config -Algorithm SHA256).Hash -eq $before) 'auto leaves config.ini untouched'

[IO.File]::WriteAllText($config, $marker, [Text.Encoding]::UTF8)
$code = Run-Mode '/desktop'
Check ($code -eq 0) 'desktop exit code 0'
$text = Get-Content $config -Raw
Check ($text -match 'WindowMode=Borderless') 'desktop sets Borderless'
Check ($text -match 'FullScreenMode=false') 'desktop clears FullScreenMode'
Check ($text -match 'NormalWindowResolution=\(1920,1080\)') 'desktop sets desktop resolution'
Check ($text -match 'DisplayType=Auto') 'desktop sets DisplayType Auto'

Write-Output ('FAILURES=' + $fail)
if ($fail -eq 0) { Remove-Item $Test -Recurse -Force }
exit $fail