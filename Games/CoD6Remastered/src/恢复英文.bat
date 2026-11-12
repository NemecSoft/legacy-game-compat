@echo off
chcp 936 >nul
title 恢复英文
cd /d "%~dp0"
echo 正在恢复英文...

REM 1) 删掉本启动器写入的语言键（原始状态：该键不存在）
reg delete "HKCU\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR" /v LOCALE /f >nul 2>&1

REM 2) 把安装器语言标记恢复成英文（set /p 不写换行，保持 4 字节纯文本）
<nul set /p "=enUS"> Launcher.db

echo.
echo 已恢复英文。现在可以正常启动游戏。
echo 想再选语言，双击 game_starter.exe 即可。
echo.
pause
