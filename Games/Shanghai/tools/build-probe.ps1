# 编译 D3D8 探针（32 位）。项目标准构建入口：pwsh -NoProfile -File tools\build-probe.ps1
# 探针只建一个窗口模式的 D3D8 设备，不启动游戏、不改桌面分辨率。
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }
$src = Join-Path $dir 'D3D8Probe.cs'
$out = Join-Path $dir 'probe\native\D3D8Probe.exe'
& $csc -nologo -platform:x86 -out:$out $src
if ($LASTEXITCODE -ne 0) { throw "csc 编译失败，退出码 $LASTEXITCODE" }
if (-not (Test-Path $out)) { throw '编译产物不存在' }
Write-Output "编译完成：$out"
