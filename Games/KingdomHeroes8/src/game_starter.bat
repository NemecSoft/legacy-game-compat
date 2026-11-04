@echo off&color 4f
setlocal EnableExtensions
set "tl=三国群英传 8"
title %tl%
mode con: lines=28 cols=88 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "SG8Launcher.exe" (
  echo   找不到包内离线启动器 SG8Launcher.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "GAME\SG8.exe" (
  echo   找不到游戏主程序 GAME\SG8.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     三国群英传 8
  echo     Kingdom Heroes 8 ^(SG8 / UserJoy^)
  echo   ------------------------------------------------------------
  echo     光荣式武将养成 + 千人战场的策略游戏。
  echo     首次进游戏会加载较久，请耐心等画面出现。
  echo   ------------------------------------------------------------
  echo     1  启动游戏（推荐：自动开始，左下角推广不显示）
  echo     2  启动游戏（只打开离线环境，自己点开始）
  echo     3  打开运行日志
  echo     4  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-4]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" set "sel=1"
if "%sel%"=="3" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="3" exit /b 0
if "%sel%"=="4" exit /b 0
set "flags=/go"
if "%sel%"=="2" set "flags=/noclick /go"
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0game_starter.exe" %flags%
set "rc=%ERRORLEVEL%"
exit /b %rc%
