@echo off&color 1f
title legacy-game-compat
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0adapt.ps1" %*
echo.
pause
