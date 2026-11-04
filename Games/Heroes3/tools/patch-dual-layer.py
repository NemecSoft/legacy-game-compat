# -*- coding: utf-8 -*-
# 把「双通道」贯通到构建脚本、控制台 bat 菜单和 兼容修复.bat
import io

B = r'D:\AI\legacy-game-compat\Games\Heroes3'


def load(p):
    return io.open(p, encoding='utf-8', newline='').read().replace('\r\n', '\n')


def save(p, t):
    io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))


# ---------- 1) build.ps1 ----------
p = B + r'\build.ps1'
t = load(p)
old = "$presets = @('ddraw.ini', 'ddraw-43.ini', 'ddraw-window.ini', 'ddraw-640.ini')"
new = ("$presets = @('ddraw.ini', 'ddraw-43.ini', 'ddraw-window.ini', 'ddraw-640.ini',\n"
       "           'ddraw-d3d9.ini', 'dgVoodoo-full.conf', 'dgVoodoo-window.conf')")
assert old in t
t = t.replace(old, new, 1)

old2 = """Deploy-Bat 'game_starter.bat'"""
new2 = """# dgVoodoo2 层：仓库自带的 32 位 DDraw.dll（游戏是 32 位），落成 _dgvoodoo2\\ddraw.dll
$vd = Join-Path $GameDir '_dgvoodoo2'
New-Item -ItemType Directory -Force -Path $vd | Out-Null
$vdSrc = Join-Path $Repo 'tools\\dgvoodoo\\x86\\DDraw.dll'
if (-not (Test-Path $vdSrc)) { throw ('缺少 dgVoodoo2 层：' + $vdSrc) }
Copy-Item $vdSrc (Join-Path $vd 'ddraw.dll') -Force

Deploy-Bat 'game_starter.bat'"""
assert old2 in t
t = t.replace(old2, new2, 1)
save(p, t)
print('build.ps1 ok')

# ---------- 2) game_starter.bat ----------
p = B + r'\src\game_starter.bat'
t = load(p)
old = """  echo     默认 GDI（最兼容），画面最稳。
  echo     黑屏/花屏时选 GDI 稳定全屏或窗口；
  echo     其他画面方式在本游戏容易出问题，别手动改。
  echo     想换窗口用启动器的「窗口模式」；多人用 IPX 或 TCP/IP。
  echo   ============================================================
  echo.
  echo     显示方式
  echo       1  稳定全屏（GDI，最兼容）
  echo       2  保持 4:3（左右黑边）
  echo       3  窗口模式（1280×720）
  echo       4  原版 640×480（窗口）
  echo.
  echo       5  打开运行日志
  echo       6  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-6]，直接回车 = 1: \""""
new = """  echo     默认走 cnc-ddraw GDI（最兼容，画面最稳）。
  echo     黑屏/花屏先换回 1；想试别的通道再往下选。
  echo     多人用 IPX 或 TCP/IP。
  echo   ============================================================
  echo.
  echo     显示方式
  echo       1  兼容模式（cnc-ddraw GDI，全屏）
  echo       2  兼容模式（cnc-ddraw GDI，保持 4:3）
  echo       3  兼容模式（cnc-ddraw GDI，窗口 1280×720）
  echo       4  原版画面（cnc-ddraw GDI，640×480 窗口）
  echo       5  直连模式（cnc-ddraw DirectX9，全屏）
  echo       6  兼容模式（dgVoodoo2，全屏）
  echo       7  兼容模式（dgVoodoo2，窗口）
  echo.
  echo       8  打开运行日志
  echo       9  退出
  echo   ============================================================
  echo.
  set /p "sel=  请选择 [1-9]，直接回车 = 1: \""""
assert old in t
t = t.replace(old, new, 1)

old = ('if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" '
       'if not "%sel%"=="5" if not "%sel%"=="6" set "sel=1"\n'
       'if "%sel%"=="5" (\n'
       '  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"\n'
       '  exit /b 0\n'
       ')\n'
       'if "%sel%"=="6" exit /b 0\n'
       'set "preset=ddraw.ini"\n'
       'if "%sel%"=="2" set "preset=ddraw-43.ini"\n'
       'if "%sel%"=="3" set "preset=ddraw-window.ini"\n'
       'if "%sel%"=="4" set "preset=ddraw-640.ini"\n')
