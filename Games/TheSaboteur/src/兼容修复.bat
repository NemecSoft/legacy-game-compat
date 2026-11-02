@echo off&color 4f
setlocal EnableExtensions
set "tl=兼容修复 - 破坏者"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
if not exist "_dxvk\d3d9.dll" (
  echo.
  echo   缺少 _dxvk\d3d9.dll，兼容包不完整
  pause
  exit /b 1
)
if exist "d3d9.dll" if not exist "d3d9.dll.reshade-bak" copy /y "d3d9.dll" "d3d9.dll.reshade-bak" >nul
copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
if exist "_dxvk\dxvk.conf" copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
echo.
echo   已启用 DXVK 2.5.3（D3D9 转 Vulkan，HUD=version,api,fps）
echo   双击 game_starter.exe 进游戏
pause
