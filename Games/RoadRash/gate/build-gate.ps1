# 编译 RoadRash 启动门禁代理 DLL（32 位）。
# 不调用 vcvarsall.bat：它内部会跑 reg.exe，在受限环境里会被拦；这里直接设好 INCLUDE/LIB 再调 cl.exe。
# 产物 AWEMan32.dll 需配合：游戏目录里把原始 AWEMan32.dll 改名成 AWEMan32_real.dll。
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# 这两台机器上 VS / SDK 的实际安装路径；换机器照实改这两个根路径即可。
$vcRoot  = 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207'
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10'
$sdkVer  = '10.0.26100.0'
$cl      = Join-Path $vcRoot 'bin\Hostx86\x86\cl.exe'

if (-not (Test-Path $cl)) { throw "找不到 x86 cl.exe：$cl（改脚本里的 vcRoot / sdkVer）" }

$env:INCLUDE = @(
  (Join-Path $vcRoot 'include'),
  (Join-Path $sdkRoot "Include\$sdkVer\um"),
  (Join-Path $sdkRoot "Include\$sdkVer\ucrt"),
  (Join-Path $sdkRoot "Include\$sdkVer\shared")
) -join ';'
$env:LIB = @(
  (Join-Path $vcRoot 'lib\x86'),
  (Join-Path $sdkRoot "Lib\$sdkVer\um\x86"),
  (Join-Path $sdkRoot "Lib\$sdkVer\ucrt\x86")
) -join ';'

& $cl /nologo /LD /O1 /MT gate.c /link /DEF:gate.def /OUT:AWEMan32.dll /MACHINE:X86 /SUBSYSTEM:WINDOWS
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $PSScriptRoot 'AWEMan32.dll'))) { throw '编译失败' }

$dll = Get-Item (Join-Path $PSScriptRoot 'AWEMan32.dll')
Write-Host ("OK 生成 {0} ({1} 字节)" -f $dll.Name, $dll.Length)
