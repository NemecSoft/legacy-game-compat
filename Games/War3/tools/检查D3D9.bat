@echo off
chcp 936 >nul
title 检查 D3D9
echo 正在检查本机 D3D9 ...
echo.
"D:\AI\legacy-game-compat\Games\War3\tools\dxprobe.exe"
echo.
echo 看上面的 CheckDeviceType / CreateDevice：
echo   0x00000000 = 正常
echo   0x8876086A = 仍然被禁用/不可用
echo.
pause
