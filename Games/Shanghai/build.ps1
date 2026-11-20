param(
  [string]$GameDir = 'D:\YunGame\IndieGame\shanghai',
  [switch]$NoDeploy
)

# 血战上海滩 启动器构建 + 部署。
# 全程不启动游戏（规则 24 第 14 类：收尾不许顺手跑会启动游戏的测试）。
# 产出：Games\Shanghai\build\game_starter.exe + game_starter.sha256，部署到游戏目录。
# 布局（2026-11-15 二次定案：默认档临时切桌面 800×600 铺满，兼容层整个移除，退出自动还原）：
#   游戏目录根   game_starter.exe / game_starter.sha256 / launcher.ini（原生 D3D8，无层文件）
#   _legacy_compat_backup\   一键撤回用备份（本脚本部署时也走同一名录留底）

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
$cscArgs += (Join-Path $Src 'Env.cs')      # 环境读数 + 桌面切换/还原（规则 37/40）
$cscArgs += (Join-Path $Src 'Crash.cs')    # WER 崩溃取证（规则 35）
$cscArgs += (Join-Path $Src 'Cheats.cs')   # 一键秘籍引擎（扫描码 SendInput）
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
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$sha = Join-Path $Out 'game_starter.sha256'
[IO.File]::WriteAllText($sha, $hash + '  game_starter.exe' + "`r`n", [Text.Encoding]::ASCII)
Write-Host "已生成 game_starter.sha256：$hash"

if ($NoDeploy) { return }

# ---------- 部署到游戏目录 ----------
if (-not (Test-Path $GameDir)) { throw "游戏目录不存在：$GameDir" }
# 备份规则：我们自己的交付文件被新版本覆盖 → 上一份留档到仓库 Games\Shanghai\backup\<名字>.before-deploy，
# 不在游戏目录里堆 -bak（玩家拿到的是干净的绿色目录，规则 36）。
$ours = @('game_starter.exe', 'game_starter.sha256', 'launcher.ini')
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
  Backup-Existing $dst
  Copy-Item -LiteralPath $srcPath $dst -Force
  Write-Host "已部署 $dst"
}

Deploy-File (Join-Path $Src 'launcher.ini') 'launcher.ini'
Deploy-File $exe 'game_starter.exe'
Deploy-File $sha 'game_starter.sha256'

# ---------- 兼容层已移除（2026-11-15 用户定案：挂层后卡死 + 老年玩家容错为零） ----------
# 旧版部署过层文件的话在这里顺手清掉（幂等；只动我们自己部署过的三个文件名）
foreach ($stale in @('D3D8.dll', 'D3D9.dll', 'dgVoodoo.conf')) {
  $sp = Join-Path $GameDir $stale
  if (Test-Path $sp) { Remove-Item $sp -Force; Write-Host "已清理旧版兼容层：$stale" }
}

# 游戏目录不许出现源码文件（规则 9）
$bad = Get-ChildItem -LiteralPath $GameDir -Include *.cs, *.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ('游戏目录出现源码文件：' + (($bad | ForEach-Object { $_.FullName }) -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir"
Get-ChildItem -LiteralPath $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|sha256)|launcher\.ini)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host '提示：默认档启动时临时把桌面切到 800×600（退出自动还原）；备用档桌面不动、窗口模式。'
Write-Host '渲染：系统原生 D3D8（兼容层已移除）；桌面还原有三重保障：finally + 独立恢复进程 + 看门狗。'
