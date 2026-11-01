@echo off&color 4f
setlocal EnableExtensions
set "tl=恢复原版 - 虐杀原形"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
del /q "d3d9.dll" "dxvk.conf" >nul 2>&1
echo.
echo   已移除 DXVK 兼容层（d3d9.dll / dxvk.conf）
echo   游戏文件、汉化、存档都没有动。
echo.
echo   提示：本机原生 D3D9 已修复，可直接试原生；想再玩兼容层
echo         双击 兼容修复.bat 即可。
pause
