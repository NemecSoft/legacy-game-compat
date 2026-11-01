$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $gameRoot 'release'
$apply = Join-Path $release '一键应用.bat'
$revert = Join-Path $release '一键撤回.bat'
$payload = Join-Path $release 'payload'
$root = Join-Path $gameRoot 'test-target'
$target2 = Join-Path $gameRoot 'test-target-modified'
$key = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'

function New-Target([string]$path) {
  if (Test-Path $path) { Remove-Item $path -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $path | Out-Null
  Set-Content -LiteralPath (Join-Path $path 'Future Soldier DX11.exe') -Value 'FAKE-DX11' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'Future Soldier.exe') -Value 'FAKE-WRAPPER' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'game_starter.bat') -Value 'ORIGINAL-BAT' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path 'launcher.ini') -Value 'ORIGINAL-INI' -Encoding ascii
  Set-Content -LiteralPath (Join-Path $path '修改器说明.txt') -Value 'ORIGINAL-TRAINER-NOTE' -Encoding ascii
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
# 模拟 go.bat 留下的 XP 兼容性项，应用脚本应通过 兼容修复.bat /quiet 清掉
$xpName = Join-Path $root 'Future Soldier DX11.exe'
Set-ItemProperty -Path $key -Name $xpName -Value '~ DWM8And16BitMitigation WINXPSP3 RUNASADMIN'
$r1 = Run-Bat $apply $root
Write-Output ("apply exit=" + $r1.Code)
if ($r1.Code -ne 0) { Write-Output $r1.Text; throw 'apply failed' }
$backup = Join-Path $root '_legacy_compat_backup\FutureSoldier'
$checks = @(
  @{ Name = 'launcher copied'; Ok = (Test-Path (Join-Path $root 'game_starter.exe')) },
  @{ Name = 'original bat backed up'; Ok = ((Read-Ascii (Join-Path $backup 'game_starter.bat')) -eq 'ORIGINAL-BAT') },
  @{ Name = 'trainer note copied'; Ok = ((Read-Ascii (Join-Path $root '修改器说明.txt')) -ne 'ORIGINAL-TRAINER-NOTE') },
  @{ Name = 'manifest/record'; Ok = ((Test-Path (Join-Path $backup 'manifest.txt')) -and (Test-Path (Join-Path $backup '交付记录.txt'))) },
  @{ Name = 'XP layer removed'; Ok = (-not (Get-ItemProperty -Path $key -Name $xpName -ErrorAction SilentlyContinue)) },
  @{ Name = 'compat record'; Ok = (Test-Path (Join-Path $backup '兼容修复记录.txt')) }
)
foreach ($c in $checks) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks.Ok -contains $false) { throw 'apply checks failed' }
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-apply-record.txt') -Force

$r2 = Run-Bat $revert $root
Write-Output ("revert exit=" + $r2.Code)
if ($r2.Code -ne 0) { Write-Output $r2.Text; throw 'revert failed' }
$checks2 = @(
  @{ Name = 'original bat restored'; Ok = ((Read-Ascii (Join-Path $root 'game_starter.bat')) -eq 'ORIGINAL-BAT') },
  @{ Name = 'original ini restored'; Ok = ((Read-Ascii (Join-Path $root 'launcher.ini')) -eq 'ORIGINAL-INI') },
  @{ Name = 'original note restored'; Ok = ((Read-Ascii (Join-Path $root '修改器说明.txt')) -eq 'ORIGINAL-TRAINER-NOTE') },
  @{ Name = 'launcher removed'; Ok = (-not (Test-Path (Join-Path $root 'game_starter.exe'))) },
  @{ Name = 'compat bat removed'; Ok = (-not (Test-Path (Join-Path $root '兼容修复.bat'))) },
  @{ Name = 'tools removed'; Ok = (-not (Test-Path (Join-Path $root '_tools'))) },
  @{ Name = 'runtime log removed'; Ok = (-not (Test-Path (Join-Path $root '运行日志.txt'))) },
  @{ Name = 'game files untouched'; Ok = ((Read-Ascii (Join-Path $root 'Future Soldier DX11.exe')) -eq 'FAKE-DX11') },
  @{ Name = 'record kept'; Ok = (Test-Path (Join-Path $backup '交付记录.txt')) }
)
foreach ($c in $checks2) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks2.Ok -contains $false) { throw 'revert checks failed' }
Copy-Item (Join-Path $backup '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-revert-record.txt') -Force

New-Target $target2
$r3 = Run-Bat $apply $target2
if ($r3.Code -ne 0) { Write-Output $r3.Text; throw 'apply2 failed' }
Set-Content -LiteralPath (Join-Path $target2 'game_starter.bat') -Value 'PLAYER-MODIFIED-BAT' -Encoding ascii
$r4 = Run-Bat $revert $target2
Write-Output ("revert-modified exit=" + $r4.Code)
$backup2 = Join-Path $target2 '_legacy_compat_backup\FutureSoldier'
$record2 = [IO.File]::ReadAllText((Join-Path $backup2 '交付记录.txt'), [Text.Encoding]::GetEncoding(936))
$checks3 = @(
  @{ Name = 'modified bat kept'; Ok = ((Read-Ascii (Join-Path $target2 'game_starter.bat')) -eq 'PLAYER-MODIFIED-BAT') },
  @{ Name = 'skip recorded'; Ok = ($record2 -match '跳过恢复') },
  @{ Name = 'backup kept'; Ok = (Test-Path (Join-Path $backup2 'game_starter.bat')) }
)
foreach ($c in $checks3) { Write-Output ("{0}: {1}" -f $c.Name, $(if ($c.Ok) { 'PASS' } else { 'FAIL' })) }
if ($checks3.Ok -contains $false) { throw 'modified-file revert checks failed' }
Copy-Item (Join-Path $backup2 '交付记录.txt') (Join-Path $gameRoot 'evidence\delivery-modified-record.txt') -Force

Remove-ItemProperty -Path $key -Name $xpName -ErrorAction SilentlyContinue
Write-Output 'ALL DELIVERY TESTS PASSED'
