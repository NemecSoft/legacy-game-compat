@echo off&color 4f
setlocal EnableExtensions
set "tl=战神III 重制版"
title %tl%
mode con: lines=30 cols=88 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "SpartanPort.exe" (
  echo   找不到 SpartanPort.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     战神III 重制版
  echo     God of War III Remastered ^(SpartanPort^)
  echo   ------------------------------------------------------------
  echo     奎托斯向众神复仇的动作冒险经典。
  echo     首次启动会准备着色器，需要等一两分钟。
  echo   ------------------------------------------------------------
  echo     1  启动游戏（推荐：自动检测画面）
  echo     2  启动游戏（画面异常时：固定桌面分辨率）
  echo     3  修复画面
  echo     4  打开运行日志
  echo     5  退出
  echo   ------------------------------------------------------------
  echo     按键（纯键盘）：J 轻击 K 重击 L 抓取 空格 跳 Q 防御 E 链刃
  echo                    R 瞄准 T 斯巴达之怒 1234 换武器 方向键 翻滚
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-5]，直接回车 = 1: "
)
if not defined sel set "sel=1"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" set "sel=1"
if "%sel%"=="3" (
  "%~dp0game_starter.exe" /fixdisplay
  if errorlevel 1 (
    echo   修复失败，请打开运行日志查看原因。
  ) else (
    echo   画面已改为无边框全屏、桌面原生分辨率。
  )
  if /i not "%~1"=="/go" pause
  exit /b 0
)
if "%sel%"=="4" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="4" exit /b 0
if "%sel%"=="5" exit /b 0
set "flags=/go"
if "%sel%"=="2" set "flags=/desktop /go"
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0game_starter.exe" %flags%
set "rc=%ERRORLEVEL%"
exit /b %rc%
