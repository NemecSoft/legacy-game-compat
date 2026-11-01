@echo off&color 4f
setlocal EnableExtensions
set "tl=恢复原版 - 黑鹰坠落"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
del /q "d3d8.dll" "d3d9.dll" "dxvk.conf" >nul 2>&1
del /q "dfbhd.dat_d3d9.log" "dfbhd.dat.dxvk-cache" >nul 2>&1
del /q "DFBHDLC_d3d9.log" "DFBHDLC.dxvk-cache" >nul 2>&1
echo.
echo   已移除 DXVK 兼容层（d3d8.dll / d3d9.dll / dxvk.conf 和运行日志）
echo   汉化外挂、汉化资源、游戏文件都没有动。
echo.
echo   提示：本机没有硬件 D3D8 替代，恢复后游戏可能无法进入；
echo         想再玩双击 兼容修复.bat 即可。
pause
