@echo off&color 4f
setlocal EnableExtensions
set "tl=一键撤回 - 使命召唤7 黑色行动"
title %tl%
cd /d "%~dp0"
set "PAY=%~dp0payload"
set "NAME=CoD7"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键撤回 - 使命召唤7 黑色行动
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
set "BAK=%TARGET%\_legacy_compat_backup\%NAME%"
if not exist "%BAK%" (
  echo   没找到备份目录 %BAK%
  echo   说明可能还没应用过，或已被撤回。
  pause
  exit /b 1
)
set "REC=%TARGET%\交付记录.txt"
>>"%REC%" echo.
>>"%REC%" echo 撤回时间：%date% %time%
for /f "usebackq delims=" %%L in ("%PAY%\manifest.txt") do (
  if exist "%BAK%\%%L" (
    copy /y "%BAK%\%%L" "%TARGET%\%%L" >nul
    >>"%REC%" echo   恢复 %%L （原件来自备份）
  ) else (
    if exist "%TARGET%\%%L" del /f /q "%TARGET%\%%L"
    >>"%REC%" echo   删除 %%L （应用前不存在的新增文件）
  )
)
>>"%REC%" echo 说明：players\config.cfg 的画面/字幕默认值由启动器按账本管理，撤回不改动玩家存档。
echo.
echo   ============================================================
echo     撤回完成。游戏目录已恢复应用前状态。
echo   ============================================================
echo.
pause
exit /b 0
