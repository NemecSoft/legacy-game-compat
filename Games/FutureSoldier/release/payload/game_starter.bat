@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=幽灵行动：未来战士"
title %tl%
mode con: lines=34 cols=96 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "Future Soldier.exe" (
  echo   找不到 Future Soldier.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "Future Soldier DX11.exe" (
  echo   找不到 Future Soldier DX11.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i "%~1"=="/dx9" set "sel=2"
if /i "%~2"=="/dx9" set "sel=2"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     幽灵行动：未来战士 Ghost Recon Future Soldier
  echo     Ubisoft · v1.8 全 DLC · 简体中文版
  echo   ------------------------------------------------------------
  echo     默认 DX11，画质最好；老机器/进不去选 DX9。
  echo     DX9 模式只影响本次启动，不改游戏设置。
  echo.
  echo     修改器在 Win11 可能被安全功能拦住，
  echo     详见游戏目录里的「修改器说明.txt」。
  echo   ============================================================
  echo.
  echo     启动方式
  echo       1  DX11（画质最好，默认）
  echo       2  DX9（最兼容，老机器）
  echo.
  echo       3  打开运行日志
  echo       4  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-4]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" set "sel=1"
if "%sel%"=="3" (
  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"
  exit /b 0
)
if "%sel%"=="4" exit /b 0
if "%sel%"=="2" (
  if not exist "d3d9.dll" (
    echo   缺少 DX9 运行文件，请先运行 兼容修复.bat
    if /i not "%~1"=="/go" pause
    exit /b 1
  )
  if not exist "dxvk.conf" (
    echo   缺少 DX9 运行文件，请先运行 兼容修复.bat
    if /i not "%~1"=="/go" pause
    exit /b 1
  )
  echo.
  echo   正在以 DX9 启动，退出后本窗口自动关闭
  echo.
  "%~dp0init.exe" >nul 2>&1
  set "__COMPAT_LAYER=WINXPSP3"
  "%~dp0Future Soldier.exe" "Future Soldier DX9.exe"
  set "rc=%ERRORLEVEL%"
  set "__COMPAT_LAYER="
  exit /b %rc%
)
echo.
echo   正在以 DX11 原生启动，退出后本窗口自动关闭
echo.
"%~dp0init.exe" >nul 2>&1
"%~dp0Future Soldier.exe" "Future Soldier DX11.exe"
set "rc=%ERRORLEVEL%"
exit /b %rc%
