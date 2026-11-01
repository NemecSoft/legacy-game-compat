@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=一键应用 - 破坏者 The Saboteur"
title %tl%
mode con: lines=36 cols=100 >nul 2>&1
cd /d "%~dp0"

set "PKG=%~dp0"
set "PAY=%PKG%payload"
set "NAME=TheSaboteur"
set "GAMEEXE=Saboteur.exe"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 破坏者 The Saboteur
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
  echo   这不是破坏者的游戏目录。
  pause
  exit /b 1
)
if not exist "%PAY%\manifest.txt" (
  echo   交付包不完整：缺少 payload\manifest.txt
  pause
  exit /b 1
)

set "BACK=%TARGET%\_legacy_compat_backup\%NAME%"
if not exist "%BACK%" mkdir "%BACK%" >nul 2>&1
if not exist "%BACK%\manifest.txt" copy /y "%PAY%\manifest.txt" "%BACK%\manifest.txt" >nul

set "REC=%BACK%\交付记录.txt"
>>"%REC%" echo ============================================================
>>"%REC%" echo [%DATE% %TIME%] 一键应用：破坏者 The Saboteur
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo 包目录："%PKG%"
>>"%REC%" echo 记录说明：应用脚本只修改目标游戏目录；启动器运行时会写我的文档 config.ini 的分辨率，撤回不自动还原。
>>"%REC%" echo 记录说明：运行日志.txt / Saboteur_d3d9.log / Saboteur.dxvk-cache 为运行产物，撤回会删除。
>>"%REC%" echo ------------------------------------------------------------

set "ERR=0"
set "COPIES=0"
set "BACKUPS=0"
for /f "usebackq delims=" %%F in ("%PAY%\manifest.txt") do (
  set "REL=%%F"
  set "SRC=%PAY%\%%F"
  set "DST=%TARGET%\%%F"
  set "BAK=%BACK%\%%F"
  set "SKIP=0"
  if not exist "!SRC!" (
    >>"%REC%" echo [!TIME!] 错误：包内缺少 !REL!
    set "ERR=1"
  ) else (
    if exist "!DST!" (
      fc /b "!DST!" "!SRC!" >nul 2>&1
      if not errorlevel 1 (
        >>"%REC%" echo [!TIME!] 已存在且相同：!REL!
      ) else (
        if not exist "!BAK!" (
          for %%D in ("!BAK!") do if not exist "%%~dpD" mkdir "%%~dpD" >nul 2>&1
          copy /y "!DST!" "!BAK!" >nul
          if errorlevel 1 (
            >>"%REC%" echo [!TIME!] 错误：备份失败，跳过覆盖 !REL!
            set "ERR=1"
            set "SKIP=1"
          ) else (
            >>"%REC%" echo [!TIME!] 备份原文件：!REL! -^> _legacy_compat_backup\%NAME%\!REL!
            set /a BACKUPS+=1
          )
        ) else (
          >>"%REC%" echo [!TIME!] 已有历史备份，保留：!REL!
        )
      )
    ) else (
      >>"%REC%" echo [!TIME!] 新增文件：!REL!
    )
    if "!SKIP!"=="0" (
      for %%D in ("!DST!") do if not exist "%%~dpD" mkdir "%%~dpD" >nul 2>&1
      copy /y "!SRC!" "!DST!" >nul
      if errorlevel 1 (
        >>"%REC%" echo [!TIME!] 错误：复制失败 !REL!
        set "ERR=1"
      ) else (
        >>"%REC%" echo [!TIME!] 复制交付文件：!REL!
        set /a COPIES+=1
      )
    ) else (
      >>"%REC%" echo [!TIME!] 跳过复制（原文件未备份）：!REL!
    )
  )
)
>>"%REC%" echo ------------------------------------------------------------
>>"%REC%" echo [!TIME!] 完成：复制 !COPIES! 个，备份 !BACKUPS! 个，错误=!ERR!
>>"%REC%" echo 记录与备份保留在："%BACK%"
>>"%REC%" echo ============================================================
if "!ERR!"=="1" (
  echo.
  echo   应用完成但有错误，详见：
  echo   "%REC%"
  pause
  exit /b 1
)
echo.
echo   应用完成。
echo   记录与备份："%BACK%"
echo   现在可双击目标目录里的 game_starter.exe 进游戏。
pause
exit /b 0
