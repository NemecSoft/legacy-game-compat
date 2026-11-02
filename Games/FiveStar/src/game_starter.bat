@echo off&color 4f
setlocal EnableExtensions
set "tl=绝命时刻：五星之光"
title %tl%
mode con: lines=34 cols=92 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "generals.exe" (
  echo   找不到 generals.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "d3d8.dll" if exist "_dxvk\d3d8.dll" copy /y "_dxvk\d3d8.dll" "d3d8.dll" >nul
if not exist "d3d8.dll" (
  echo   缺少运行文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
set "gam=3"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     绝命时刻：五星之光
  echo     Command ^& Conquer: Generals - Zero Hour 国产模组
  echo   ------------------------------------------------------------
  echo     经典即时战略：中国阵营、单位、战役和地图。
  echo     左键选择，右键移动/攻击。
  echo   ------------------------------------------------------------
  echo     画面方案
  echo       1  1920x1080 高画质（推荐，约 60 帧）
  echo       2  1920x1080 中画质
  echo       3  1600x900 高画质
  echo       4  1280x720 中画质
  echo       5  1024x768 低画质
  echo       6  800x600 低画质
  echo.
  echo       7  算号并注册
  echo       8  打开运行日志
  echo       9  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择画面方案 [1-9]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" if not "%sel%"=="6" if not "%sel%"=="7" if not "%sel%"=="8" if not "%sel%"=="9" set "sel=1"
if "%sel%"=="7" (
  "%~dp0game_starter.exe" /fixreg /newserial
  if errorlevel 1 echo   注册失败，请右键本文件「以管理员身份运行」。
  if /i not "%~1"=="/go" pause
  exit /b 0
)
if "%sel%"=="8" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="8" exit /b 0
if "%sel%"=="9" exit /b 0
if /i not "%~1"=="/go" (
  echo.
  set /p "gam=  亮度 [1-5]，1=20 2=25 3=30（推荐） 4=35 5=40，回车 = 3: "
)
if not defined gam set "gam=3"
set "gam=%gam: =%"
if not "%gam%"=="1" if not "%gam%"=="2" if not "%gam%"=="3" if not "%gam%"=="4" if not "%gam%"=="5" set "gam=3"
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0game_starter.exe" /preset %sel% /gamma %gam% /go
set "rc=%ERRORLEVEL%"
exit /b %rc%
