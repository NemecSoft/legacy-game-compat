@echo off&color 4f
title 恢复原版 - 反恐精英 1.6
cd /d "%~dp0"
reg import "%~dp0注册表备份.reg" >nul 2>&1
del /q "opengl32.dll" "libgallium_wgl.dll" "dxil.dll" >nul 2>&1
del /q "D3D8.dll" "D3D9.dll" "DDraw.dll" "D3DImm.dll" "dgVoodoo.conf" >nul 2>&1
del /q "CSOverlay.exe" >nul 2>&1
del /q "cstrike\userconfig.cfg" "cstrike_schinese\userconfig.cfg" >nul 2>&1
del /q "cstrike\usercfg_name.cfg" "cstrike_schinese\usercfg_name.cfg" >nul 2>&1
if exist "cstrike\config.cfg.ori-bak" copy /y "cstrike\config.cfg.ori-bak" "cstrike\config.cfg" >nul
if exist "cstrike\listenserver.cfg.ori-bak" copy /y "cstrike\listenserver.cfg.ori-bak" "cstrike\listenserver.cfg" >nul
del /q "cstrike\autoexec.cfg" >nul 2>&1
echo.
echo   已移除兼容文件和覆盖层，并恢复注册表和 config
echo   注意：本机没有硬件 OpenGL，恢复后 CS 会用系统 OpenGL（GL 1.1）而无法进入
echo   想再次玩，双击 兼容修复.bat 恢复 Mesa 层
echo.
pause
