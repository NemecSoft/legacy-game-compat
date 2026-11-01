@echo off&color 4f
title 兼容修复 - 反恐精英 1.6
cd /d "%~dp0"
if not exist "_mesa\opengl32.dll" echo   缺少 _mesa\opengl32.dll，无法修复&pause&exit /b 1
if not exist "_mesa\libgallium_wgl.dll" echo   缺少 _mesa\libgallium_wgl.dll，无法修复&pause&exit /b 1
if not exist "_mesa\dxil.dll" echo   缺少 _mesa\dxil.dll，无法修复&pause&exit /b 1
copy /y "_mesa\opengl32.dll" "opengl32.dll" >nul
copy /y "_mesa\libgallium_wgl.dll" "libgallium_wgl.dll" >nul
copy /y "_mesa\dxil.dll" "dxil.dll" >nul
if exist "_mesa\CSOverlay.exe" copy /y "_mesa\CSOverlay.exe" "CSOverlay.exe" >nul
echo.
echo   已启用兼容文件
echo   双击 game_starter.exe 或 game_starter.bat 进游戏
echo.
pause
