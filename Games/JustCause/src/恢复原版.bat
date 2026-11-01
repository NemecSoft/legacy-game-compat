@echo off&color 4f
title 恢复原版 - 正当防卫
cd /d "%~dp0"
del /q "d3d9.dll" "dxvk.conf" "JustCause_d3d9.log" >nul 2>&1
if exist "d3d9.dll.原版-bak" ren "d3d9.dll.原版-bak" "d3d9.dll"
if exist "scripts\JCTextFix.asi" del /q "scripts\JCTextFix.asi" >nul
if exist "_backup\JCSubtitleOverlay.asi" copy /y "_backup\JCSubtitleOverlay.asi" "scripts\JCSubtitleOverlay.asi" >nul
echo.
echo   已移除 DXVK 兼容层和中文文本修复，汉化补丁恢复成原来的文件
echo   注意：本机原生 Direct3D9 不可用，恢复后游戏会再次报顶点着色器错误
echo.
pause
