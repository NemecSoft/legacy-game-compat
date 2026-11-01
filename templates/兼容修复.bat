@echo off&color 1f
title 兼容修复
cd /d "%~dp0"
if not exist "_dxvk\{{LAYER_DLL}}" echo   缺少 _dxvk\{{LAYER_DLL}}&pause&exit /b
if exist "{{LAYER_DLL}}" if not exist "{{LAYER_DLL}}.{{BACKUP_SUFFIX}}" ren "{{LAYER_DLL}}" "{{LAYER_DLL}}.{{BACKUP_SUFFIX}}"
copy /y "_dxvk\{{LAYER_DLL}}" "{{LAYER_DLL}}" >nul
if not exist "dxvk.conf" >"dxvk.conf" echo d3d9.presentInterval = 1
echo.
echo   已启用 {{LAYER_NAME}}
echo   双击 game_starter.exe 进游戏
echo.
pause
