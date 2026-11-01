$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $gameRoot 'release'
$apply = Join-Path $release '一键应用.bat'
$revert = Join-Path $release '一键撤回.bat'
$payload = Join-Path $release 'payload'
$root = Join-Path $gameRoot 'test-target'
$target2 = Join-Path $gameRoot 'test-target-modified'

function New-Target([string]$path) {
  if (Test-Path $path) { Remove-Item $path -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $path | Out-Null
  Set-Content -LiteralPath (Join-Path $path 'Saboteur.exe') -Value 'FAKE-GAME-EXE' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'd3d9.dll') -Value 'ORIGINAL-RESHADE-DLL' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'dxvk.conf') -Value 'ORIGINAL-DXVK-CONF' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'game_starter.bat') -Value 'ORIGINAL-USER-BAT' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'ReShade.ini') -Value 'ORIGINAL-RESHADE-INI' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path '运行日志.txt') -Value 'RUNTIME-LOG' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'Saboteur_d3d9.log') -Value 'DXVK-LOG' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'Saboteur.dxvk-cache') -Value 'DXVK-CACHE' -Encoding ascii
}

function Run-Bat([string]$bat, [string]$target) {
  $text = & cmd.exe /c "`"$bat`" `"$target`" < nul" 2>&1
  return [pscustomobject]@{ Code = $LASTEXITCODE; Text = ($text -join "`n") }
}

function Read-Text([string]$path) {
  if (-not (Test-Path $path)) { return $null }
  return (Get-Content -LiteralPath $path -Raw -Encoding ascii).Trim()
}

New-Target $root
$r1 = Run-Bat $apply $root
Write-Output ("apply1 exit=" + $r1.Code)
if ($r1.Code -ne 0) { Write-Output $r1.Text; throw 'apply failed' }
$backup = Join-Path $root '_legacy_compat_backup\TheSaboteur'
$checks = @(
  @{ Name = 'd3d9 replaced'; Ok = ((Get-FileHash (Join-Path $root 'd3d9.dll')).Hash -eq (Get-FileHash (Join-Path $payload 'd3d9.dll')).Hash) },
  @{ Name = 'original dll backed up'; Ok = ((Read-Text (Join-Path $backup 'd3d9.dll')) -eq 'ORIGINAL-RESHADE-DLL') },
  @{ Name = 'original bat backed up'; Ok = ((Read-Text (Join-Path $backup 'game_starter.bat')) -eq 'ORIGINAL-USER-BAT') },
  @{ Name = 'launcher exe copied'; Ok = (Test-Path (Join-Path $root 'game_starter.exe')) },
  @{ Name = 'recovery bats copied'; Ok = ((Test-Path (Join-Path $root '兼容修复.bat')) -and (Test-Path (Join-Path $root '恢复原版.bat'))) },
  @{ Name = 'manifest in backup'; Ok = (Test-Path (Join-Path $backup 'manifest.txt')) },
  @{ Name = 'record written'; Ok = (Test-Path (Join-Path $backup '交付记录.txt')) },
  @{ Name = 'ReShade.ini untouched'; Ok = ((Read-Text (Join-Path $root 'ReShade.ini')) -eq 'ORIGINAL-RESHADE-INI') }
)
foreach ($c in $checks) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks.Ok -contains $false) { throw 'apply checks failed' }

$applyRecord = [IO.File]::ReadAllText((Join-Path $backup '交付记录.txt'), [Text.Encoding]::GetEncoding(936))
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-apply-record.txt') -Force

$r2 = Run-Bat $revert $root
Write-Output ("revert1 exit=" + $r2.Code)
if ($r2.Code -ne 0) { Write-Output $r2.Text; throw 'revert failed' }
$checks2 = @(
  @{ Name = 'original dll restored'; Ok = ((Read-Text (Join-Path $root 'd3d9.dll')) -eq 'ORIGINAL-RESHADE-DLL') },
  @{ Name = 'original conf restored'; Ok = ((Read-Text (Join-Path $root 'dxvk.conf')) -eq 'ORIGINAL-DXVK-CONF') },
  @{ Name = 'original bat restored'; Ok = ((Read-Text (Join-Path $root 'game_starter.bat')) -eq 'ORIGINAL-USER-BAT') },
  @{ Name = 'launcher exe removed'; Ok = (-not (Test-Path (Join-Path $root 'game_starter.exe'))) },
  @{ Name = 'launcher ini removed'; Ok = (-not (Test-Path (Join-Path $root 'launcher.ini'))) },
  @{ Name = 'recovery bats removed'; Ok = ((-not (Test-Path (Join-Path $root '兼容修复.bat'))) -and (-not (Test-Path (Join-Path $root '恢复原版.bat')))) },
  @{ Name = '_dxvk removed'; Ok = (-not (Test-Path (Join-Path $root '_dxvk'))) },
  @{ Name = '_tools removed'; Ok = (-not (Test-Path (Join-Path $root '_tools'))) },
  @{ Name = 'runtime log removed'; Ok = (-not (Test-Path (Join-Path $root '运行日志.txt'))) },
  @{ Name = 'DXVK log/cache removed'; Ok = ((-not (Test-Path (Join-Path $root 'Saboteur_d3d9.log'))) -and (-not (Test-Path (Join-Path $root 'Saboteur.dxvk-cache')))) },
  @{ Name = 'ReShade.ini untouched'; Ok = ((Read-Text (Join-Path $root 'ReShade.ini')) -eq 'ORIGINAL-RESHADE-INI') },
  @{ Name = 'backup/record kept'; Ok = (Test-Path (Join-Path $backup '交付记录.txt')) }
)
foreach ($c in $checks2) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks2.Ok -contains $false) { throw 'revert checks failed' }
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-revert-record.txt') -Force

New-Target $target2
$r3 = Run-Bat $apply $target2
if ($r3.Code -ne 0) { Write-Output $r3.Text; throw 'apply2 failed' }
Set-Content -LiteralPath (Join-Path $target2 'd3d9.dll') -Value 'PLAYER-MODIFIED-DLL' -Encoding ascii
Set-Content -LiteralPath (Join-Path $target2 'game_starter.bat') -Value 'PLAYER-MODIFIED-BAT' -Encoding ascii
$r4 = Run-Bat $revert $target2
Write-Output ("revert-modified exit=" + $r4.Code)
$backup2 = Join-Path $target2 '_legacy_compat_backup\TheSaboteur'
$record2 = [IO.File]::ReadAllText((Join-Path $backup2 '交付记录.txt'), [Text.Encoding]::GetEncoding(936))
$checks3 = @(
  @{ Name = 'modified dll kept'; Ok = ((Read-Text (Join-Path $target2 'd3d9.dll')) -eq 'PLAYER-MODIFIED-DLL') },
  @{ Name = 'modified bat kept'; Ok = ((Read-Text (Join-Path $target2 'game_starter.bat')) -eq 'PLAYER-MODIFIED-BAT') },
  @{ Name = 'skip recorded'; Ok = ($record2 -match '跳过恢复') },
  @{ Name = 'backup kept'; Ok = (Test-Path (Join-Path $backup2 'd3d9.dll')) }
)
foreach ($c in $checks3) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks3.Ok -contains $false) { throw 'modified-file revert checks failed' }
Copy-Item (Join-Path $backup2 '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-modified-record.txt') -Force

Write-Output 'ALL DELIVERY TESTS PASSED'
