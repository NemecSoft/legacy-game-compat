@echo off&color 1f
title 恢复原版
cd /d "%~dp0"
del /q "{{LAYER_DLL}}" "dxvk.conf" >nul 2>&1
if exist "{{LAYER_DLL}}.{{BACKUP_SUFFIX}}" ren "{{LAYER_DLL}}.{{BACKUP_SUFFIX}}" "{{LAYER_DLL}}"
echo.
echo   已移除 {{LAYER_NAME}}，恢复原生设置
echo.
pause
