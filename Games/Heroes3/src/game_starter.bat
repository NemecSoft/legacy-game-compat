@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=英雄无敌3：死亡阴影"
title %tl%
mode con: lines=34 cols=96 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
if not exist "Heroes3.exe" (
  echo   找不到 Heroes3.exe
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "ddraw.dll" (
  echo   缺少运行文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "_presets\ddraw.ini" (
  echo   缺少显示文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=6"
if /i not "%~1"=="/go" (
  cls
  echo.
  echo   ============================================================
  echo     英雄无敌3：死亡阴影  Heroes of Might and Magic III
  echo     New World Computing · v3.1 简体中文完整版
  echo   ------------------------------------------------------------
  echo     共 6 档，默认第 6 档 dgVoodoo2转换（无边框全屏）。
  echo     画面不对往上回：5 DX9硬件加速、1 软件画面 GDI，
  echo     要窗口选 3 或 4；多人用 IPX 或 TCP/IP。
  echo   ============================================================
  echo.
  echo     显示方式（直接回车 = 6）
  echo       1  软件画面 GDI（全屏）
  echo       2  软件画面 GDI（4:3黑边）
  echo       3  软件画面 GDI（窗口1280×720）
  echo       4  原版640×480（窗口）
  echo       5  DX9硬件加速（全屏）
  echo       6  dgVoodoo2转换（无边框全屏）
  echo.
  echo       7  打开运行日志
  echo       8  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-8]，直接回车 = 6: "
)
if not defined sel set "sel=6"
set "sel=%sel: =%"
if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" if not "%sel%"=="5" if not "%sel%"=="6" if not "%sel%"=="7" if not "%sel%"=="8" set "sel=6"
if "%sel%"=="7" (
  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"
  exit /b 0
)
if "%sel%"=="8" exit /b 0
rem 选哪条通道就顶哪一层的 ddraw.dll，配置也只留这一层的那一份
set "preset=ddraw.ini"
set "layer=_cnc-ddraw"
if "%sel%"=="2" set "preset=ddraw-43.ini"
if "%sel%"=="3" set "preset=ddraw-window.ini"
if "%sel%"=="4" set "preset=ddraw-640.ini"
if "%sel%"=="5" set "preset=ddraw-d3d9.ini"
if "%sel%"=="6" (set "preset=dgVoodoo-window.conf"&set "layer=_dgvoodoo2")
if not exist "_presets\%preset%" (
  echo   缺少显示文件，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if not exist "%layer%\ddraw.dll" (
  echo   缺少 %layer%\ddraw.dll，这个显示方式用不了
  if /i not "%~1"=="/go" pause
  exit /b 1
)
if exist "兼容修复.bat" call "兼容修复.bat" /quiet >nul 2>&1
copy /y "%layer%\ddraw.dll" "ddraw.dll" >nul
if "%layer%"=="_dgvoodoo2" (
  copy /y "_presets\%preset%" "dgVoodoo.conf" >nul
  del /q "ddraw.ini" >nul 2>&1
) else (
  copy /y "_presets\%preset%" "ddraw.ini" >nul
  del /q "dgVoodoo.conf" >nul 2>&1
)
echo.
echo   正在启动游戏，退出后本窗口自动关闭
echo.
"%~dp0Heroes3.exe"
set "rc=%ERRORLEVEL%"
rem 收尾回到默认的 cnc-ddraw GDI，不在机器上留两层叠着的现场
copy /y "_cnc-ddraw\ddraw.dll" "ddraw.dll" >nul 2>&1
copy /y "_presets\ddraw.ini" "ddraw.ini" >nul 2>&1
del /q "dgVoodoo.conf" >nul 2>&1
exit /b %rc%
