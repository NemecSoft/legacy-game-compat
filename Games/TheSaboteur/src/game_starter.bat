@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=破坏者 The Saboteur"
title %tl%
mode con: lines=32 cols=92 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "Saboteur.exe" (
  echo   找不到 Saboteur.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "d3d9.dll" if exist "_dxvk\d3d9.dll" copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
if not exist "d3d9.dll" (
  echo   缺少兼容文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     破坏者 The Saboteur
  echo     Pandemic · 2009 · 简体中文硬盘版
  echo   ------------------------------------------------------------
  echo     分辨率/画质在游戏内选项里也能改。
  echo   ============================================================
  echo.
  echo     画面 / 分辨率
  echo       1  1920×1080 全屏（默认）
  echo       2  1600×900 全屏
  echo       3  1280×720 全屏
  echo       4  1024×768 全屏
  echo       5  800×600 全屏
  echo.
  echo       6  打开运行日志
  echo       7  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-7]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" if not "%sel%"=="6" if not "%sel%"=="7" set "sel=1"
if "%sel%"=="6" (
  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"
  exit /b 0
)
if "%sel%"=="7" exit /b 0
"%~dp0game_starter.exe" /setres %sel%
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
set "DXVK_LOG_LEVEL=none"
"%~dp0Saboteur.exe"
set "rc=%ERRORLEVEL%"
exit /b %rc%
