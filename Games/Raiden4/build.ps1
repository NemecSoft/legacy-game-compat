param(
  [string]$GameDir = 'D:\YunGame\G\Raiden4',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot                              # Games\Raiden4
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# 1. 编译 game_starter.exe（规则 27：新交付只做一个 exe，不写 bat）
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon" }
$manifest = Join-Path $Src 'app.manifest'
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe),
  ('-win32icon:' + $icon), ('-win32manifest:' + $manifest),
  (Join-Path $Src 'Keycaps.cs'), (Join-Path $Src 'Launcher.cs'))
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "编译失败" }
Write-Host "已编译 $exe"

# 2. 体积与文件规范（src 递归查；游戏目录只查顶层，模拟器运行时与游戏自带资源不算违规）
$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  & $check -Exe $exe -SrcDir $Src
  if ($LASTEXITCODE -ne 0) { throw "启动器 GUI 规范检查未通过" }
}

if ($NoDeploy) { return }

# 3. 部署：exe + launcher.ini（UTF-8，LoadIni 按 UTF-8 读）+ 无敌 trainer 脚本
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $GameDir 'tools') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'tools\trainer.ps1') (Join-Path $GameDir 'tools\trainer.ps1') -Force
Write-Host "已部署 game_starter.exe / launcher.ini / tools\trainer.ps1"

# 4. 游戏目录顶层不许有 .cs / 图片资源（模拟器运行时文件豁免）
$bad = Get-ChildItem $GameDir -File | Where-Object { $_.Extension -in '.cs', '.png', '.jpg', '.ico', '.resx' }
if ($bad) { throw ("游戏目录顶层出现禁止文件：" + ($bad.Name -join ', ')) }

Write-Host ""
Write-Host "部署完成：$GameDir"
Get-ChildItem $GameDir -File | Where-Object { $_.Name -match '^(game_starter\.exe|launcher\.ini|JJ11\.ini(\.sjis-bak)?|运行日志\.txt|readings\.txt)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize