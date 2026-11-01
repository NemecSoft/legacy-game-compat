@echo off&color 4f
setlocal EnableExtensions
set "tl=兼容修复 - 虐杀原形"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
if not exist "_dxvk\d3d9.dll" (
  echo.
  echo   缺少 _dxvk\d3d9.dll，兼容包不完整
  pause
  exit /b 1
)
copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
echo.
echo   已启用 DXVK 兼容层（d3d9.dll）
if not exist "prototypef.exe" echo   警告：找不到 prototypef.exe
echo.
echo   双击 game_starter.exe 进游戏
pause
