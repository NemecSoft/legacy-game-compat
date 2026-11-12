@echo off&color 4f
chcp 936 >nul
setlocal EnableExtensions
set "tl=一键应用 - 使命召唤8 现代战争3 · DXVK 兼容层交付"
title %tl%
cd /d "%~dp0"
set "PAY=%~dp0payload"
set "GAMEEXE=iw5sp.exe"
set "NAME=CoD8"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 使命召唤8 现代战争3 DXVK 兼容层
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
  echo   目标目录里没有 %GAMEEXE%，
  echo   "%TARGET%"
  echo   这不是使命召唤8 单人版的游戏目录。
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
> "%REC%" echo 交付记录 - 使命召唤8 现代战争3 DXVK 兼容层
>>"%REC%" echo 应用时间：%date% %time%
>>"%REC%" echo 本包为绿色交付：不改注册表、不装驱动、不修改游戏任何原始文件，
>>"%REC%" echo 只新增/覆盖下列兼容层文件（被覆盖的原件备份在 _legacy_compat_backup\%NAME%\）。
>>"%REC%" echo 注意：启动游戏请用 game_starter.exe，它会自动适配本机桌面分辨率、
>>"%REC%" echo 跳过开场动画、清理安全模式残留标记；通关存档放在 save\ 里原样保留。
>>"%REC%" echo.
>>"%REC%" echo 覆盖文件清单：
for /f "usebackq delims=" %%L in ("%PAY%\manifest.txt") do (
  if exist "%TARGET%\%%L" (
    if not exist "%BAK%\%%L" (
      mkdir "%BAK%\%%~dpL" 2>nul
      copy /y "%TARGET%\%%L" "%BAK%\%%L" >nul
      >>"%REC%" echo   已覆盖 %%L （原件备份在 _legacy_compat_backup\%NAME%\%%L）
    ) else (
      >>"%REC%" echo   已覆盖 %%L （首次备份已存在，不再重复备份）
    )
  ) else (
    >>"%REC%" echo   新增   %%L
  )
  copy /y "%PAY%\%%L" "%TARGET%\%%L" >nul
)
echo.
echo   ============================================================
echo     应用完成。到游戏目录里点 game_starter.exe 开始游戏。
echo     反悔：同目录的 一键撤回.bat
echo   ============================================================
echo.
pause
exit /b 0
