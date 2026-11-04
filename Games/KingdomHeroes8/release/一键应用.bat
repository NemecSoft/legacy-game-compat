@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=一键应用 - 三国群英传 8"
title %tl%
mode con: lines=36 cols=100 >nul 2>&1
cd /d "%~dp0"

set "PKG=%~dp0"
set "PAY=%PKG%payload"
set "NAME=KingdomHeroes8"
set "OFFLINE=SG8Launcher.exe"
set "GAMEEXE=GAME\SG8.exe"
set "TARGET=%~1"
if not defined TARGET (
  echo.
  echo   ============================================================
  echo     一键应用 - 三国群英传 8
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
  echo.
  echo   目标目录里没有 %OFFLINE%：
  echo   "%TARGET%"
  echo   这不是三国群英传 8 的目录。
  if "%~1"=="" pause
  exit /b 1
)
if not exist "%TARGET%\%GAMEEXE%" (
  echo   目标目录里没有 %GAMEEXE%，不是完整的游戏包。
  if "%~1"=="" pause
  exit /b 1
)
if not exist "%PAY%\manifest.txt" (
  echo   交付包不完整：缺少 payload\manifest.txt
  if "%~1"=="" pause
  exit /b 1
)

set "BACK=%TARGET%\_legacy_compat_backup\%NAME%"
if not exist "%BACK%" mkdir "%BACK%" >nul 2>&1
set "REC=%BACK%\交付记录.txt"

echo   正在记录离线环境三个关键文件的指纹（证明我们没有改动它们）...
for %%F in ("%OFFLINE%" "Runtime\MiniPipeDll.dll" "%GAMEEXE%") do (
  if exist "%TARGET%\%%~fF" (
    for /f "skip=1 tokens=2 delims=:" %%H in ('certutil -hashfile "%TARGET%\%%~fF" SHA256 2^>nul') do (
      set "HS=%%H"
      set "HS=!HS: =!"
      >>"%REC%" echo [%%~nxF 未改动] !HS!
      echo   !HS!  %%~nxF
    )
  ) else (
    >>"%REC%" echo [%%~fF 不存在，跳过指纹记录]
  )
)

>>"%REC%" echo ============================================================
>>"%REC%" echo [%DATE% %TIME%] 一键应用：三国群英传 8 启动器
>>"%REC%" echo 目标目录："%TARGET%"
>>"%REC%" echo 说明：只新增下面这些文件，不覆盖、不删除包内任何原有文件。
>>"%REC%" echo 说明：离线环境（SG8Launcher.exe / Runtime / GAME）保持原样，由启动器调用。

set "ERR=0"
for /f "usebackq delims=" %%F in ("%PAY%\manifest.txt") do (
  set "REL=%%F"
  set "SRC=%PAY%\%%F"
  set "DST=%TARGET%\%%F"
  if not exist "!SRC!" (
    >>"%REC%" echo [!TIME!] 错误：包内缺少 !REL!
    set "ERR=1"
  ) else (
    for %%D in ("!DST!") do if not exist "%%~dpD" mkdir "%%~dpD" >nul 2>&1
    if exist "!DST!" (
      fc /b "!DST!" "!SRC!" >nul 2>&1
      if not errorlevel 1 (
        >>"%REC%" echo [!TIME!] 已存在且相同：!REL!
        echo   已存在：!REL!
      ) else (
        set "BAK=%BACK%\!REL!"
        for %%D in ("!BAK!") do if not exist "%%~dpD" mkdir "%%~dpD" >nul 2>&1
        if not exist "!BAK!" (
          copy /y "!DST!" "!BAK!" >nul
          >>"%REC%" echo [!TIME!] 备份旧版交付文件：!REL!
        ) else (
          >>"%REC%" echo [!TIME!] 已有历史备份，保留旧备份：!REL!
        )
        copy /y "!SRC!" "!DST!" >nul
        if errorlevel 1 (
          >>"%REC%" echo [!TIME!] 错误：复制失败 !REL!
          set "ERR=1"
        ) else (
          >>"%REC%" echo [!TIME!] 更新交付文件：!REL!
          echo   已更新：!REL!
        )
      )
    ) else (
      copy /y "!SRC!" "!DST!" >nul
      if errorlevel 1 (
        >>"%REC%" echo [!TIME!] 错误：复制失败 !REL!
        set "ERR=1"
      ) else (
        >>"%REC%" echo [!TIME!] 新增文件：!REL!
        echo   已安装：!REL!
      )
    )
  )
)

if "%ERR%"=="0" (
  echo.
  echo   完成。以后请用 game_starter.exe 启动游戏（左下角推广不会显示）。
) else (
  echo.
  echo   有文件没复制成功，请把本窗口的内容和 %REC% 发给管理员。
)
if "%~1"=="" pause
exit /b %ERR%
