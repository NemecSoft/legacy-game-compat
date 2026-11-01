@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=幽灵行动：未来战士"
title %tl%
mode con: lines=30 cols=94 >nul 2>&1
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
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     幽灵行动：未来战士 Ghost Recon Future Soldier
  echo     Ubisoft · v1.8 全 DLC · 简体中文版
  echo   ------------------------------------------------------------
  echo     启动方式：init.exe -^> Future Soldier.exe DX11
  echo     不运行 go.bat，不写 XP 兼容性/RUNASADMIN。
  echo.
  echo     修改器是 Themida 加壳；Win11 开了
  echo     内存完整性/VBS 会被反虚拟机检测拦住。
  echo     详见游戏目录里的「修改器说明.txt」。
  echo   ============================================================
  echo.
  echo       1  启动游戏（DX11，默认）
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
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0init.exe" >nul 2>&1
"%~dp0Future Soldier.exe" "Future Soldier DX11.exe"
set "rc=%ERRORLEVEL%"
exit /b %rc%
