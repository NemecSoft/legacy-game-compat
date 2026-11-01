@echo off&color 4f
setlocal
set "tl=暴力摩托 切键 - WASD"
title %tl%
mode con: lines=24 cols=63 >nul 2>&1
cd /d "%~dp0"
set "rc=0"
start "" /wait "%~dp0game_starter.exe" /keymap 2 >nul 2>&1
if errorlevel 1 set "rc=1"
cls
if "%rc%"=="1" echo   写入失败：需要管理员权限（请同意 UAC）或缺少 game_starter.exe。
if "%rc%"=="1" echo.
if "%rc%"=="1" pause
if "%rc%"=="1" exit /b
echo.============================================================
echo.             已选择 拳皇式 WASD 按键
echo.============================================================
echo.  前进 W         刹车 S
echo.  转向 A / D     倾斜 Q / E
echo.  加速 Shift     拳打 J
echo.  脚踢 U         重击 K
echo.  下车 X
echo.============================================================
echo.  左手 WASD 开车，右手 J/U/K 打人，Shift 加速，X 下车。
echo.  左右 Shift 都可以；启动器会记住本次选择。
echo.  在游戏“按键设置”里改键会被下次启动覆盖。
echo.
pause
