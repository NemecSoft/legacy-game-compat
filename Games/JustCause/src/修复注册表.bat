@echo off&color 4f
setlocal EnableExtensions
set "tl=修复正当防卫注册表"
title %tl%
mode con: lines=20 cols=86 >nul 2>&1
if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
cd /d "%~dp0"
set "AP=%~dp0"
if "%AP:~-1%"=="\" set "AP=%AP:~0,-1%"
set "KEY=HKLM\SOFTWARE\Eidos\Just Cause"

reg query "%KEY%" /reg:32 /v InstallPath 2>nul | find /i "%AP%" >nul
if not errorlevel 1 (
  reg query "%KEY%" /reg:32 /v Language 2>nul | find /i "0x409" >nul
  if not errorlevel 1 (
    reg query "%KEY%\1.00.0000" /reg:32 >nul 2>&1
    if not errorlevel 1 (
      echo   注册表已就绪，不需要修改。
      if /i not "%~1"=="/quiet" pause
      exit /b 0
    )
  )
)

net session >nul 2>&1
if errorlevel 1 (
  echo   需要管理员权限，正在请求...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '/quiet' -Verb RunAs"
  exit /b 0
)

set "ok=1"
reg add "%KEY%" /reg:32 /f >nul 2>&1 || set "ok=0"
reg add "%KEY%" /reg:32 /v Language /t REG_DWORD /d 0x409 /f >nul 2>&1 || set "ok=0"
reg add "%KEY%" /reg:32 /v InstallPath /t REG_SZ /d "%AP%" /f >nul 2>&1 || set "ok=0"
reg add "%KEY%\1.00.0000" /reg:32 /f >nul 2>&1 || set "ok=0"

echo.
if "%ok%"=="1" (
  echo   注册表已写入：
  echo     HKLM\SOFTWARE\Eidos\Just Cause
  echo     Language    = 0x409
  echo     InstallPath = %AP%
  echo     1.00.0000
) else (
  echo   注册表写入失败，请右键本文件「以管理员身份运行」。
)
if /i not "%~1"=="/quiet" pause
if "%ok%"=="1" exit /b 0
exit /b 1
