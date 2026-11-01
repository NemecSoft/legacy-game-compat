param(
  [string]$GameDir = 'D:\YunGame\Z\JustCause1',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot                              # Games\JustCause
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# 1. 编译 game_starter.exe
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }
$exe = Join-Path $Out 'game_starter.exe'
$ico = Join-Path $Out 'game_starter.ico'
$iconExe = Join-Path $GameDir 'JustCause.exe'
try {
  Add-Type -AssemblyName System.Drawing
  if (Test-Path $iconExe) {
    $ic = [System.Drawing.Icon]::ExtractAssociatedIcon($iconExe)
    if ($ic) { $fs = [IO.File]::Create($ico); $ic.Save($fs); $fs.Close(); $ic.Dispose() }
  }
} catch { Write-Host "图标提取失败，继续无图标：$($_.Exception.Message)" }
$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', ('-out:' + $exe))
if (Test-Path $ico) { $cscArgs += ('-win32icon:' + $ico) }
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

# 2. 文本修复 ASI：原版留底，裁剪版装进 scripts\
python (Join-Path $Src 'build-textfix.py')
if ($LASTEXITCODE -ne 0) { throw "build-textfix.py 失败" }

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

# 4. 兼容层：DXVK 2.5.3 + dxvk.conf，游戏目录生效 + _dxvk 留底
New-Item -ItemType Directory -Force -Path (Join-Path $GameDir '_dxvk') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $GameDir '_backup') | Out-Null
Copy-Item (Join-Path $Repo 'tools\dxvk\x86\d3d9.dll') (Join-Path $GameDir 'd3d9.dll') -Force
Copy-Item (Join-Path $Repo 'tools\dxvk\x86\d3d9.dll') (Join-Path $GameDir '_dxvk\d3d9.dll') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $GameDir 'dxvk.conf') -Force
Copy-Item (Join-Path $Src 'dxvk.conf') (Join-Path $GameDir '_dxvk\dxvk.conf') -Force

# 5. 文本修复 ASI：scripts\ 生效，_backup 留底（原版 overlay 也留底，恢复原版用）
Copy-Item (Join-Path $Src 'JCTextFix.asi') (Join-Path $GameDir 'scripts\JCTextFix.asi') -Force
Copy-Item (Join-Path $Src 'JCTextFix.asi') (Join-Path $GameDir '_backup\JCTextFix.asi') -Force
Copy-Item (Join-Path $Src 'vendor\JCSubtitleOverlay.asi') (Join-Path $GameDir '_backup\JCSubtitleOverlay.asi') -Force
$stale = Join-Path $GameDir 'scripts\JCSubtitleOverlay.asi'
if (Test-Path $stale) { Remove-Item $stale -Force; Write-Host "已移除 scripts\JCSubtitleOverlay.asi（原版在 _backup）" }

# 6. 启动器
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
# 控制台菜单版居中用 nircmd（if exist 保护，缺了也能启动）
New-Item -ItemType Directory -Force -Path (Join-Path $GameDir '_tools\nircmd') | Out-Null
Copy-Item (Join-Path $Repo 'tools\nircmd\nircmdc.exe') (Join-Path $GameDir '_tools\nircmd\nircmdc.exe') -Force

# 7. 清掉开发期遗留的探针（游戏目录只留成品）
foreach ($junk in 'D3D9Probe32.exe','D3D9Probe32_d3d9.log') {
  Remove-Item (Join-Path $GameDir $junk) -Force -ErrorAction SilentlyContinue
}

# 8. 自检：目标目录不许有 .cs / .ps1
$bad = Get-ChildItem $GameDir -Include *.cs,*.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ("目标目录出现源码文件：" + ($bad.FullName -join ', ')) }

Write-Host ""
Write-Host "部署完成：$GameDir"
Get-ChildItem $GameDir -File | Where-Object { $_.Name -match '^(game_starter\.(exe|bat)|launcher\.ini|运行日志\.txt|d3d9\.dll|dxvk\.conf|兼容修复\.bat|恢复原版\.bat)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
