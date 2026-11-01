@echo off&color 4f
setlocal
set "tl=暴力摩托 切键 - 系统默认"
title %tl%
mode con: lines=24 cols=63 >nul 2>&1
cd /d "%~dp0"
set "rc=0"
start "" /wait "%~dp0game_starter.exe" /keymap 3 >nul 2>&1
if errorlevel 1 set "rc=1"
cls
if "%rc%"=="1" echo   写入失败：需要管理员权限（请同意 UAC）或缺少 game_starter.exe。
if "%rc%"=="1" echo.
if "%rc%"=="1" pause
if "%rc%"=="1" exit /b
echo.============================================================
echo.             已选择 系统默认按键
echo.============================================================
echo.  前进 Up          刹车 Down
echo.  转向 Left / Right
echo.  倾斜 Home / Page Up
echo.  加速 n           拳打 Insert
echo.  脚踢 Enter       重击 Space
echo.  下车 d
echo.============================================================
echo.  这是游戏“使用键盘默认设置”里的原始方案。
echo.  启动器会记住本次选择，之后直接开 game_starter.bat / game_starter.exe 即可。
echo.  在游戏“按键设置”里改键会被下次启动覆盖。
echo.
pause
