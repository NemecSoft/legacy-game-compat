@echo off&color 1f
setlocal
set "tl={{GAME_NAME}}"
title %tl%
mode con: lines=25 cols=63 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
cls
echo.============================================================
echo.        {{GAME_NAME}}
echo.        {{GAME_NAME_EN}}
echo.============================================================
echo.画面：{{LAYER_DESC}}
echo.黑屏或白屏：先跑一次 兼容修复.bat
echo.============================================================
echo.{{EXTRA_HINT}}
echo.
echo.游戏运行中，退出后本窗口自动关闭
echo.
start "" "%~dp0{{LOADER_EXE}}"
powershell -NoProfile -Command "$t=0; while (-not (Get-Process | Where-Object { $_.ProcessName -like '{{PROC_NAME}}*' }) -and $t -lt 120) { Start-Sleep -Milliseconds 500; $t++ }; while (Get-Process | Where-Object { $_.ProcessName -like '{{PROC_NAME}}*' }) { Start-Sleep -Seconds 2 }"
