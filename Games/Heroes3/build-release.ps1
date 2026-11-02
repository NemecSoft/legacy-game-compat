param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [string]$ReleaseDir = (Join-Path $PSScriptRoot 'release')
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Payload = Join-Path $ReleaseDir 'payload'
$gbk = [Text.Encoding]::GetEncoding(936)

$exe = Join-Path $Out 'game_starter.exe'
if (-not (Test-Path $exe)) { throw '缺少 out\game_starter.exe，请先运行 build.ps1' }
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Payload, (Join-Path $Payload '_presets'), (Join-Path $Payload '_cnc-ddraw'), (Join-Path $Payload '_tools\nircmd') | Out-Null

function Copy-Text([string]$name, [string]$destDir) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes((Join-Path $destDir $name), $gbk.GetBytes($text))
}

Copy-Item $exe (Join-Path $Payload 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $Payload 'launcher.ini') -Force
$presets = @('ddraw.ini', 'ddraw-43.ini', 'ddraw-window.ini', 'ddraw-640.ini')
foreach ($p in $presets) { Copy-Item (Join-Path $Src $p) (Join-Path $Payload $p) -Force }
foreach ($p in $presets) { Copy-Item (Join-Path $Src $p) (Join-Path $Payload ('_presets\' + $p)) -Force }
$ddraw = Join-Path $GameDir '_cnc-ddraw\ddraw.dll'
if (-not (Test-Path $ddraw)) { $ddraw = Join-Path $GameDir 'ddraw.dll' }
if (-not (Test-Path $ddraw)) { throw '缺少 ddraw.dll' }
Copy-Item $ddraw (Join-Path $Payload 'ddraw.dll') -Force
Copy-Item $ddraw (Join-Path $Payload '_cnc-ddraw\ddraw.dll') -Force
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $Payload '_cnc-ddraw\ddraw.ini') -Force
Copy-Item (Join-Path $Root 'tools\nircmd\nircmdc.exe') (Join-Path $Payload '_tools\nircmd\ircmdc.exe') -Force
foreach ($name in 'game_starter.bat', '兼容修复.bat') { Copy-Text $name $Payload }
foreach ($name in '一键应用.bat', '一键撤回.bat') { Copy-Text $name $ReleaseDir }

$manifest = @(
  'game_starter.exe',
  'game_starter.bat',
  'launcher.ini',
  '兼容修复.bat',
  'ddraw.dll',
  'ddraw.ini',
  'ddraw-43.ini',
  'ddraw-window.ini',
  'ddraw-640.ini',
  '_presets\ddraw.ini',
  '_presets\ddraw-43.ini',
  '_presets\ddraw-window.ini',
  '_presets\ddraw-640.ini',
  '_cnc-ddraw\ddraw.dll',
  '_cnc-ddraw\ddraw.ini',
  '_tools\nircmd\ircmdc.exe'
)
[IO.File]::WriteAllText((Join-Path $Payload 'manifest.txt'), (($manifest -join "`r`n") + "`r`n"), $gbk)
foreach ($rel in $manifest) {
  if (-not (Test-Path (Join-Path $Payload $rel))) { throw "交付包缺少 $rel" }
}
Write-Host "交付包已生成：$ReleaseDir"
Get-ChildItem $ReleaseDir -Recurse -File | Select-Object FullName, Length | Format-Table -AutoSize
