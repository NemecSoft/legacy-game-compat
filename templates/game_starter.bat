@echo off&color 4f
setlocal EnableExtensions
set "tl={{GAME_NAME}}"
title %tl%
mode con: lines=32 cols=92 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
:: 控制台版启动器：菜单项、顺序、默认值与 game_starter.exe 完全一致，选完自己启动游戏。
:: 这是正式入口，不是转发 GUI 的薄包装；平台零交互入口用 game_starter.bat /go。
if not exist "{{LAYER_DLL}}" if exist "_dxvk\{{LAYER_DLL}}" copy /y "_dxvk\{{LAYER_DLL}}" "{{LAYER_DLL}}" >nul
if not exist "{{LAYER_DLL}}" (
  echo.
  echo   缺少兼容层 {{LAYER_DLL}}，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     {{GAME_NAME}}
  echo     {{GAME_NAME_EN}}
  echo   ------------------------------------------------------------
  echo     画面：{{LAYER_DESC}}
  echo     {{EXTRA_HINT}}
  echo   ------------------------------------------------------------
  echo     1  启动游戏（默认）
  echo     2  启动游戏（备用配置）
  echo.
  echo     3  打开运行日志
  echo     4  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-4]，直接回车 = 1: "
)
if not defined sel set "sel=1"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" set "sel=1"
if "%sel%"=="3" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="3" exit /b 0
if "%sel%"=="4" exit /b 0
if "%sel%"=="2" (
  {{APPLY_OPTION2}}
) else (
  {{RESTORE_DEFAULT}}
)
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0{{GAME_EXE}}" >nul 2>&1
set "rc=%ERRORLEVEL%"
{{RESTORE_DEFAULT}}
exit /b %rc%
