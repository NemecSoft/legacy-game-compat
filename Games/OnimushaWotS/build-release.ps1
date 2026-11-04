param(
  [string]$ReleaseDir = (Join-Path $PSScriptRoot 'release')
)

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$RelSrc = Join-Path $PSScriptRoot 'release-src'
$Out = Join-Path $PSScriptRoot 'out'
$Payload = Join-Path $ReleaseDir 'payload'
if (-not (Test-Path (Join-Path $Out 'game_starter.exe'))) { throw '先运行 build.ps1 生成 out\game_starter.exe' }
New-Item -ItemType Directory -Force -Path $Payload | Out-Null

$gbk = [System.Text.Encoding]::GetEncoding(936)
function Convert-Bat([string]$src, [string]$dst) {
  $text = [IO.File]::ReadAllText($src, [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes($dst, $gbk.GetBytes($text))
  Write-Host "已转换 $dst (GBK+CRLF)"
}

Copy-Item (Join-Path $Out 'game_starter.exe') (Join-Path $Payload 'game_starter.exe') -Force
Copy-Item (Join-Path $Out 'game_starter.sha256') (Join-Path $Payload 'game_starter.sha256') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $Payload 'launcher.ini') -Force
Convert-Bat (Join-Path $Src 'game_starter.bat') (Join-Path $Payload 'game_starter.bat')

$n = Join-Path $Payload '_tools\nircmd'
New-Item -ItemType Directory -Force -Path $n | Out-Null
$nircmd = Join-Path $Repo 'tools\nircmd\nircmdc.exe'
if (Test-Path $nircmd) { Copy-Item $nircmd (Join-Path $n 'nircmdc.exe') -Force }

$manifest = @(
  'game_starter.exe',
  'game_starter.bat',
  'launcher.ini',
  'game_starter.sha256',
  '_tools\nircmd\nircmdc.exe'
)
[IO.File]::WriteAllText((Join-Path $Payload 'manifest.txt'), ($manifest -join "`r`n") + "`r`n", [Text.Encoding]::ASCII)

Convert-Bat (Join-Path $RelSrc '一键应用.bat') (Join-Path $ReleaseDir '一键应用.bat')
Convert-Bat (Join-Path $RelSrc '一键撤回.bat') (Join-Path $ReleaseDir '一键撤回.bat')

if (Get-ChildItem $Payload -Recurse -Filter 'config.ini' -ErrorAction SilentlyContinue) {
  throw '交付包里出现 config.ini，禁止分发'
}
if (Get-ChildItem $ReleaseDir -Recurse -Include *.cs,*.ps1 -ErrorAction SilentlyContinue) {
  throw '交付包里出现源码文件'
}

Write-Host ''
Write-Host "交付包已生成：$ReleaseDir"
Get-ChildItem $ReleaseDir -Recurse -File |
  Select-Object FullName, Length | Format-Table -AutoSize
