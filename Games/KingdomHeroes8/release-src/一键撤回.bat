@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=一键撤回 - 三国群英传 8"
title %tl%
mode con: lines=34 cols=100 >nul 2>&1
cd /d "%~dp0"

set "PAY=%~dp0payload"
set "NAME=KingdomHeroes8"
set "OFFLINE=SG8Launcher.exe"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键撤回 - 三国群英传 8
  echo   ============================================================
  echo.
  echo   请把游戏目录拖到本窗口，或输入完整路径。
  set /p "TARGET=  目标目录："
)
if not defined TARGET (
  echo   没有输入目标目录。
  if "%~1"=="" pause
  exit /b 1
)
set "TARGET=%TARGET:"=%"
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\%OFFLINE%" (
  echo   目标目录里没有 %OFFLINE%，看起来不是三国群英传 8 的目录。
  if "%~1"=="" pause
  exit /b 1
)

set "BACK=%TARGET%\_legacy_compat_backup\%NAME%"
set "REC=%BACK%\交付记录.txt"
set "LIST=%BACK%\manifest.txt"
if not exist "%LIST%" set "LIST=%PAY%\manifest.txt"
if not exist "%LIST%" (
  echo   找不到交付清单（manifest.txt），无法确定要删哪些文件。
  if "%~1"=="" pause
  exit /b 1
)

>>"%REC%" echo ============================================================
>>"%REC%" echo [%DATE% %TIME%] 一键撤回：三国群英传 8 启动器
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo 说明：只删除交付清单里列出的文件；包内原有文件一个都不碰。

set "ERR=0"
for /f "usebackq delims=" %%F in ("%LIST%") do (
  set "REL=%%F"
  set "DST=%TARGET%\%%F"
  if exist "!DST!" (
    set "KEEP=0"
    if exist "%PAY%\!REL!" (
      fc /b "!DST!" "%PAY%\!REL!" >nul 2>&1
      if errorlevel 1 set "KEEP=1"
    )
    if "!KEEP!"=="1" (
      >>"%REC%" echo [!TIME!] 跳过（文件被改过，不删）：!REL!
      echo   保留：!REL!（内容与交付包不一致）
    ) else (
      del /f /q "!DST!" >nul 2>&1
      if exist "!DST!" (
        >>"%REC%" echo [!TIME!] 错误：删除失败 !REL!
        set "ERR=1"
      ) else (
        >>"%REC%" echo [!TIME!] 删除交付文件：!REL!
        echo   已删除：!REL!
      )
    )
  ) else (
    >>"%REC%" echo [!TIME!] 本来就没有：!REL!
  )
)

if exist "%TARGET%\_legacy_compat_backup\%NAME%\game_starter.exe.before-launcher" (
  echo   注意：目标目录原本就有 game_starter.exe，已保留在备份里，可按需手工恢复。
)

rmdir /s /q "%TARGET%\_tools\nircmd" >nul 2>&1
if exist "%TARGET%\_tools" (
  dir /b "%TARGET%\_tools" 2>nul | findstr /i "." >nul || rmdir /s /q "%TARGET%\_tools" >nul 2>&1
)

echo.
echo   撤回完成。游戏本身（%OFFLINE% / GAME / Runtime）没有被动过，
echo   仍可用原版方式启动。交付记录：%REC%
if "%~1"=="" pause
exit /b %ERR%
