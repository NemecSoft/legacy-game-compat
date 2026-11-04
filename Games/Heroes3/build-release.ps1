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
New-Item -ItemType Directory -Force -Path $Payload, (Join-Path $Payload '_presets'), (Join-Path $Payload '_cnc-ddraw'), (Join-Path $Payload '_dgvoodoo2'), (Join-Path $Payload '_tools\nircmd') | Out-Null

function Copy-Text([string]$name, [string]$destDir) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes((Join-Path $destDir $name), $gbk.GetBytes($text))
}

Copy-Item $exe (Join-Path $Payload 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $Payload 'launcher.ini') -Force
$presets = @('ddraw.ini', 'ddraw-43.ini', 'ddraw-window.ini', 'ddraw-640.ini')
$layerPresets = @('ddraw-d3d9.ini', 'dgVoodoo-window.conf')   # 只在 _presets 里备着，不往根目录铺
foreach ($p in $presets) { Copy-Item (Join-Path $Src $p) (Join-Path $Payload $p) -Force }
foreach ($p in $presets + $layerPresets) { Copy-Item (Join-Path $Src $p) (Join-Path $Payload ('_presets\' + $p)) -Force }
# 默认档是第 6 档 dgVoodoo2：顶在 ddraw.dll 位置的就是 dgVoodoo2 的层，
# 根目录那份必须和 兼容修复.bat 装上去的字节一致，否则一键撤回会当成「玩家改过」而不敢恢复原文件
$vooDll = Join-Path $Root 'tools\dgvoodoo\x86\DDraw.dll'
if (-not (Test-Path $vooDll)) { throw '缺少 dgVoodoo2 层 tools\dgvoodoo\x86\DDraw.dll' }
Copy-Item $vooDll (Join-Path $Payload 'ddraw.dll') -Force
Copy-Item $vooDll (Join-Path $Payload '_dgvoodoo2\ddraw.dll') -Force
Copy-Item (Join-Path $Src 'dgVoodoo-window.conf') (Join-Path $Payload 'dgVoodoo.conf') -Force
# cnc-ddraw 层留在 _cnc-ddraw\，选 1-5 档时才由启动器/兼容修复顶上去
$cncDll = Join-Path $GameDir '_cnc-ddraw\ddraw.dll'
if (-not (Test-Path $cncDll)) { throw '缺少 cnc-ddraw 层 _cnc-ddraw\ddraw.dll（先跑 build.ps1）' }
Copy-Item $cncDll (Join-Path $Payload '_cnc-ddraw\ddraw.dll') -Force
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $Payload '_cnc-ddraw\ddraw.ini') -Force
Copy-Item (Join-Path $Root 'tools\nircmd\nircmdc.exe') (Join-Path $Payload '_tools\nircmd\nircmdc.exe') -Force
foreach ($name in 'game_starter.bat', '兼容修复.bat') { Copy-Text $name $Payload }
foreach ($name in '一键应用.bat', '一键撤回.bat') { Copy-Text $name $ReleaseDir }

$manifest = @(
  'game_starter.exe',
  'game_starter.bat',
  'launcher.ini',
  '兼容修复.bat',
  'ddraw.dll',
  'dgVoodoo.conf',
  'ddraw.ini',
  'ddraw-43.ini',
  'ddraw-window.ini',
  'ddraw-640.ini',
  '_presets\ddraw.ini',
  '_presets\ddraw-43.ini',
  '_presets\ddraw-window.ini',
  '_presets\ddraw-640.ini',
  '_presets\ddraw-d3d9.ini',
  '_presets\dgVoodoo-window.conf',
  '_cnc-ddraw\ddraw.dll',
  '_cnc-ddraw\ddraw.ini',
  '_dgvoodoo2\ddraw.dll',
  '_tools\nircmd\nircmdc.exe'
)
[IO.File]::WriteAllText((Join-Path $Payload 'manifest.txt'), (($manifest -join "`r`n") + "`r`n"), $gbk)
foreach ($rel in $manifest) {
  if (-not (Test-Path (Join-Path $Payload $rel))) { throw "交付包缺少 $rel" }
}
Write-Host "交付包已生成：$ReleaseDir"
Get-ChildItem $ReleaseDir -Recurse -File | Select-Object FullName, Length | Format-Table -AutoSize
