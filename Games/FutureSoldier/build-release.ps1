param(
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
New-Item -ItemType Directory -Force -Path $Payload, (Join-Path $Payload '_dx9_compat'), (Join-Path $Payload '_tools\nircmd') | Out-Null

function Copy-Text([string]$name, [string]$destDir) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes((Join-Path $destDir $name), $gbk.GetBytes($text))
}

Copy-Item $exe (Join-Path $Payload 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $Payload 'launcher.ini') -Force
$dxvk = Join-Path $Root 'tools\dxvk\x86\d3d9.dll'
if (-not (Test-Path $dxvk)) { throw "缺少 $dxvk" }
Copy-Item $dxvk (Join-Path $Payload 'd3d9.dll') -Force
Copy-Item $dxvk (Join-Path $Payload '_dx9_compat\d3d9.dll') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $Payload 'dxvk.conf') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $Payload '_dx9_compat\dxvk.conf') -Force
Copy-Item (Join-Path $Root 'tools\nircmd\nircmdc.exe') (Join-Path $Payload '_tools\nircmd\ircmdc.exe') -Force
foreach ($name in 'game_starter.bat', '兼容修复.bat', '修改器说明.txt') { Copy-Text $name $Payload }
foreach ($name in '一键应用.bat', '一键撤回.bat') { Copy-Text $name $ReleaseDir }

$manifest = @(
  'game_starter.exe',
  'game_starter.bat',
  'launcher.ini',
  '兼容修复.bat',
  '修改器说明.txt',
  'd3d9.dll',
  'dxvk.conf',
  '_dx9_compat\d3d9.dll',
  '_dx9_compat\dxvk.conf',
  '_tools\nircmd\ircmdc.exe'
)
[IO.File]::WriteAllText((Join-Path $Payload 'manifest.txt'), (($manifest -join "`r`n") + "`r`n"), $gbk)
foreach ($rel in $manifest) {
  if (-not (Test-Path (Join-Path $Payload $rel))) { throw "交付包缺少 $rel" }
}
Write-Host "交付包已生成：$ReleaseDir"
Get-ChildItem $ReleaseDir -Recurse -File | Select-Object FullName, Length | Format-Table -AutoSize
