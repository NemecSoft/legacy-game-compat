param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon（仓库资源，所有 game_starter.exe 固定使用它）" }
$oldIco = Join-Path $Out 'game_starter.ico'
if (Test-Path $oldIco) { Remove-Item $oldIco -Force }
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe))
$cscArgs += ('-win32icon:' + $icon)
$cscArgs += (Join-Path $Src 'Launcher.cs')
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw '编译失败' }
Write-Host "已编译 $exe"
$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  & $check -Exe $exe -SrcDir $Src -MaxKB 60
  if ($LASTEXITCODE -ne 0) { throw '启动器 GUI 规范检查未通过' }
}

if ($NoDeploy) { return }

$gbk = [System.Text.Encoding]::GetEncoding(936)
function Backup-File([string]$name) {
  $dst = Join-Path $GameDir $name
  $bak = Join-Path $Backup ($name + '.before-h3')
  if ((Test-Path $dst) -and -not (Test-Path $bak)) { Copy-Item $dst $bak -Force }
}
function Deploy-Bat([string]$name) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  Backup-File $name
  [IO.File]::WriteAllBytes((Join-Path $GameDir $name), $gbk.GetBytes($text))
  Write-Host "已部署 $name (GBK+CRLF)"
}

$presets = @('ddraw.ini', 'ddraw-43.ini', 'ddraw-window.ini', 'ddraw-640.ini',
           'ddraw-d3d9.ini', 'dgVoodoo-window.conf')
$presetDir = Join-Path $GameDir '_presets'
New-Item -ItemType Directory -Force -Path $presetDir | Out-Null
foreach ($p in $presets) { Copy-Item (Join-Path $Src $p) (Join-Path $presetDir $p) -Force }

$layer = Join-Path $GameDir '_cnc-ddraw'
New-Item -ItemType Directory -Force -Path $layer | Out-Null
$curDll = Join-Path $GameDir 'ddraw.dll'
$bakDll = Join-Path $layer 'ddraw.dll'
if (-not (Test-Path $bakDll) -and (Test-Path $curDll)) { Copy-Item $curDll $bakDll -Force }
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $GameDir 'ddraw.ini') -Force
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $layer 'ddraw.ini') -Force

# dgVoodoo2 层：仓库自带的 32 位 DDraw.dll（游戏是 32 位），落成 _dgvoodoo2\ddraw.dll
$vd = Join-Path $GameDir '_dgvoodoo2'
New-Item -ItemType Directory -Force -Path $vd | Out-Null
$vdSrc = Join-Path $Repo 'tools\dgvoodoo\x86\DDraw.dll'
if (-not (Test-Path $vdSrc)) { throw ('缺少 dgVoodoo2 层：' + $vdSrc) }
Copy-Item $vdSrc (Join-Path $vd 'ddraw.dll') -Force

Deploy-Bat 'game_starter.bat'
Deploy-Bat '兼容修复.bat'
Backup-File 'game_starter.exe'
Backup-File 'launcher.ini'
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force

$n = Join-Path $GameDir '_tools\nircmd'
New-Item -ItemType Directory -Force -Path $n | Out-Null
$nircmd = Join-Path $Repo 'tools\nircmd\nircmdc.exe'
if (Test-Path $nircmd) { Copy-Item $nircmd (Join-Path $n 'nircmdc.exe') -Force }
$stale = Join-Path $n 'ircmdc.exe'
if (Test-Path $stale) { Remove-Item $stale -Force }   # 2026-11-04 修正：文件名必须和 bat 里逐字一致

$bad = Get-ChildItem $GameDir -Include *.cs,*.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ("目标目录出现源码文件：" + ($bad.FullName -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir"
Get-ChildItem $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|bat)|launcher\.ini|兼容修复\.bat|ddraw\.dll|ddraw\.ini|运行日志\.txt)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
