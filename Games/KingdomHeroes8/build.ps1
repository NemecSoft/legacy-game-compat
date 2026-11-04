param(
  [string]$GameDir = 'D:\YunGame\F\KingdomHeroes8',
  [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Src = Join-Path $PSScriptRoot 'src'
$Out = Join-Path $PSScriptRoot 'out'
$Backup = Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Force -Path $Out, $Backup | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }
$exe = Join-Path $Out 'game_starter.exe'
$icon = Join-Path $Repo 'assets\ico.ico'
if (-not (Test-Path $icon)) { throw "缺少统一启动器图标：$icon（仓库资源，所有 game_starter.exe 固定使用它）" }

$cscArgs = @('-nologo', '-target:winexe', '-optimize+', '-platform:anycpu', '-codepage:65001',
  '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll',
  ('-out:' + $exe), ('-win32icon:' + $icon))
# UIAutomation 系列不在框架根目录，在 WPF 子目录里，得给全路径
$wpf = Join-Path (Split-Path $csc) 'WPF'
foreach ($ref in @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll')) {
  $rp = Join-Path $wpf $ref
  if (-not (Test-Path $rp)) { throw "缺少 $rp（UIAutomation 引用）" }
  $cscArgs += ('-r:' + $rp)
}
$cscArgs += (Join-Path $Src 'Launcher.cs')
& $csc @cscArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw '编译失败' }
Write-Host "已编译 $exe"

$check = Join-Path $Repo '.qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1'
if (Test-Path $check) {
  & $check -Exe $exe -SrcDir $Src -MaxKB 60
  if ($LASTEXITCODE -ne 0) { throw '启动器 GUI 规范检查未通过' }
}

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$sha = Join-Path $Out 'game_starter.sha256'
[IO.File]::WriteAllText($sha, $hash + '  game_starter.exe' + "`r`n", [Text.Encoding]::ASCII)
Write-Host "已生成 game_starter.sha256：$hash"

if ($NoDeploy) { return }

$gbk = [System.Text.Encoding]::GetEncoding(936)
function Backup-File([string]$name) {
  $dst = Join-Path $GameDir $name
  $bak = Join-Path $Backup ($name + '.before-launcher')
  if ((Test-Path $dst) -and -not (Test-Path $bak)) { Copy-Item $dst $bak -Force }
}

# 离线环境三件套（原版启动器 / 游戏 / 运行时）只读不动，每次构建重新记一份指纹留档
$lines = @()
foreach ($keep in @('SG8Launcher.exe', 'Runtime\MiniPipeDll.dll', 'GAME\SG8.exe')) {
  $src2 = Join-Path $GameDir $keep
  if (Test-Path $src2) {
    $h = (Get-FileHash $src2 -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines += ($keep + '  ' + $h)
  }
}
[IO.File]::WriteAllLines((Join-Path $Backup 'untouched.sha256'), $lines, [Text.Encoding]::ASCII)

$text = [IO.File]::ReadAllText((Join-Path $Src 'game_starter.bat'), [Text.Encoding]::UTF8)
$text = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
Backup-File 'game_starter.bat'
[IO.File]::WriteAllBytes((Join-Path $GameDir 'game_starter.bat'), $gbk.GetBytes($text))
Write-Host '已部署 game_starter.bat (GBK+CRLF)'

$n = Join-Path $GameDir '_tools\nircmd'
New-Item -ItemType Directory -Force -Path $n | Out-Null
$nircmd = Join-Path $Repo 'tools\nircmd\nircmdc.exe'
if (Test-Path $nircmd) { Copy-Item $nircmd (Join-Path $n 'nircmdc.exe') -Force }

Backup-File 'game_starter.exe'
Backup-File 'launcher.ini'
Copy-Item $exe (Join-Path $GameDir 'game_starter.exe') -Force
Copy-Item (Join-Path $Src 'launcher.ini') (Join-Path $GameDir 'launcher.ini') -Force
Copy-Item $sha (Join-Path $GameDir 'game_starter.sha256') -Force

$bad = Get-ChildItem $GameDir -Include *.cs, *.ps1 -Recurse -ErrorAction SilentlyContinue
if ($bad) { throw ('目标目录出现源码文件：' + ($bad.FullName -join ', ')) }

Write-Host ''
Write-Host "部署完成：$GameDir（SG8Launcher.exe / GAME / Runtime 保持不动）"
Get-ChildItem $GameDir -File |
  Where-Object { $_.Name -match '^(game_starter\.(exe|bat|sha256)|launcher\.ini|运行日志\.txt)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
