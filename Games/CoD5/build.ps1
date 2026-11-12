param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD5',
  [switch]$NoDeploy
)

# 使命召唤5 启动器构建 + 部署。
# 全程不启动游戏（用户明令：收尾不许顺手跑会启动游戏的测试，规则 24 第 14 类）。
# 产出：Games\CoD5\build\game_starter.exe + game_starter.sha256，并部署到游戏目录。
# 布局（2026-11-10 用户定案：不打 DX 补丁，原生 Direct3D 9 单档）：
#   游戏目录根   不放任何兼容层文件
#   players\     通关存档三件套（profiles\active.txt + profiles\$$$\config.cfg + profiles\$$$\save\*.svg）
#                随游戏目录预置（fs_localAppData 重定向后存档跟目录走，换电脑自动带过去）

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'build'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

# ---------- 编译 ----------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
# 规则 21：图标只认仓库资源，缺了就构建失败，不许静默退回默认图标、更不许运行时去找游戏 exe
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon（仓库资源，所有 game_starter.exe 固定使用它）" }

# 键帽一律走通用库：Keycaps.cs 必须是 templates\Keycaps.cs 的整份拷贝（规则 30）
$keycaps = Join-Path $Src 'Keycaps.cs'
if (-not (Test-Path $keycaps)) { throw "缺少 $keycaps（从 templates\Keycaps.cs 整份复制，不要在 Launcher.cs 里临时画键帽）" }
$tplHash = (Get-FileHash (Join-Path $Repo 'templates\Keycaps.cs')).Hash
$srcHash = (Get-FileHash $keycaps).Hash
if ($tplHash -ne $srcHash) { Write-Warning "src\Keycaps.cs 与 templates\Keycaps.cs 不一致（键帽库是标准件，要改先改模板）" }

if (Test-Path $exe) { Remove-Item $exe -Force }
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe), ('-win32icon:' + $icon))
$cscArgs += $keycaps
$cscArgs += (Join-Path $Src 'Launcher.cs')
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "编译失败（csc 退出码 $LASTEXITCODE）" }
Write-Host "已编译 $exe（$([Math]::Round((Get-Item $exe).Length / 1KB, 1)) KB）"

$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  # 只查产物体积 + src 目录不许有图片/资源文件；不传 -GameDir（游戏目录本来就有 .bmp/.ico 素材）
  & $check -Exe $exe -SrcDir $Src -MaxKB 1024
  if ($LASTEXITCODE -ne 0) { throw '启动器 GUI 规范检查未通过' }
}

# ---------- 自校验哈希 ----------
# hash + 两个空格 + game_starter.exe，ASCII 单行（与其他游戏同格式）。
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$sha = Join-Path $Out 'game_starter.sha256'
[IO.File]::WriteAllText($sha, $hash + '  game_starter.exe' + "`r`n", [Text.Encoding]::ASCII)
Write-Host "已生成 game_starter.sha256：$hash"

if ($NoDeploy) { return }

# ---------- 部署到游戏目录 ----------
if (-not (Test-Path $GameDir)) { throw "游戏目录不存在：$GameDir" }
# 备份规则：我们自己的交付文件被新版本覆盖 → 上一份留档到仓库 Games\CoD5\backup\<名字>.before-deploy，
# 不在游戏目录里堆 -bak（玩家拿到的是干净的绿色目录，规则 36）。
$ours = @('game_starter.exe', 'game_starter.sha256', 'launcher.ini', 'display.state')
function Backup-Existing([string]$dst) {
  if (-not (Test-Path $dst)) { return }
  $leaf = Split-Path $dst -Leaf
  if ($ours -contains $leaf) {
    Copy-Item -LiteralPath $dst (Join-Path $Backup ($leaf + '.before-deploy')) -Force
    Write-Host "上一份 $leaf 已留档到仓库 backup\$leaf.before-deploy（不在游戏目录留 -bak）"
    return
  }
  $bak = $dst + '-bak'
  if (Test-Path $bak) { Write-Host "已存在备份，不覆盖：$bak"; return }
  Copy-Item -LiteralPath $dst $bak -Force
  Write-Host "已备份 $dst → $(Split-Path $bak -Leaf)"
}
function Deploy-File([string]$srcPath, [string]$name, [string]$dstDir = $GameDir) {
  if (-not (Test-Path $srcPath)) { throw "缺少 $srcPath" }
  $dst = Join-Path $dstDir $name
  if ((Test-Path $dst) -and (Get-FileHash $dst).Hash -eq (Get-FileHash $srcPath).Hash) {
    Write-Host "已一致，不动：$name"; return
  }
  if ($name -ne 'game_starter.sha256') { Backup-Existing $dst }
  Copy-Item -LiteralPath $srcPath $dst -Force
  Write-Host "已部署 $dst"
}

Deploy-File (Join-Path $Src 'launcher.ini') 'launcher.ini'
Deploy-File $exe 'game_starter.exe'
Deploy-File $sha 'game_starter.sha256'

# 游戏目录不许出现源码文件（规则 9）
$bad = Get-ChildItem -LiteralPath $GameDir -Include *.cs, *.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ('游戏目录出现源码文件：' + (($bad | ForEach-Object { $_.FullName }) -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir"
Get-ChildItem -LiteralPath $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|sha256)|launcher\.ini|display\.state)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host '提示：players\ 下的通关存档随游戏目录分发；启动器每次启动把画面规范成全屏 + 本机桌面分辨率（账本 display.state）。'
