@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERR] csc.exe not found - install .NET Framework 4.x
    exit /b 1
)

"%CSC%" -nologo -platform:x86 -out:D3D9Probe32.exe D3D9Probe.cs
if errorlevel 1 exit /b 1
"%CSC%" -nologo -platform:x64 -out:D3D9Probe64.exe D3D9Probe.cs
if errorlevel 1 exit /b 1
"%CSC%" -nologo -out:FixGame.exe FixGame.cs
if errorlevel 1 exit /b 1

echo.
echo Build OK: D3D9Probe32.exe / D3D9Probe64.exe / FixGame.exe
