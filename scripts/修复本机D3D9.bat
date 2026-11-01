@echo off&color 4f
title 修复本机 Direct3D 9
set "tl=修复本机 Direct3D 9"
echo.
echo  本工具只做一件事：清除让 D3D9 强制走软件渲染的全局开关。
echo  不改驱动、不装更新、不改系统文件；清除后直接启动游戏即可。
echo  开关来源：部分“DirectX 修复/优化”工具会写入它们。
echo.
echo  [1/6] HKLM 64 位 Direct3D\Drivers  SoftwareOnly
reg query "HKLM\SOFTWARE\Microsoft\Direct3D\Drivers" /v SoftwareOnly >nul 2>&1
if not errorlevel 1 (
  echo        发现，备份并删除...
  reg export "HKLM\SOFTWARE\Microsoft\Direct3D" "%TEMP%\D3D9-Direct3D64-backup.reg" /y >nul 2>&1
  reg delete "HKLM\SOFTWARE\Microsoft\Direct3D\Drivers" /v SoftwareOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [2/6] HKLM 32 位 Direct3D\Drivers  SoftwareOnly
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers" /v SoftwareOnly >nul 2>&1
if not errorlevel 1 (
  echo        发现，备份并删除...
  reg export "HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D" "%TEMP%\D3D9-Direct3D32-backup.reg" /y >nul 2>&1
  reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers" /v SoftwareOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [3/6] HKLM 64 位 DirectDraw  EmulationOnly
reg query "HKLM\SOFTWARE\Microsoft\DirectDraw" /v EmulationOnly >nul 2>&1
if not errorlevel 1 (
  echo        发现，备份并删除...
  reg export "HKLM\SOFTWARE\Microsoft\DirectDraw" "%TEMP%\D3D9-DirectDraw64-backup.reg" /y >nul 2>&1
  reg delete "HKLM\SOFTWARE\Microsoft\DirectDraw" /v EmulationOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [4/6] HKLM 32 位 DirectDraw  EmulationOnly
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\DirectDraw" /v EmulationOnly >nul 2>&1
if not errorlevel 1 (
  echo        发现，备份并删除...
  reg export "HKLM\SOFTWARE\WOW6432Node\Microsoft\DirectDraw" "%TEMP%\D3D9-DirectDraw32-backup.reg" /y >nul 2>&1
  reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\DirectDraw" /v EmulationOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [5/6] HKCU Direct3D\Drivers  SoftwareOnly
reg query "HKCU\SOFTWARE\Microsoft\Direct3D\Drivers" /v SoftwareOnly >nul 2>&1
if not errorlevel 1 (
  reg export "HKCU\SOFTWARE\Microsoft\Direct3D" "%TEMP%\D3D9-HKCU-Direct3D-backup.reg" /y >nul 2>&1
  reg delete "HKCU\SOFTWARE\Microsoft\Direct3D\Drivers" /v SoftwareOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [6/6] HKCU DirectDraw  EmulationOnly
reg query "HKCU\SOFTWARE\Microsoft\DirectDraw" /v EmulationOnly >nul 2>&1
if not errorlevel 1 (
  reg export "HKCU\SOFTWARE\Microsoft\DirectDraw" "%TEMP%\D3D9-HKCU-DirectDraw-backup.reg" /y >nul 2>&1
  reg delete "HKCU\SOFTWARE\Microsoft\DirectDraw" /v EmulationOnly /f >nul 2>&1
  echo        已删除
) else (
  echo        正常（未发现）
)
echo.
echo  [检测] 运行 32 位 D3D9 硬件设备探针（会短暂切换一次全屏）
if exist "%~dp0D3D9Probe32.exe" (
  "%~dp0D3D9Probe32.exe"
) else (
  echo        未找到 D3D9Probe32.exe（应与本脚本放在同一目录）
)
echo.
echo  结果说明：0x00000000 = 硬件 D3D9 正常
echo            0x8876086A = 仍不可用，需要继续排查驱动/系统
echo.
echo  清除后不需要重启，直接启动游戏即可。
echo.
pause