new = ('if not "%sel%"=="1" if not "%sel%"=="2" if not "%sel%"=="3" if not "%sel%"=="4" '
       'if not "%sel%"=="5" if not "%sel%"=="6" if not "%sel%"=="7" if not "%sel%"=="8" '
       'if not "%sel%"=="9" set "sel=1"\n'
       'if "%sel%"=="8" (\n'
       '  if exist "运行日志.txt" start "" notepad "%~dp0运行日志.txt"\n'
       '  exit /b 0\n'
       ')\n'
       'if "%sel%"=="9" exit /b 0\n'
       'rem 选哪条通道就顶哪一层的 ddraw.dll，配置也只留这一层的那一份\n'
       'set "preset=ddraw.ini"\n'
       'set "layer=_cnc-ddraw"\n'
       'if "%sel%"=="2" set "preset=ddraw-43.ini"\n'
       'if "%sel%"=="3" set "preset=ddraw-window.ini"\n'
       'if "%sel%"=="4" set "preset=ddraw-640.ini"\n'
       'if "%sel%"=="5" set "preset=ddraw-d3d9.ini"\n'
       'if "%sel%"=="6" (set "preset=dgVoodoo-full.conf"&set "layer=_dgvoodoo2")\n'
       'if "%sel%"=="7" (set "preset=dgVoodoo-window.conf"&set "layer=_dgvoodoo2")\n')
assert old in t
t = t.replace(old, new, 1)

old = (')\nif exist "兼容修复.bat" call "兼容修复.bat" /quiet >nul 2>&1\n'
       'copy /y "_presets\\%preset%" "ddraw.ini" >nul\n')
new = (')\nif not exist "%layer%\\ddraw.dll" (\n'
       '  echo   缺少 %layer%\\ddraw.dll，这个显示方式用不了\n'
       '  if /i not "%~1"=="/go" pause\n'
       '  exit /b 1\n'
       ')\n'
       'if exist "兼容修复.bat" call "兼容修复.bat" /quiet >nul 2>&1\n'
       'copy /y "%layer%\\ddraw.dll" "ddraw.dll" >nul\n'
       'if "%layer%"=="_dgvoodoo2" (\n'
       '  copy /y "_presets\\%preset%" "dgVoodoo.conf" >nul\n'
       '  del /q "ddraw.ini" >nul 2>&1\n'
       ') else (\n'
       '  copy /y "_presets\\%preset%" "ddraw.ini" >nul\n'
       '  del /q "dgVoodoo.conf" >nul 2>&1\n'
       ')\n')
assert old in t
t = t.replace(old, new, 1)

old = ('"%~dp0Heroes3.exe"\n'
       'set "rc=%ERRORLEVEL%"\n'
       'copy /y "_presets\\ddraw.ini" "ddraw.ini" >nul 2>&1\n'
       'exit /b %rc%\n')
new = ('"%~dp0Heroes3.exe"\n'
       'set "rc=%ERRORLEVEL%"\n'
       'rem 收尾回到最兼容的 cnc-ddraw GDI，不在机器上留两层叠着的现场\n'
       'copy /y "_cnc-ddraw\\ddraw.dll" "ddraw.dll" >nul 2>&1\n'
       'copy /y "_presets\\ddraw.ini" "ddraw.ini" >nul 2>&1\n'
       'del /q "dgVoodoo.conf" >nul 2>&1\n'
       'exit /b %rc%\n')
assert old in t
t = t.replace(old, new, 1)
save(p, t)
print('game_starter.bat ok')

# ---------- 3) 兼容修复.bat ----------
p = B + r'\src\兼容修复.bat'
t = load(p)
old = "echo   - 显示预设：_presets\\ddraw.ini / ddraw-43.ini / ddraw-window.ini / ddraw-640.ini"
new = ("echo   - 显示预设：_presets\\ddraw.ini / ddraw-43.ini / ddraw-window.ini / ddraw-640.ini\r\n"
       "echo              另有 ddraw-d3d9.ini（cnc-ddraw DirectX9）与 dgVoodoo-full/window.conf（dgVoodoo2）\r\n"
       "if exist \"_dgvoodoo2\\ddraw.dll\" (\r\n"
       "  echo   - dgVoodoo2 层：_dgvoodoo2\\ddraw.dll 已就位（选 6/7 时才顶上去）\r\n"
       ") else (\r\n"
       "  echo   - dgVoodoo2 层：缺 _dgvoodoo2\\ddraw.dll，只能选 1-5\r\n"
       ")")
new = new.replace('\r\n', '\n')
assert old in t
t = t.replace(old, new, 1)
save(p, t)
print('兼容修复.bat ok')
