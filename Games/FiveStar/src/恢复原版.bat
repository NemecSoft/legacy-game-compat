@echo off&color 4f
title 恢复原版 - 绝命时刻：五星之光
cd /d "%~dp0"
del /q "d3d8.dll" "d3d9.dll" "dxvk.conf" "Game.dat_d3d9.log" "Game.dat.dxvk-cache" >nul 2>&1
if exist "d3d8.dll.原版-bak" ren "d3d8.dll.原版-bak" "d3d8.dll"
if exist "d3d9.dll.原版-bak" ren "d3d9.dll.原版-bak" "d3d9.dll"
echo.
echo   已移除兼容文件，恢复系统原生 D3D8/D3D9
echo   注册表和 Options.ini 保留（游戏仍需要）；如需清除请手动处理
echo.
pause
