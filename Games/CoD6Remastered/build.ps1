param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD6.Remastered',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

# 编译 game_starter.exe（.NET Framework csc，统一图标来自仓库资源）
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon" }
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe), ('-win32icon:' + $icon),
  (Join-Path $Src 'Launcher.cs'))
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "编译失败" }
Write-Host "已编译 $exe"
$sz = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host "体积 $sz KB"
if ((Get-Item $exe).Length -gt 1MB) { throw "exe 超过 1MB 红线" }

if ($NoDeploy) { return }

# 部署前留底游戏目录原有的语言标记（只留一次）
$gameDb = Join-Path $GameDir 'Launcher.db'
$dbBak = Join-Path $Backup 'Launcher.db.原版'
if ((Test-Path $gameDb) -and -not (Test-Path $dbBak)) {
  Copy-Item $gameDb $dbBak -Force
  Write-Host "已留底原始 Launcher.db -> backup\Launcher.db.原版"
}

# 部署 exe + 哈希（规则10：自校验）
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
$hash = (Get-FileHash -LiteralPath (Join-Path $GameDir 'game_starter.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$line = "$hash  game_starter.exe`r`n"
[IO.File]::WriteAllText((Join-Path $GameDir 'game_starter.sha256'), $line, [Text.Encoding]::ASCII)
[IO.File]::WriteAllText((Join-Path $Out 'game_starter.sha256'), $line, [Text.Encoding]::ASCII)
Write-Host "已部署 game_starter.exe + sha256（$hash）"

# 部署玩家说明与恢复脚本（UTF-8 源码 -> GBK + CRLF）
$gbk = [System.Text.Encoding]::GetEncoding(936)
foreach ($name in '使用说明.txt', '恢复英文.bat') {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes((Join-Path $GameDir $name), $gbk.GetBytes($text))
  Write-Host "已部署 $name (GBK+CRLF)"
}

# 目标目录不许有源码
$cs = Get-ChildItem $GameDir -Filter *.cs -Recurse -ErrorAction SilentlyContinue
if ($cs) { throw ("目标目录出现 .cs：" + ($cs.FullName -join ', ')) }
Write-Host "部署完成：$GameDir"
