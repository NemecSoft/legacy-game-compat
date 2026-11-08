@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
rem 本文件是 GBK；从 UTF-8 控制台（例如 pwsh 7）调用时 cmd 会按 65001 解码，中文全乱码、
rem 连「交付记录.txt」都会写成乱码文件名，下一次撤回就找不到记录。这里把代码页钉死成 936，
rem 不管谁怎么调用都走同一条路（AGENTS 规则 24 第 10 类、规则 37）。
chcp 936 >nul
set "tl=一键应用 - 英雄无敌3：死亡阴影"
title %tl%
mode con: lines=36 cols=100 >nul 2>&1
cd /d "%~dp0"

set "PKG=%~dp0"
set "PAY=%PKG%payload"
set "NAME=Heroes3"
set "GAMEEXE=Heroes3.exe"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 英雄无敌3：死亡阴影
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
  echo   这不是英雄无敌3的游戏目录。
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
>>"%REC%" echo [%DATE% %TIME%] 一键应用：英雄无敌3：死亡阴影
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo 包目录："%PKG%"
>>"%REC%" echo 记录说明：应用脚本会备份目标同名文件并调用兼容修复设置 AppPath/CDDrive；撤回不自动恢复该注册表项。
>>"%REC%" echo 记录说明：运行日志.txt 为启动器运行产物，撤回会删除；显示预设与存档不改。
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
if exist "%TARGET%\兼容修复.bat" (
  >>"%REC%" echo [!TIME!] 调用兼容修复：设置 AppPath/CDDrive、检查 cnc-ddraw
  call "%TARGET%\兼容修复.bat" /quiet >nul 2>&1
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
