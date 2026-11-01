@echo off&color 4f
title 兼容修复 - 正当防卫
cd /d "%~dp0"
if not exist "_dxvk\d3d9.dll" echo   缺少 _dxvk\d3d9.dll，无法修复&pause&exit /b
if not exist "_backup\JCTextFix.asi" echo   缺少 _backup\JCTextFix.asi，无法修复&pause&exit /b
if exist "d3d9.dll" if not exist "d3d9.dll.原版-bak" ren "d3d9.dll" "d3d9.dll.原版-bak"
copy /y "_dxvk\d3d9.dll" "d3d9.dll" >nul
copy /y "_dxvk\dxvk.conf" "dxvk.conf" >nul
if not exist "scripts" mkdir "scripts"
if exist "scripts\JCSubtitleOverlay.asi" if not exist "_backup\JCSubtitleOverlay.asi" copy /y "scripts\JCSubtitleOverlay.asi" "_backup\JCSubtitleOverlay.asi" >nul
if exist "scripts\JCSubtitleOverlay.asi" del /q "scripts\JCSubtitleOverlay.asi" >nul
copy /y "_backup\JCTextFix.asi" "scripts\JCTextFix.asi" >nul
echo.
echo   已启用 DXVK 2.5.3 渲染 + 中文文本修复（JCTextFix.asi）
echo   汉化补丁本体（Archives / dinput8.dll / 其它 asi）保持不动
echo   双击 game_starter.exe 或 game_starter.bat 进游戏
echo.
pause
