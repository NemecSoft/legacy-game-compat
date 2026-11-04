param(
  [string]$GameDir = 'D:\YunGame\II\DFBHD',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

# 1. 编译 game_starter.exe
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

# 2. bat 源码 UTF-8 -> GBK + CRLF 后落地；已有同名文件先留底
$gbk = [System.Text.Encoding]::GetEncoding(936)
function Deploy-Bat([string]$name) {
  $text = [IO.File]::ReadAllText((Join-Path $Src $name), [System.Text.Encoding]::UTF8)
  $text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
  $dst = Join-Path $GameDir $name
  $bak = Join-Path $Backup ($name + '.before-dfbhd')
  if ((Test-Path $dst) -and -not (Test-Path $bak)) { Copy-Item $dst $bak -Force }
  [IO.File]::WriteAllBytes($dst, $gbk.GetBytes($text))
  Write-Host "已部署 $name (GBK+CRLF)"
}
Deploy-Bat 'game_starter.bat'
Deploy-Bat '兼容修复.bat'
Deploy-Bat '恢复原版.bat'

# 3. 启动器、配置和交付版 DXVK 配置（HUD=fps）
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $GameDir 'dxvk.conf') -Force

# 4. _dxvk 留底：DLL 从游戏目录现有文件取一次，dxvk.conf 用交付版（HUD 开）
$dxvk = Join-Path $GameDir '_dxvk'
New-Item -ItemType Directory -Force -Path $dxvk | Out-Null
foreach ($f in 'd3d8.dll', 'd3d9.dll') {
  $dst = Join-Path $dxvk $f
  $cur = Join-Path $GameDir $f
  if (-not (Test-Path $dst) -and (Test-Path $cur)) {
    Copy-Item $cur $dst -Force
    Write-Host "备份 $f -> _dxvk"
  }
}
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $dxvk 'dxvk.conf') -Force

# 5. 控制台窗口居中
$n = Join-Path $GameDir '_tools\nircmd'
New-Item -ItemType Directory -Force -Path $n | Out-Null
$nircmd = Join-Path $Repo 'tools\nircmd\nircmdc.exe'
if (Test-Path $nircmd) { Copy-Item $nircmd (Join-Path $n 'ircmdc.exe') -Force }

# 6. 旧的 go.bat 入口作废，留底到 Games\DFBHD\backup
foreach ($old in 'go.bat', 'go.bat.bak') {
  $p = Join-Path $GameDir $old
  if (Test-Path $p) {
    Copy-Item $p (Join-Path $Backup ($old + '.before-dfbhd')) -Force
    Remove-Item $p -Force
    Write-Host "已收起旧入口 $old"
  }
}

# 7. 自检：目标目录不许有 .cs / .ps1
$bad = Get-ChildItem $GameDir -Include *.cs,*.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ("目标目录出现源码文件：" + ($bad.FullName -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir"
Get-ChildItem $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|bat)|launcher\.ini|dxvk\.conf|兼容修复\.bat|恢复原版\.bat|运行日志\.txt)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
