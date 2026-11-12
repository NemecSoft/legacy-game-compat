param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot                              # Games\RoadRash
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Tools = Join-Path $PSScriptRoot 'tools'
$Cnc = Join-Path $Tools 'cnc-ddraw'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

# 1. 用户原版启动器只备份过一次，就在 backup\go.bat.原版（那时我们的包装还叫 go.bat）
$origBak = Join-Path $Backup 'go.bat.原版'
# 改名迁移：游戏目录里的旧 go.bat 只有确认是「我们的薄包装」才删，别误删用户自己的启动脚本
$oldGo = Join-Path $GameDir 'go.bat'
if ((Test-Path $oldGo) -and (Select-String -Path $oldGo -Pattern 'game_starter' -Quiet)) {
  Remove-Item $oldGo -Force
  Write-Host "已删掉旧名包装 $oldGo（改叫 game_starter.bat）"
}
# 部署前守卫：目标同名文件不是我们生成的（缺 game_starter 签名）就先留底，别覆盖用户手改的东西
$mine = Join-Path $GameDir 'game_starter.bat'
if ((Test-Path $mine) -and -not (Select-String -Path $mine -Pattern 'game_starter\.exe' -Quiet)) {
  Copy-Item $mine (Join-Path $Backup 'game_starter.bat.用户版') -Force
  Write-Host "已备份用户自己的 game_starter.bat -> backup\game_starter.bat.用户版"
}

# 2. 编译 game_starter.exe（按键方案落地已合并为 game_starter.exe /keymap，交付只带一个 exe）
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }
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
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "编译失败" }
Write-Host "已编译 $exe"
$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  & $check -Exe $exe -SrcDir $Src -MaxKB 60
  if ($LASTEXITCODE -ne 0) { throw "启动器 GUI 规范检查未通过" }
}

if ($NoDeploy) { return }

# 3. bat 源码 UTF-8 -> GBK + CRLF 后落地
$gbk = [System.Text.Encoding]::GetEncoding(936)
function Deploy-Bat([string]$name) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [System.Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  [IO.File]::WriteAllBytes((Join-Path $GameDir $name), $gbk.GetBytes($text))
  Write-Host "已部署 $name (GBK+CRLF)"
}
Deploy-Bat 'game_starter.bat'
Deploy-Bat '兼容修复.bat'
Deploy-Bat '恢复原版.bat'
Deploy-Bat '切键-修改版.bat'
Deploy-Bat '切键-WASD.bat'
Deploy-Bat '切键-系统默认.bat'
Deploy-Bat '操作说明.txt'

# 3b. 按键方案：三套 reg（修改版 / WASD / 系统默认），game_starter.exe /keymap 切换
foreach ($r in 'road64.reg', 'road.reg', 'road64-wasd.reg', 'road-wasd.reg', 'road64-system.reg', 'road-system.reg') {
  Copy-Item (Join-Path $Src $r) (Join-Path $GameDir $r) -Force
}
foreach ($staleKey in 'road64-arrows.reg', 'road-arrows.reg', '切键-方向键.bat', '切键-修改者.bat') {
  Remove-Item (Join-Path $GameDir $staleKey) -Force -ErrorAction SilentlyContinue
}
Write-Host "已部署三套按键 reg + 切键脚本"

