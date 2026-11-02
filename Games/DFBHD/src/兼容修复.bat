@echo off&color 4f
setlocal EnableExtensions
set "tl=兼容修复 - 黑鹰坠落"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
if not exist "_dxvk\d3d8.dll" (
  echo.
  echo   缺少 _dxvk\d3d8.dll，兼容包不完整
  pause
  exit /b 1
)
copy /y "_dxvk\d3d8.dll" "d3d8.dll" >nul
copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
if exist "_dxvk\dxvk.conf" copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
echo.
echo   已启用 DXVK 2.5.3（D3D8 转 Vulkan，左上角只显示帧数）
if not exist "汉化外挂.exe" echo   警告：缺少 汉化外挂.exe
if not exist "LG_Data\LG_DF_BHD_TS.dll" echo   警告：缺少 LG_Data\LG_DF_BHD_TS.dll
echo.
echo   双击 game_starter.exe 进游戏
pause
