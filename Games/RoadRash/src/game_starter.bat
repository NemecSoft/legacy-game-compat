@echo off&color 4f
setlocal
set "tl=暴力摩托 Road Rash 2002"
title %tl%
cd /d "%~dp0"
:: 薄包装：只做「无黑框 + 陪跑 + 回传退出码」，选项与说明全在 game_starter.exe 界面里
:: cmd 对 GUI 程序不等待，所以必须 start "" /wait；黑窗口由 game_starter.exe 的 /hideparent 藏掉
start "" /wait "%~dp0game_starter.exe" /go /hideparent %*
exit /b %ERRORLEVEL%
