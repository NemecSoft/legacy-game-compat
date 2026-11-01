@echo off&color 4f
title 恢复原版 - 暴力摩托
cd /d "%~dp0"
del /q "ddraw.dll" "ddraw.ini" "ddraw-stretch.ini" "ddraw-soft.ini" "ddraw-640.ini" "d3d9.dll" "dxvk.conf" "Roadrash_d3d9.log" >nul 2>&1
if exist "ddraw.dll.原版-bak" ren "ddraw.dll.原版-bak" "ddraw.dll"
if exist "d3d9.dll.原版-bak" ren "d3d9.dll.原版-bak" "d3d9.dll"
echo.
echo   已移除 cnc-ddraw / DXVK，游戏将使用系统原生 DirectDraw
echo   注意：Win10/11 原生 DirectDraw 可能再次弹出 DX 错误或闪退
echo.
pause
