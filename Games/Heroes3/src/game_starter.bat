@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=英雄无敌3：死亡阴影"
title %tl%
mode con: lines=34 cols=96 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "Heroes3.exe" (
  echo   找不到 Heroes3.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "ddraw.dll" (
  echo   缺少运行文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "_presets\ddraw.ini" (
  echo   缺少显示文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     英雄无敌3：死亡阴影  Heroes of Might and Magic III
  echo     New World Computing · v3.1 简体中文完整版
  echo   ------------------------------------------------------------
  echo     默认 GDI（最兼容），画面最稳。
  echo     黑屏/花屏时选 GDI 稳定全屏或窗口；
  echo     其他画面方式在本游戏容易出问题，别手动改。
  echo     想换窗口用启动器的「窗口模式」；多人用 IPX 或 TCP/IP。
  echo   ============================================================
  echo.
  echo     显示方式
  echo       1  稳定全屏（GDI，最兼容）
  echo       2  保持 4:3（左右黑边）
  echo       3  窗口模式（1280×720）
  echo       4  原版 640×480（窗口）
  echo.
  echo       5  打开运行日志
  echo       6  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-6]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" if not "%sel%"=="6" set "sel=1"
if "%sel%"=="5" (
  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"
  exit /b 0
)
if "%sel%"=="6" exit /b 0
set "preset=ddraw.ini"
if "%sel%"=="2" set "preset=ddraw-43.ini"
if "%sel%"=="3" set "preset=ddraw-window.ini"
if "%sel%"=="4" set "preset=ddraw-640.ini"
if not exist "_presets\%preset%" (
  echo   缺少显示文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if exist "兼容修复.bat" call "兼容修复.bat" /quiet >nul 2>&1
copy /y "_presets\%preset%" "ddraw.ini" >nul
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0Heroes3.exe"
set "rc=%ERRORLEVEL%"
copy /y "_presets\ddraw.ini" "ddraw.ini" >nul 2>&1
exit /b %rc%
