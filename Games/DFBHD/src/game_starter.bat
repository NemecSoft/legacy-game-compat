@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=三角洲特种部队：黑鹰坠落 - 军刀部队"
title %tl%
mode con: lines=34 cols=96 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
:: 控制台版启动器：选项、顺序、默认值与 game_starter.exe 一致，自己启动游戏，不转发 GUI
:: 零交互入口：game_starter.bat /go（默认 1600×1200 全屏）
if not exist "d3d8.dll" if exist "_dxvk\d3d8.dll" copy /y "_dxvk\d3d8.dll" "d3d8.dll" >nul
if not exist "d3d9.dll" if exist "_dxvk\d3d9.dll" copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
if not exist "dxvk.conf" if exist "_dxvk\dxvk.conf" copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
if not exist "d3d8.dll" (
  echo.
  echo   缺少兼容文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "汉化外挂.exe" (
  echo.
  echo   缺少汉化外挂，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "LG_Data\LG_DF_BHD_TS.dll" (
  echo.
  echo   缺少汉化字体，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     三角洲特种部队：黑鹰坠落 - 军刀部队
  echo     NovaLogic · 2003 · 简体中文硬盘版
  echo   ------------------------------------------------------------
  echo     战术射击：单人战役、局域网联机，中文界面。
  echo.
  echo     操作：W A S D 移动 / 鼠标 视角·开火 / X 蹲 / Z 趴
  echo           C 站 / R 换弹 / 1 2 3 换武器 / B 望远镜 / N 夜视
  echo   ------------------------------------------------------------
  echo     画面 / 分辨率
  echo       1  1600×1200 全屏（默认）
  echo       2  1280×1024 全屏
  echo       3  1024×768 全屏
  echo       4  800×600 全屏
  echo       5  640×480 全屏
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
set "GR=8"
set "RES=1600x1200"
if "%sel%"=="2" (
  set "GR=7"
  set "RES=1280x1024"
)
if "%sel%"=="3" (
  set "GR=6"
  set "RES=1024x768"
)
if "%sel%"=="4" (
  set "GR=5"
  set "RES=800x600"
)
if "%sel%"=="5" (
  set "GR=4"
  set "RES=640x480"
)
if exist "dfv.cfg" powershell -NoProfile -Command "$p='dfv.cfg'; $t=Get-Content $p -Raw; $t=$t -replace 'game_res\s*=\s*\d+','game_res = %GR%'; $t=$t -replace 'windowed\s*=\s*\d+','windowed = 0'; $t=$t -replace 'hw3d_res%RES%\s*=\s*\d+','hw3d_res%RES% = 1'; $t=$t -replace 'hw3d_res1600x1200\s*=\s*\d+','hw3d_res1600x1200 = 1'; Set-Content $p $t -NoNewline"
echo.
echo   正在启动，分辨率 %RES%；游戏退出后本窗口自动关闭
echo.
start "" "%~dp0汉化外挂.exe"
rem 汉化外挂拉起 dfbhd.dat 后自己陪跑；这里等游戏进程并回传游戏的退出码
powershell -NoProfile -Command "$t=0; while(-not (Get-Process | Where-Object { $_.ProcessName -like 'dfbhd*' }) -and $t -lt 120) { Start-Sleep -Milliseconds 500; $t++ }; $p=Get-Process | Where-Object { $_.ProcessName -like 'dfbhd*' } | Select-Object -First 1; if($p){ $p.WaitForExit(); exit $p.ExitCode }; exit 1"
set "rc=%ERRORLEVEL%"
exit /b %rc%
