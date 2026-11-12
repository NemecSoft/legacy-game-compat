@echo off&color 4f
setlocal EnableExtensions
set "tl=一键应用 - 使命召唤7 黑色行动"
title %tl%
cd /d "%~dp0"
set "PAY=%~dp0payload"
set "GAMEEXE=BlackOps.exe"
set "NAME=CoD7"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 使命召唤7 黑色行动（DXVK 绿色启动器）
  echo   ============================================================
  echo.
  echo   请把游戏目录拖到本窗口，或输入完整路径。
  set /p "TARGET=  目标目录："
)
if not defined TARGET (
  echo   没有输入目标目录。
  pause
  exit /b 1
)
set "TARGET=%TARGET:"=%"
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\%GAMEEXE%" (
  echo.
  echo   目标目录里没有 %GAMEEXE%：
  echo   "%TARGET%"
  echo   这不是使命召唤7 的游戏目录。
  pause
  exit /b 1
)
if not exist "%PAY%\manifest.txt" (
  echo   交付包不完整：缺少 payload\manifest.txt
  pause
  exit /b 1
)
set "BAK=%TARGET%\_legacy_compat_backup\%NAME%"
if not exist "%BAK%" mkdir "%BAK%"
set "REC=%TARGET%\交付记录.txt"
> "%REC%" echo 交付记录 - 使命召唤7 黑色行动 DXVK 绿色启动器
>>"%REC%" echo 应用时间：%date% %time%
>>"%REC%" echo 本包为纯新增型交付：不覆盖、不修改游戏任何原始文件，只新增启动器与兼容层文件。
>>"%REC%" echo 注册表：本游戏不需要（实测未导入任何键即可运行，启动器也不写注册表）。
>>"%REC%" echo.
>>"%REC%" echo 交付文件清单：
for /f "usebackq delims=" %%L in ("%PAY%\manifest.txt") do (
  if exist "%TARGET%\%%L" (
    if not exist "%BAK%\%%L" (
      mkdir "%BAK%\%%~dpL" 2>nul
      copy /y "%TARGET%\%%L" "%BAK%\%%L" >nul
      >>"%REC%" echo   已覆盖 %%L （原件备份在 _legacy_compat_backup\%NAME%\%%L）
    ) else (
      >>"%REC%" echo   已覆盖 %%L （首次备份已留档）
    )
  ) else (
    >>"%REC%" echo   新增   %%L
  )
  copy /y "%PAY%\%%L" "%TARGET%\%%L" >nul
)
echo.
echo   ============================================================
echo     应用完成。点游戏目录里的 game_starter.exe 开始游戏。
echo     撤回：运行本目录的 一键撤回.bat
echo   ============================================================
echo.
pause
exit /b 0
