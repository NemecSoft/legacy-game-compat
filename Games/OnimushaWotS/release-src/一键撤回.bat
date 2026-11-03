@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=一键撤回 - 鬼武者：剑之道"
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
  echo     一键撤回 - 鬼武者：剑之道
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

set "BACK=%TARGET%\_legacy_compat_backup\%NAME%"
if not exist "%BACK%\manifest.txt" (
  echo.
  echo   找不到交付记录：
  echo   "%BACK%\manifest.txt"
  echo   没有可撤回的交付。
  pause
  exit /b 1
)

set "REC=%BACK%\交付记录.txt"
>>"%REC%" echo ============================================================
>>"%REC%" echo [%DATE% %TIME%] 一键撤回：鬼武者：剑之道
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo ------------------------------------------------------------

set "ERR=0"
set "RESTORED=0"
set "DELETED=0"
set "SKIPPED=0"
for /f "usebackq delims=" %%F in ("%BACK%\manifest.txt") do (
  set "REL=%%F"
  set "SRC=%PAY%\%%F"
  set "DST=%TARGET%\%%F"
  set "BAK=%BACK%\%%F"
  if exist "!BAK!" (
    if exist "!DST!" (
      if exist "!SRC!" (
        fc /b "!DST!" "!SRC!" >nul 2>&1
        if errorlevel 1 (
          >>"%REC%" echo [!TIME!] 跳过恢复（目标文件已被修改）：!REL!
          set /a SKIPPED+=1
        ) else (
          copy /y "!BAK!" "!DST!" >nul
          if errorlevel 1 (
            >>"%REC%" echo [!TIME!] 错误：恢复失败 !REL!
            set "ERR=1"
          ) else (
            >>"%REC%" echo [!TIME!] 恢复原文件：!REL!
            set /a RESTORED+=1
          )
        )
      ) else (
        >>"%REC%" echo [!TIME!] 跳过（包内缺少对照文件）：!REL!
        set /a SKIPPED+=1
      )
    ) else (
      for %%D in ("!DST!") do if not exist "%%~dpD" mkdir "%%~dpD" >nul 2>&1
      copy /y "!BAK!" "!DST!" >nul
      if errorlevel 1 (
        >>"%REC%" echo [!TIME!] 错误：恢复失败 !REL!
        set "ERR=1"
      ) else (
        >>"%REC%" echo [!TIME!] 恢复原文件：!REL!
        set /a RESTORED+=1
      )
    )
  ) else (
    if exist "!DST!" (
      if exist "!SRC!" (
        fc /b "!DST!" "!SRC!" >nul 2>&1
        if errorlevel 1 (
          >>"%REC%" echo [!TIME!] 跳过删除（目标文件已被修改）：!REL!
          set /a SKIPPED+=1
        ) else (
          del /f /q "!DST!" >nul 2>&1
          if errorlevel 1 (
            >>"%REC%" echo [!TIME!] 错误：删除失败 !REL!
            set "ERR=1"
          ) else (
            >>"%REC%" echo [!TIME!] 删除交付文件：!REL!
            set /a DELETED+=1
          )
        )
      ) else (
        >>"%REC%" echo [!TIME!] 跳过删除（包内缺少对照文件）：!REL!
        set /a SKIPPED+=1
      )
    )
  )
)

set "CFG=%TARGET%\config.ini"
set "CFGBAK=%BACK%\config.ini"
if exist "%CFGBAK%" (
  if exist "%CFG%" (
    >>"%REC%" echo [!TIME!] config.ini 已由游戏重新生成，保留当前文件；原配置备份在 "%CFGBAK%"
  ) else (
    copy /y "%CFGBAK%" "%CFG%" >nul
    if errorlevel 1 (
      >>"%REC%" echo [!TIME!] 错误：config.ini 恢复失败
      set "ERR=1"
    ) else (
      >>"%REC%" echo [!TIME!] 恢复原 config.ini
      set /a RESTORED+=1
    )
  )
) else (
  >>"%REC%" echo [!TIME!] 没有 config.ini 备份，跳过
)

>>"%REC%" echo ------------------------------------------------------------
>>"%REC%" echo [!TIME!] 完成：恢复 !RESTORED! 个，删除 !DELETED! 个，跳过 !SKIPPED! 个，错误=!ERR!
>>"%REC%" echo ============================================================
if "!ERR!"=="1" (
  echo.
  echo   撤回完成，但有错误，请看：
  echo   "%REC%"
  pause
  exit /b 1
)
echo.
echo   撤回完成。
echo   记录："%REC%"
pause
exit /b 0
