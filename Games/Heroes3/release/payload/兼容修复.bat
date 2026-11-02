@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=兼容修复 - 英雄无敌3"
title %tl%
mode con: lines=26 cols=96 >nul 2>&1
cd /d "%~dp0"

if not exist "_cnc-ddraw\ddraw.dll" (
  echo.
  echo   缺少 _cnc-ddraw\ddraw.dll，兼容包不完整
  if /i not "%~1"=="/quiet" pause
  exit /b 1
)
if not exist "ddraw.dll" copy /y "_cnc-ddraw\ddraw.dll" "ddraw.dll" >nul
if not exist "ddraw.ini" if exist "_presets\ddraw.ini" copy /y "_presets\ddraw.ini" "ddraw.ini" >nul

set "GAMEDIR=%~dp0"
set "GAMEDRIVE=%~d0"
set "GAME=%GAMEDIR%Heroes3.exe"
set "R=%TEMP%\h3-compat.reg"
set "LOG=%TEMP%\h3-compat.log"
echo.  正在设置游戏注册表...
>"%R%" echo Windows Registry Editor Version 5.00
>>"%R%" echo.
>>"%R%" echo [HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\New World Computing\Heroes of Might and Magic?III\1.0]
>>"%R%" echo "AppPath"="%GAMEDIR:\=\\%"
>>"%R%" echo "CDDrive"="%GAMEDRIVE%"
>>"%R%" echo "Show Intro"=dword:00000001
>>"%R%" echo "Test Decomp"=dword:00000000
>>"%R%" echo "Test Read"=dword:00000000
>>"%R%" echo "Test Blit"=dword:00000000
reg import "%R%" >"%LOG%" 2>&1
if errorlevel 1 (
  echo.  注册表没设置成功；当前用户可能没有管理员权限。
  echo.  游戏仍会尝试启动；如果报错，右键本文件「以管理员身份运行」。
) else (
  echo.  注册表已设置。
)
del "%R%" >nul 2>&1
echo.
echo   已检查 cnc-ddraw 兼容层：
echo   - ddraw.dll：存在
echo   - 显示预设：_presets\ddraw.ini / ddraw-43.ini / ddraw-window.ini / ddraw-640.ini
echo.
echo   注意：本游戏只建议 GDI（最兼容）画面方式。
if /i not "%~1"=="/quiet" pause
exit /b 0
