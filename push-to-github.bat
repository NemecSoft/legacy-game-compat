@echo off&color 4f
setlocal EnableExtensions
set "tl=推送 legacy-game-compat 到 GitHub"
title %tl%
mode con: lines=30 cols=100 >nul 2>&1
cd /d "%~dp0"

echo.
echo   ============================================================
echo     推送 legacy-game-compat 到 GitHub
echo   ============================================================
echo.

where git >nul 2>&1
if errorlevel 1 (
  echo   找不到 git，请先安装 Git for Windows。
  echo   下载：https://git-scm.com/download/win
  pause
  exit /b 1
)

if not exist ".git" (
  echo   初始化本地仓库...
  git init -b main
  if errorlevel 1 (
    echo   初始化失败，请检查当前目录权限。
    pause
    exit /b 1
  )
)

git remote get-url origin >nul 2>&1
if errorlevel 1 (
  git remote add origin https://github.com/NemecSoft/legacy-game-compat.git
) else (
  git remote set-url origin https://github.com/NemecSoft/legacy-game-compat.git
)

echo   收集改动...
git add -A
if errorlevel 1 (
  echo   git add 失败，请检查文件是否被占用。
  pause
  exit /b 1
)

git diff --cached --quiet
if errorlevel 1 (
  echo   提交改动...
  git commit -m "Update: %DATE:~0,10% %TIME%"
  if errorlevel 1 (
    echo   提交失败，请查看上面的错误信息。
    pause
    exit /b 1
  )
) else (
  echo   没有新改动，直接推送已有提交。
)

echo.
echo   正在推送到 GitHub...
echo   如果弹出 GitHub 登录窗口，请完成一次授权。
echo.
git push -u origin main
if errorlevel 1 (
  echo.
  echo   推送失败：请检查网络、GitHub 登录是否过期，
  echo   或远端是否有新提交需要先同步。
  pause
  exit /b 1
)

echo.
echo   ============================================================
echo     推送完成
echo     https://github.com/NemecSoft/legacy-game-compat
echo   ============================================================
echo.
pause
exit /b 0
