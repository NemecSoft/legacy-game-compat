@echo off&color 4f
title 兼容修复 - 绝命时刻：五星之光
cd /d "%~dp0"
if not exist "_dxvk\d3d8.dll" echo   缺少 _dxvk\d3d8.dll，无法修复&pause&exit /b
if not exist "_dxvk\d3d9.dll" echo   缺少 _dxvk\d3d9.dll，无法修复&pause&exit /b

copy /y "_dxvk\d3d8.dll" "d3d8.dll" >nul
copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
if exist "game_starter.exe" (
  "game_starter.exe" /fixreg
  if errorlevel 1 echo   注册表写入失败，请右键本文件「以管理员身份运行」。
)
echo.
echo   已启用 DXVK 2.5.3（D3D8 转 Vulkan，左上角只显示帧数）
echo   已检查/写入五星之光注册表
echo   双击 game_starter.exe 进游戏
echo.
pause
