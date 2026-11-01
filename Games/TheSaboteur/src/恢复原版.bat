@echo off&color 4f
setlocal EnableExtensions
set "tl=恢复原版 - 破坏者"
title %tl%
mode con: lines=24 cols=82 >nul 2>&1
cd /d "%~dp0"
del /q "d3d9.dll" "dxvk.conf" >nul 2>&1
if exist "d3d9.dll.reshade-bak" ren "d3d9.dll.reshade-bak" "d3d9.dll"
if exist "ReShade.ini.bak" ren "ReShade.ini.bak" "ReShade.ini"
echo.
echo   已恢复原版（ReShade），DXVK 已移除
echo   游戏文件、存档都没有动。
pause
