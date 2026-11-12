param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD8',
  [switch]$NoDeploy
)

# 使命召唤8 启动器构建 + 部署。
# 全程不启动游戏（用户明令：收尾不许顺手跑会启动游戏的测试，规则 24 第 14 类）。
# 产出：Games\CoD8\build\game_starter.exe + game_starter.sha256，并部署到游戏目录。
# 布局（2026-11-10 用户定案：默认原生 D3D9 档，不打 DXVK）：
#   游戏目录根   不放任何 DXVK 文件（原生档直接跑；启动器若发现根目录有 DXVK 会移进 _dxvk\禁用\）
#   _dxvk\       d3d9.dll / dxgi.dll / dxvk.conf = 留底，只给第 2 档（DXVK 备胎）用

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
# 仓库脚本 scripts\write-launcher-hashes.ps1 里的游戏目录清单没有 CoD2（也不许顺手改别人的脚本），
# 所以这里按同一份格式自己写：hash + 两个空格 + game_starter.exe，ASCII 单行。
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$sha = Join-Path $Out 'game_starter.sha256'
[IO.File]::WriteAllText($sha, $hash + '  game_starter.exe' + "`r`n", [Text.Encoding]::ASCII)
Write-Host "已生成 game_starter.sha256：$hash"

if ($NoDeploy) { return }

# ---------- 部署到游戏目录 ----------
if (-not (Test-Path $GameDir)) { throw "游戏目录不存在：$GameDir" }
# 备份规则（两种情况分开处理，别把交付目录搞脏）：
#   * 我们自己的交付文件（game_starter.exe / game_starter.sha256 / launcher.ini / dxvk.conf）
#     被新版本覆盖 → 上一份留档到仓库 Games\CoD8\backup\<名字>.before-deploy，
#     不在游戏目录里堆 *-bak：玩家拿到的是干净的绿色目录，规则 36「放进目录就能跑」。
#   * 覆盖到别的同名文件（现在没有；将来若有游戏自己的文件）→ 就地备份成 *.xxx-bak，只备一次。
$ours = @('game_starter.exe', 'game_starter.sha256', 'launcher.ini', 'dxvk.conf')
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
  # sha256 是构建产物自己的校验文件，覆盖它不需要留备份（留了反而在游戏目录里多一个残留）
  if ($name -ne 'game_starter.sha256') { Backup-Existing $dst }
  Copy-Item -LiteralPath $srcPath $dst -Force
  Write-Host "已部署 $dst"
}

Deploy-File (Join-Path $Src 'launcher.ini') 'launcher.ini'
Deploy-File $exe 'game_starter.exe'
Deploy-File $sha 'game_starter.sha256'

# DXVK 只维护 _dxvk\ 留底（第 2 档备胎用），游戏目录根一律不放 DXVK 文件：
# 默认档是原生 D3D9，根目录多一份 d3d9.dll 就等于偷偷打了兼容层（2026-11-10 用户明令不打 DXVK）。
$dxvkDir = Join-Path $GameDir '_dxvk'
New-Item -ItemType Directory -Force -Path $dxvkDir | Out-Null
Deploy-File (Join-Path $Src 'dxvk.conf') 'dxvk.conf' $dxvkDir
$repoDxvk = Join-Path $Repo 'tools\dxvk\x86'
foreach ($dll in 'd3d9.dll', 'dxgi.dll') {
  if (-not (Test-Path (Join-Path $dxvkDir $dll))) {
    Copy-Item (Join-Path $repoDxvk $dll) (Join-Path $dxvkDir $dll) -Force
    Write-Host "已补留底 _dxvk\$dll（仓库 DXVK 2.5.3 x86）"
  }
}
# 根目录不许留 DXVK（上一版交付/手工测试的残留）：有就清掉（留底在 _dxvk\，删根目录不丢东西）
foreach ($f in 'd3d9.dll', 'dxgi.dll', 'dxvk.conf') {
  $root = Join-Path $GameDir $f
  if (Test-Path $root) {
    Remove-Item $root -Force
    Write-Host "已清掉游戏目录根的 $f（默认原生档不打 DXVK；留底在 _dxvk\）"
  }
}

# 游戏目录不许出现源码文件（规则 9）
$bad = Get-ChildItem -LiteralPath $GameDir -Include *.cs, *.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ('游戏目录出现源码文件：' + (($bad | ForEach-Object { $_.FullName }) -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir"
Get-ChildItem -LiteralPath $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|sha256)|launcher\.ini|dxvk\.conf|d3d9\.dll|dxgi\.dll)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host '提示：游戏目录里若还留着上次测试的 iw5sp_d3d9.log（DXVK 流水日志），'
Write-Host '      那是排障产物不是交付内容；本启动器已把游戏进程的日志级别钉成 error，不再产生流水日志。'
