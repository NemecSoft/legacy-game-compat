$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $gameRoot 'release'
$apply = Join-Path $release '一键应用.bat'
$revert = Join-Path $release '一键撤回.bat'
$payload = Join-Path $release 'payload'
$root = Join-Path $gameRoot 'test-target'
$realGame = 'D:\YunGame\E\Heroes3'

function New-Target([string]$path) {
  if (Test-Path $path) { Remove-Item $path -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $path | Out-Null
  Set-Content -LiteralPath (Join-Path $path 'Heroes3.exe') -Value 'FAKE-HEROES3' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'ddraw.dll') -Value 'ORIGINAL-DDRAW' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'ddraw.ini') -Value 'ORIGINAL-DDRAW-INI' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'go.bat') -Value 'ORIGINAL-GO-BAT' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'game_starter.bat') -Value 'ORIGINAL-STARTER' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'launcher.ini') -Value 'ORIGINAL-LAUNCHER-INI' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path '运行日志.txt') -Value 'RUNTIME-LOG' -Encoding ascii
}

function Run-Bat([string]$bat, [string]$target) {
  $text = & cmd.exe /c "`"$bat`" `"$target`" < nul" 2>&1
  return [pscustomobject]@{ Code = $LASTEXITCODE; Text = ($text -join "`n") }
}

function Read-Ascii([string]$path) {
  if (-not (Test-Path $path)) { return $null }
  return (Get-Content -LiteralPath $path -Raw -Encoding ascii).Trim()
}

New-Target $root
$r1 = Run-Bat $apply $root
Write-Output ("apply exit=" + $r1.Code)
if ($r1.Code -ne 0) { Write-Output $r1.Text; throw 'apply failed' }
$backup = Join-Path $root '_legacy_compat_backup\Heroes3'
$checks = @(
  @{ Name = 'launcher copied'; Ok = (Test-Path (Join-Path $root 'game_starter.exe')) },
  @{ Name = 'original ddraw backed up'; Ok = ((Read-Ascii (Join-Path $backup 'ddraw.dll')) -eq 'ORIGINAL-DDRAW') },
  @{ Name = 'original ini backed up'; Ok = ((Read-Ascii (Join-Path $backup 'ddraw.ini')) -eq 'ORIGINAL-DDRAW-INI') },
  @{ Name = 'presets copied'; Ok = ((Test-Path (Join-Path $root '_presets\ddraw-43.ini')) -and (Test-Path (Join-Path $root '_presets\ddraw-window.ini')) -and (Test-Path (Join-Path $root '_presets\ddraw-640.ini'))) },
  @{ Name = 'cnc-ddraw layer copied'; Ok = (Test-Path (Join-Path $root '_cnc-ddraw\ddraw.dll')) },
  @{ Name = 'go.bat untouched'; Ok = ((Read-Ascii (Join-Path $root 'go.bat')) -eq 'ORIGINAL-GO-BAT') },
  @{ Name = 'manifest/record'; Ok = ((Test-Path (Join-Path $backup 'manifest.txt')) -and (Test-Path (Join-Path $backup '交付记录.txt'))) }
)
foreach ($c in $checks) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks.Ok -contains $false) { throw 'apply checks failed' }
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-apply-record.txt') -Force

$r2 = Run-Bat $revert $root
Write-Output ("revert exit=" + $r2.Code)
if ($r2.Code -ne 0) { Write-Output $r2.Text; throw 'revert failed' }
$checks2 = @(
  @{ Name = 'original ddraw restored'; Ok = ((Read-Ascii (Join-Path $root 'ddraw.dll')) -eq 'ORIGINAL-DDRAW') },
  @{ Name = 'original ini restored'; Ok = ((Read-Ascii (Join-Path $root 'ddraw.ini')) -eq 'ORIGINAL-DDRAW-INI') },
  @{ Name = 'launcher removed'; Ok = (-not (Test-Path (Join-Path $root 'game_starter.exe'))) },
  @{ Name = 'compat removed'; Ok = (-not (Test-Path (Join-Path $root '兼容修复.bat'))) },
  @{ Name = 'presets removed'; Ok = (-not (Test-Path (Join-Path $root '_presets'))) },
  @{ Name = 'cnc backup removed'; Ok = (-not (Test-Path (Join-Path $root '_cnc-ddraw'))) },
  @{ Name = 'runtime log removed'; Ok = (-not (Test-Path (Join-Path $root '运行日志.txt'))) },
  @{ Name = 'go.bat untouched'; Ok = ((Read-Ascii (Join-Path $root 'go.bat')) -eq 'ORIGINAL-GO-BAT') },
  @{ Name = 'record kept'; Ok = (Test-Path (Join-Path $backup '交付记录.txt')) }
)
foreach ($c in $checks2) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks2.Ok -contains $false) { throw 'revert checks failed' }
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-revert-record.txt') -Force

# 测试时兼容修复会把 AppPath 指向假目标，恢复真实游戏路径
if (Test-Path (Join-Path $realGame '兼容修复.bat')) {
  & cmd.exe /c "`"$(Join-Path $realGame '兼容修复.bat')`" /quiet" | Out-Null
}
Write-Output 'ALL DELIVERY TESTS PASSED'
