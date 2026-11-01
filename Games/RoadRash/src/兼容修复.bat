@echo off&color 4f
title 兼容修复 - 暴力摩托
cd /d "%~dp0"
if not exist "_cnc-ddraw\ddraw.dll" echo   缺少 _cnc-ddraw\ddraw.dll，无法修复&pause&exit /b
if exist "ddraw.dll" if not exist "ddraw.dll.原版-bak" ren "ddraw.dll" "ddraw.dll.原版-bak"
if exist "d3d9.dll" if not exist "d3d9.dll.原版-bak" ren "d3d9.dll" "d3d9.dll.原版-bak"
copy /y "_cnc-ddraw\ddraw.dll" "ddraw.dll" >nul
copy /y "_cnc-ddraw\ddraw.ini" "ddraw.ini" >nul
if exist "_cnc-ddraw\ddraw-stretch.ini" copy /y "_cnc-ddraw\ddraw-stretch.ini" "ddraw-stretch.ini" >nul
if exist "_cnc-ddraw\ddraw-soft.ini" copy /y "_cnc-ddraw\ddraw-soft.ini" "ddraw-soft.ini" >nul
if exist "_cnc-ddraw\ddraw-640.ini" copy /y "_cnc-ddraw\ddraw-640.ini" "ddraw-640.ini" >nul
if exist "_cnc-ddraw\d3d9.dll" copy /y "_cnc-ddraw\d3d9.dll" "d3d9.dll" >nul
if exist "_cnc-ddraw\dxvk.conf" copy /y "_cnc-ddraw\dxvk.conf" "dxvk.conf" >nul
echo.
echo   已启用 cnc-ddraw 7.1 + DXVK 2.5.3
echo   启动后可选：兼容 1080p / 拉伸 / 纯软件 / 原版 640
echo   双击 game_starter.bat 或 game_starter.exe 进游戏
echo.
pause