# 4. 兼容层：游戏目录 + _cnc-ddraw 留底（四套 ddraw.ini + DXVK）
Copy-Item (Join-Path $Cnc 'ddraw.dll') (Join-Path $GameDir 'ddraw.dll') -Force
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $GameDir 'ddraw.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-stretch.ini') (Join-Path $GameDir 'ddraw-stretch.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-soft.ini') (Join-Path $GameDir 'ddraw-soft.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-640.ini') (Join-Path $GameDir 'ddraw-640.ini') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $GameDir 'dxvk.conf') -Force
Copy-Item (Join-Path $Repo 'tools\dxvk\x86\d3d9.dll') (Join-Path $GameDir 'd3d9.dll') -Force
$layerBak = Join-Path $GameDir '_cnc-ddraw'
New-Item -ItemType Directory -Force -Path $layerBak | Out-Null
Copy-Item (Join-Path $Cnc 'ddraw.dll') (Join-Path $layerBak 'ddraw.dll') -Force
Copy-Item (Join-Path $Src 'ddraw.ini') (Join-Path $layerBak 'ddraw.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-stretch.ini') (Join-Path $layerBak 'ddraw-stretch.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-soft.ini') (Join-Path $layerBak 'ddraw-soft.ini') -Force
Copy-Item (Join-Path $Src 'ddraw-640.ini') (Join-Path $layerBak 'ddraw-640.ini') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $layerBak 'dxvk.conf') -Force
Copy-Item (Join-Path $Repo 'tools\dxvk\x86\d3d9.dll') (Join-Path $layerBak 'd3d9.dll') -Force
foreach ($stale in 'game_starter-soft.bat','game_starter-640.bat','go-dxvk.bat','ddraw-dxvk.ini') {
  Remove-Item (Join-Path $GameDir $stale) -Force -ErrorAction SilentlyContinue
  Remove-Item (Join-Path $layerBak $stale) -Force -ErrorAction SilentlyContinue
}
Write-Host "已部署 cnc-ddraw 7.1 + DXVK 2.5.3 + _cnc-ddraw 留底"

# 5. 启动器（game_starter.bat 已不再需要 nircmd 藏窗口：黑框由 game_starter.exe /hideparent 处理）
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
Remove-Item (Join-Path $GameDir '_tools\keymap.exe') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $GameDir '_tools\nircmd') -Recurse -Force -ErrorAction SilentlyContinue
# 启动器改名 go.exe -> game_starter.exe：旧名一并清掉，别留两个入口
Remove-Item (Join-Path $GameDir 'go.exe') -Force -ErrorAction SilentlyContinue
Write-Host "已部署 game_starter.exe / launcher.ini（并清掉 _tools\keymap.exe、_tools\nircmd、旧名 go.exe）"

# 5b. 启动门禁（可选）：代理 AWEMan32.dll 顶替原版，只有启动器注入的暗号才能放行。
#     用 gate\ 里编译好的制品，不依赖本机装 VS；首次部署先把 1995 年原版改名留底。
$gateDir = Join-Path $PSScriptRoot 'gate'
$proxy   = Join-Path $gateDir 'AWEMan32.dll'
if (Test-Path $proxy) {
  $gameProxy = Join-Path $GameDir 'AWEMan32.dll'
  $gameReal  = Join-Path $GameDir 'AWEMan32_real.dll'
  if (-not (Test-Path $gameReal) -and (Test-Path $gameProxy)) {
    Copy-Item $gameProxy $gameReal -Force          # 原版（只读）复制留底
    (Get-Item $gameReal).IsReadOnly = $false
    Write-Host "首次部署门禁：原始 AWEMan32.dll 已留底为 AWEMan32_real.dll"
  }
  if (Test-Path $gameReal) {
    if (Test-Path $gameProxy) { (Get-Item $gameProxy).IsReadOnly = $false }
    Copy-Item $proxy $gameProxy -Force              # 代理顶上原名
    Write-Host "已部署启动门禁代理 AWEMan32.dll（一次性，无需 VS）"
    Deploy-Bat '门禁-撤回.bat'                      # 一键回滚脚本
  } else {
    Write-Host "警告：游戏目录找不到 AWEMan32.dll / AWEMan32_real.dll，跳过门禁部署"
  }
}

# 6. 自检：目标目录不许有 .cs
$cs = Get-ChildItem $GameDir -Filter *.cs -Recurse -ErrorAction SilentlyContinue
if ($cs) { throw ("目标目录出现 .cs 源文件：" + ($cs.FullName -join ', ')) }

Write-Host ""
Write-Host "部署完成：$GameDir"
Get-ChildItem $GameDir -File | Where-Object { $_.Name -match '^(game_starter\.(exe|bat)|go\.(exe|bat)|launcher\.ini|keymap\.txt|savepick\.txt|运行日志\.txt|ddraw(-stretch|-soft|-640)?\.(dll|ini)|d3d9\.dll|dxvk\.conf|兼容修复\.bat|恢复原版\.bat|切键-.+\.bat|road(64)?(-wasd|-system)?\.reg|操作说明\.txt|go\.bat\.原版-bak)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Get-ChildItem (Join-Path $GameDir '_tools') -File -ErrorAction SilentlyContinue | Select-Object Name, Length | Format-Table -AutoSize
