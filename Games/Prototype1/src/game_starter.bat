@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=虐杀原形"
title %tl%
mode con: lines=28 cols=92 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "prototypef.exe" (
  echo   找不到 prototypef.exe
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
  echo     虐杀原形 Prototype
  echo     Radical Entertainment · 简体中文硬盘版
  echo   ------------------------------------------------------------
  echo     直接进游戏即可。
  echo.
  echo     分辨率、画质请进游戏「选项」里改。
  echo   ============================================================
  echo.
  echo       1  启动游戏（默认）
  echo       2  打开运行日志
  echo       3  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-3]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" set "sel=1"
if "%sel%"=="2" (
  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"
  exit /b 0
)
if "%sel%"=="3" exit /b 0
if exist "init.exe" "init.exe"
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
start "" /affinity 3ff /wait "prototypef.exe" >nul 2>&1
set "rc=%ERRORLEVEL%"
exit /b %rc%
