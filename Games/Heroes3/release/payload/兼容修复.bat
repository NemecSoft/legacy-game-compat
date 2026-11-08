@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
rem 代码页钉死 936：从 UTF-8 控制台（pwsh 7）调用时 cmd 会按 65001 解码 GBK 字节，中文提示全乱码。
chcp 936 >nul
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
  if exist "ddraw.ini" del "ddraw.ini" >nul 2>&1
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
  echo.  注册表这次没写进去（游戏目录在映射盘上时常见）。
  echo.  不影响使用：启动器每次启动都会自己再写一次，写不成也只记一行日志。
) else (
  echo.  注册表已设置。
)
del "%R%" >nul 2>&1
echo.
if exist "_dgvoodoo2\ddraw.dll" (
  echo   已挂载 dgVoodoo2（就是启动器默认的 6 档：无边框全屏）
  echo   - ddraw.dll = _dgvoodoo2\ddraw.dll，dgVoodoo.conf = _presets\dgVoodoo-window.conf
) else (
  echo   已挂载 cnc-ddraw（包里没有 dgVoodoo2 层时的退路）
  echo   - ddraw.dll = _cnc-ddraw\ddraw.dll，ddraw.ini = _presets\ddraw.ini
)
echo.
echo   显示预设全在 _presets\：1-4 软件画面 GDI、5 DX9硬件加速、6 dgVoodoo2转换。
echo   画面不对就在启动器里换一档再点「启动游戏」，不用跑这个文件。
echo   另外：dgVoodoo2 全屏那一变体实测报 DDERR_INVALIDRECT / WINGRAPH.CPP:456，已判死，不要再试。
if /i not "%~1"=="/quiet" pause
exit /b 0
