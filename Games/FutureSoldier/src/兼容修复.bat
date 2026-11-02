@echo off&color 4f
setlocal EnableExtensions EnableDelayedExpansion
set "tl=兼容修复 - 幽灵行动：未来战士"
title %tl%
mode con: lines=26 cols=92 >nul 2>&1
cd /d "%~dp0"

set "KEY=HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
set "BAK=%~dp0_legacy_compat_backup\FutureSoldier"
if not exist "%BAK%" mkdir "%BAK%" >nul 2>&1
if not exist "%BAK%\AppCompat-Layers-before.reg" reg export "%KEY%" "%BAK%\AppCompat-Layers-before.reg" /y >nul 2>&1
set "REC=%BAK%\兼容修复记录.txt"
>>"%REC%" echo ============================================================
>>"%REC%" echo [%DATE% %TIME%] 清理 XP 兼容性/RUNASADMIN
>>"%REC%" echo 目标目录："%~dp0"
>>"%REC%" echo ------------------------------------------------------------

echo.
echo   正在移除 Future Soldier DX9/DX11 的 XP 兼容性...
echo.
for %%E in ("Future Soldier DX11.exe" "Future Soldier DX9.exe") do (
  set "P=%~dp0%%~E"
  reg query "%KEY%" /v "!P!" >nul 2>&1
  if errorlevel 1 (
    echo   已干净：%%~E
    >>"%REC%" echo [!TIME!] 已无兼容性项：%%~E
  ) else (
    reg delete "%KEY%" /v "!P!" /f >nul 2>&1
    if errorlevel 1 (
      echo   删除失败（可能需要管理员）：%%~E
      >>"%REC%" echo [!TIME!] 删除失败：%%~E
    ) else (
      echo   已移除：%%~E
      >>"%REC%" echo [!TIME!] 已移除：%%~E
    )
  )
)
if not exist "d3d9.dll" if exist "_dx9_compat\d3d9.dll" copy /y "_dx9_compat\d3d9.dll" "d3d9.dll" >nul 2>&1
if not exist "dxvk.conf" if exist "_dx9_compat\dxvk.conf" copy /y "_dx9_compat\dxvk.conf" "dxvk.conf" >nul 2>&1
>>"%REC%" echo DX9 兼容层：d3d9.dll + dxvk.conf（DXVK 2.5.3）已就绪
>>"%REC%" echo 备份："%BAK%\AppCompat-Layers-before.reg"
>>"%REC%" echo 说明：XP 兼容性不自动恢复；如需恢复请手动导入备份。
>>"%REC%" echo ============================================================
echo.
echo   完成。启动器不会再写 XP 兼容性；不要再用 go.bat。
echo   记录：%REC%
if /i not "%~1"=="/quiet" pause
exit /b 0
