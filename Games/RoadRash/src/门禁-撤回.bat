@echo off&color 4f
chcp 65001 >nul
title 暴力摩托 - 撤回启动门禁
cd /d "%~dp0"
:: 撤回启动门禁：把代理 DLL 换回原版，游戏恢复成双击也能直接玩。
:: 只动 AWEMan32.dll 这一个文件，不碰存档、按键、兼容层。
set "REAL=AWEMan32_real.dll"
set "PROXY=AWEMan32.dll"

echo.
echo   撤回启动门禁
echo   恢复后：双击 Roadrash.exe 就能直接进游戏
echo.
echo   此操作会覆盖当前的 %PROXY%
echo.

if not exist "%REAL%" (
  color 4f
  echo [失败] 找不到原始文件 %REAL%，无法撤回。
  echo        如果你已经撤回过了，就不用再点这个。
  echo.
  pause
  exit /b 1
)

:: 去掉只读属性再覆盖（原文件是 1995 年的只读属性）
attrib -r "%PROXY%" >nul 2>&1
copy /y "%REAL%" "%PROXY%" >nul 2>&1
if errorlevel 1 (
  echo [失败] 覆盖 %PROXY% 失败，检查文件是否被占用（游戏是否还开着）。
  echo.
  pause
  exit /b 1
)
attrib +r "%PROXY%" >nul 2>&1

echo [完成] 已恢复原版 %PROXY%，启动门禁已撤回。
echo        建议把 %REAL% 留在目录里，别删，方便以后重新启用。
echo.
pause
exit /b 0
