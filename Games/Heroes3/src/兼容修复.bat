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
if exist "_dgvoodoo2\ddraw.dll" (
  copy /y "_dgvoodoo2\ddraw.dll" "ddraw.dll" >nul
  if exist "_presets\dgVoodoo-window.conf" copy /y "_presets\dgVoodoo-window.conf" "dgVoodoo.conf" >nul
  del "ddraw.ini" >/dev/null 2>&1
) else (
  copy /y "_cnc-ddraw\ddraw.dll" "ddraw.dll" >nul
  if exist "_presets\ddraw.ini" copy /y "_presets\ddraw.ini" "ddraw.ini" >nul
)

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
echo              另有 ddraw-d3d9.ini（DX9硬件加速）与 dgVoodoo-window.conf（dgVoodoo2）
if exist "_dgvoodoo2\ddraw.dll" (
  echo   - dgVoodoo2 层：_dgvoodoo2\ddraw.dll 已就位（默认档，当前就顶在 ddraw.dll）
) else (
  echo   - dgVoodoo2 层：缺 _dgvoodoo2\ddraw.dll，默认退回第 1 档软件画面 GDI
)
echo.
echo   注意：默认第 6 档 dgVoodoo2转换（无边框全屏）；画面不对在启动器里往上回。
if /i not "%~1"=="/quiet" pause
exit /b 0
