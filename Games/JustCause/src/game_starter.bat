@echo off&color 4f
setlocal EnableExtensions
set "tl=正当防卫 Just Cause"
title %tl%
mode con: lines=34 cols=92 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
:: 控制台版启动器：选项、顺序、默认值与 game_starter.exe 一致，自己启动游戏，不转发 GUI
:: 平台零交互入口：game_starter.bat /go（等价于「1 正常游玩」直接启动）
if not exist "d3d9.dll" if exist "_dxvk\d3d9.dll" copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
if not exist "d3d9.dll" (
  echo.
  echo   缺少运行文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "scripts\JCTextFix.asi" if exist "_backup\JCTextFix.asi" copy /y "_backup\JCTextFix.asi" "scripts\JCTextFix.asi" >nul
if not exist "scripts\JCTextFix.asi" (
  echo.
  echo   缺少中文文本修复 JCTextFix.asi，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     正当防卫 Just Cause
  echo     Avalanche Studios · Eidos 2006 · 简体中文汉化版
  echo   ------------------------------------------------------------
  echo     2006 年的开放世界动作游戏：空降到热带岛国，跳伞、抢车、
  echo     炸基地，任务提示和对话字幕都是中文。
  echo.
  echo     操作：W A S D 移动 / 鼠标 视角·射击 / E 互动·开伞 / Esc 菜单
  echo   ------------------------------------------------------------
  echo     显示
  echo       1  启动游戏（显示帧数，默认）
  echo       2  启动游戏（正常游玩，不显示帧数）
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
if "%sel%"=="3" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="3" exit /b 0
if "%sel%"=="4" exit /b 0
if exist "%~dp0game_starter.exe" (
  "%~dp0game_starter.exe" /fixreg
  if errorlevel 1 (
    echo   注册表写入失败，请右键「修复注册表.bat」以管理员身份运行
    if /i not "%~1"=="/go" pause
    exit /b 1
  )
)
if "%sel%"=="1" (
  >"dxvk.conf" echo # 由 game_starter.bat 按菜单选择生成：显示帧数 HUD
  >>"dxvk.conf" echo dxvk.hud = fps
) else (
  copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
)
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0JustCause.exe" >nul 2>&1
set "rc=%ERRORLEVEL%"
copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
exit /b %rc%
