param(
  [string]$GameDir = 'D:\YunGame\F\CC1-RedAlert',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
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

$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe), ('-win32icon:' + $icon))
# 通用拟物键帽库（templates\Keycaps.cs 的整份拷贝，四平台共用一套标准件）
$cscArgs += (Join-Path $Src 'Keycaps.cs')
$cscArgs += (Join-Path $Src 'Launcher.cs')
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw '编译失败' }
Write-Host "已编译 $exe"

$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  & $check -Exe $exe -SrcDir $Src -MaxKB 1024
  if ($LASTEXITCODE -ne 0) { throw '启动器 GUI 规范检查未通过' }
}

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$sha = Join-Path $Out 'game_starter.sha256'
[IO.File]::WriteAllText($sha, $hash + '  game_starter.exe' + "`r`n", [Text.Encoding]::ASCII)
Write-Host "已生成 game_starter.sha256：$hash"

if ($NoDeploy) { return }
if (-not (Test-Path $GameDir)) { throw "游戏目录不存在：$GameDir" }

# 只部署成品三件：exe + ini + sha256。不写 bat（AGENTS 规则 27），
# 不碰 Engine\Support\settings.yaml / 自带 启动游戏.exe / ra.ftl / mod.yaml
#（settings.yaml 的 Player: Name: 由启动器运行时按机器名写，构建期不碰玩家设置）。
function Backup-File([string]$name) {
  $dst = Join-Path $GameDir $name
  $bak = Join-Path $Backup ($name + '.before-launcher')
  if ((Test-Path $dst) -and -not (Test-Path $bak)) { Copy-Item $dst $bak -Force }
}
Backup-File 'game_starter.exe'
Backup-File 'launcher.ini'
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
Copy-Item $sha (Join-Path $GameDir 'game_starter.sha256') -Force
Write-Host "已部署到 $GameDir"