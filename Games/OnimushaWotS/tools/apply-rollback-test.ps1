$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Release = Join-Path $GameRoot 'release'
$Test = Join-Path $GameRoot 'test-delivery'
if (Test-Path $Test) { Remove-Item $Test -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Test | Out-Null

[IO.File]::WriteAllText((Join-Path $Test 'OnimushaWotS.exe'), 'dummy-game-exe', [Text.Encoding]::ASCII)
[IO.File]::WriteAllText((Join-Path $Test 'config.ini'), "[Graphics]`nWindowMode=FullScreen`nNormalWindowResolution=(1280,720)`n", [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $Test 'launcher.ini'), "old-launcher`n", [Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $Test 'game_starter.exe'), 'old-starter', [Text.Encoding]::ASCII)

function Invoke-Bat([string]$bat, [string]$target) {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $env:ComSpec
  $psi.Arguments = '/c call "' + $bat + '" "' + $target + '"'
  $psi.WorkingDirectory = Split-Path $bat -Parent
  $psi.UseShellExecute = $false
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.StandardOutputEncoding = [Text.Encoding]::GetEncoding(936)
  $psi.StandardErrorEncoding = [Text.Encoding]::GetEncoding(936)
  $p = [System.Diagnostics.Process]::Start($psi)
  $p.StandardInput.WriteLine('')
  $p.StandardInput.Close()
  $out = $p.StandardOutput.ReadToEnd()
  $err = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($out) { Write-Output $out }
  if ($err) { Write-Output $err }
  Write-Output ('EXIT=' + $p.ExitCode)
}

Write-Output '===== APPLY ====='
Invoke-Bat (Join-Path $Release '一键应用.bat') $Test

$fail = 0
function Check([bool]$ok, [string]$name) {
  if ($ok) { Write-Output ('PASS ' + $name) }
  else { Write-Output ('FAIL ' + $name); $script:fail++ }
}
$back = Join-Path $Test '_legacy_compat_backup\OnimushaWotS'
Check (Test-Path (Join-Path $Test 'game_starter.exe')) 'game_starter.exe copied'
Check ((Get-Item (Join-Path $Test 'game_starter.exe')).Length -gt 10000) 'game_starter.exe is real launcher'
Check (-not (Test-Path (Join-Path $Test 'config.ini'))) 'config.ini deleted'
Check (Test-Path (Join-Path $back 'config.ini')) 'config.ini backed up'
Check ((Get-Content (Join-Path $back 'config.ini') -Raw) -match 'WindowMode=FullScreen') 'backup keeps original config'
Check ((Get-Content (Join-Path $back 'launcher.ini') -Raw) -match 'old-launcher') 'launcher.ini backed up'
Check (Test-Path (Join-Path $back '交付记录.txt')) 'delivery record written'
Check (Test-Path (Join-Path $Test '_tools\nircmd\ircmdc.exe')) 'nircmd delivered'

Write-Output '===== ROLLBACK ====='
Invoke-Bat (Join-Path $Release '一键撤回.bat') $Test

Check ((Test-Path (Join-Path $Test 'config.ini')) -and ((Get-Content (Join-Path $Test 'config.ini') -Raw) -match 'WindowMode=FullScreen')) 'config.ini restored'
Check ((Get-Content (Join-Path $Test 'launcher.ini') -Raw) -match 'old-launcher') 'launcher.ini restored'
Check ((Get-Content (Join-Path $Test 'game_starter.exe') -Raw) -match 'old-starter') 'old game_starter.exe restored'
Check (-not (Test-Path (Join-Path $Test 'game_starter.bat'))) 'delivered game_starter.bat removed'
Check (-not (Test-Path (Join-Path $Test 'game_starter.sha256'))) 'delivered sha256 removed'
Check (-not (Test-Path (Join-Path $Test '_tools\nircmd\ircmdc.exe'))) 'delivered nircmd removed'

Write-Output ('FAILURES=' + $fail)
exit $fail
