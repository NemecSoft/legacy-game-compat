@echo off&color 4f
setlocal EnableExtensions
set "tl=鬼武者：剑之道"
title %tl%
mode con: lines=30 cols=88 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "OnimushaWotS.exe" (
  echo   找不到 OnimushaWotS.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     鬼武者：剑之道
  echo     Onimusha: Way of the Sword
  echo   ------------------------------------------------------------
  echo     战国动作游戏，以宫本武藏为主角。
  echo     游戏会自动检测画面，宽屏和 2K 自动适配。
  echo   ------------------------------------------------------------
  echo     1  游戏自动检测（推荐）
  echo     2  画面异常时（固定桌面分辨率）
  echo     3  修复分辨率
  echo     4  打开运行日志
  echo     5  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-5]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" set "sel=1"
if "%sel%"=="3" (
  "%~dp0game_starter.exe" /fixres
  if errorlevel 1 (
    echo   修复失败，请点「打开运行日志」查看原因。
  ) else (
    echo   分辨率已按当前桌面分辨率修复。
  )
  if /i not "%~1"=="/go" pause
  exit /b 0
)
if "%sel%"=="4" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="4" exit /b 0
if "%sel%"=="5" exit /b 0
set "mode="
if "%sel%"=="2" set "mode=/desktop"
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0game_starter.exe" %mode% /go
set "rc=%ERRORLEVEL%"
exit /b %rc%
