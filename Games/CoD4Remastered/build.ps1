param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD4.Remastered',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# 通用键帽库必须是模板那份，不许各游戏改出分叉
$tpl = Join-Path $Repo 'templates\Keycaps.cs'
$ours = Join-Path $Src 'Keycaps.cs'
if (-not (Test-Path $ours)) { throw "缺少 $ours" }
if ((Get-FileHash $tpl).Hash -ne (Get-FileHash $ours).Hash) {
  throw "src\Keycaps.cs 与 templates\Keycaps.cs 不一致，请整份重新复制"
}

# 编译 game_starter.exe（.NET Framework csc，统一图标来自仓库资源）
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon" }
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe), ('-win32icon:' + $icon),
  (Join-Path $Src 'Keycaps.cs'), (Join-Path $Src 'Launcher.cs'))
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "编译失败" }
$sz = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host "已编译 $exe（$sz KB）"
if ((Get-Item $exe).Length -gt 1MB) { throw "exe 超过 1MB 红线" }

if ($NoDeploy) { return }

# 部署前先关掉占着文件的旧启动器（实测截图后会残留）
Get-Process game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

# 部署 exe + 哈希（规则 10：自校验）
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
$hash = (Get-FileHash -LiteralPath (Join-Path $GameDir 'game_starter.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$line = "$hash  game_starter.exe`r`n"
[IO.File]::WriteAllText((Join-Path $GameDir 'game_starter.sha256'), $line, [Text.Encoding]::ASCII)
[IO.File]::WriteAllText((Join-Path $Out 'game_starter.sha256'), $line, [Text.Encoding]::ASCII)
Write-Host "已部署 game_starter.exe + sha256（$hash）"

# 玩家说明：UTF-8 源码 -> GBK + CRLF
$gbk = [Text.Encoding]::GetEncoding(936)
$name = '使用说明.txt'
$text = [IO.File]::ReadAllText((Join-Path $Src $name), [Text.Encoding]::UTF8)
$text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
[IO.File]::WriteAllBytes((Join-Path $GameDir $name), $gbk.GetBytes($text))
Write-Host "已部署 $name (GBK+CRLF)"

# 目标目录不许有源码（规则 9 / 27：新交付只一个 exe，不写 bat）。
# 整合包自带的两个启动/运行库 bat 是原有文件，保持原样、不纳入检查。
$bad = Get-ChildItem $GameDir -File -Include *.cs, *.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ("目标目录出现源码：" + ($bad.FullName -join ', ')) }
Write-Host "部署完成：$GameDir"
