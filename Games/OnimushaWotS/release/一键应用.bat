@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=一键应用 - 鬼武者：剑之道"
title %tl%
mode con: lines=38 cols=100 >nul 2>&1
cd /d "%~dp0"

set "PKG=%~dp0"
set "PAY=%PKG%payload"
set "NAME=OnimushaWotS"
set "GAMEEXE=OnimushaWotS.exe"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 鬼武者：剑之道
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
  echo   这不是鬼武者：剑之道的游戏目录。
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
>>"%REC%" echo [%DATE% %TIME%] 一键应用：鬼武者：剑之道
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo 包目录："%PKG%"
>>"%REC%" echo 说明：会先备份并删除目标目录的 config.ini。
>>"%REC%" echo 说明：每台电脑首次启动时按自己的桌面分辨率生成画面，
>>"%REC%" echo 说明：不沿用开发机的全屏分辨率索引和画质设置。
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
            >>"%REC%" echo [!TIME!] 错误：备份失败，跳过复制 !REL!
            set "ERR=1"
            set "SKIP=1"
          ) else (
            >>"%REC%" echo [!TIME!] 备份原文件：!REL! -^> _legacy_compat_backup\%NAME%\!REL!
            set /a BACKUPS+=1
          )
        ) else (
          >>"%REC%" echo [!TIME!] 已有历史备份，保留旧备份：!REL!
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
      >>"%REC%" echo [!TIME!] 跳过复制（原文件未能备份）：!REL!
    )
  )
)

set "CFG=%TARGET%\config.ini"
set "CFGBAK=%BACK%\config.ini"
if exist "%CFG%" (
  set "CFGBAKOK=1"
  if not exist "!CFGBAK!" (
    copy /y "!CFG!" "!CFGBAK!" >nul
    if errorlevel 1 set "CFGBAKOK=0"
  )
  if "!CFGBAKOK!"=="1" (
    del /f /q "!CFG!" >nul 2>&1
    if exist "!CFG!" (
      >>"%REC%" echo [!TIME!] 错误：config.ini 删除失败，请手动删除
      set "ERR=1"
    ) else (
      >>"%REC%" echo [!TIME!] 已删除 config.ini，首次启动会按本机桌面分辨率重新生成
    )
  ) else (
    >>"%REC%" echo [!TIME!] 错误：config.ini 备份失败，未删除
    set "ERR=1"
  )
) else (
  >>"%REC%" echo [!TIME!] config.ini 不存在，首次启动会按本机桌面分辨率生成
)

>>"%REC%" echo ------------------------------------------------------------
>>"%REC%" echo [!TIME!] 完成：复制 !COPIES! 个，备份 !BACKUPS! 个，错误=!ERR!
>>"%REC%" echo 记录与备份保存在："%BACK%"
>>"%REC%" echo ============================================================
if "!ERR!"=="1" (
  echo.
  echo   应用完成，但有错误，请看：
  echo   "%REC%"
  pause
  exit /b 1
)
echo.
echo   应用完成。
echo   记录与备份："%BACK%"
echo   现在可双击目标目录里的 game_starter.exe 启动游戏。
pause
exit /b 0
