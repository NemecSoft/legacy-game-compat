@echo off
title 英雄无敌3 注册表修复工具
rem 用法：本 bat 必须放在游戏目录里，双击运行。
rem 作用：把本机验证过的完整 39 个注册表值写进 32 位视图，
rem       两个键名（R 版和问号版）都写，AppPath/CDDrive 按当前目录自动填。
set "H3DIR=%~dp0"
set "PS1=%TEMP%\h3_reg_fix.ps1"
del "%PS1%" >nul 2>&1
>> "%PS1%" echo $ErrorActionPreference = "Stop"
>> "%PS1%" echo try {
>> "%PS1%" echo $GameDir = $env:H3DIR
>> "%PS1%" echo if (-not $GameDir.EndsWith("\")) { $GameDir = $GameDir + "\" }
>> "%PS1%" echo $mark = [char]0xAE
>> "%PS1%" echo $names = @(("Heroes of Might and Magic" + $mark + " III"), "Heroes of Might and Magic?III")
>> "%PS1%" echo $list = @("AppPath|String|", "CDDrive|String|", "Show Intro|DWord|1", "Music Volume|DWord|5", "Sound Volume|DWord|5", "Last Music Volume|DWord|5", "Last Sound Volume|DWord|5", "Walk Speed|DWord|2", "Computer Walk Speed|DWord|3", "Show Route|DWord|1", "Move Reminder|DWord|1", "Quick Combat|DWord|0", "Video Subtitles|DWord|0", "Town Outlines|DWord|1", "Animate SpellBook|DWord|1", "Window Scroll Speed|DWord|1", "Bink Video|DWord|0", "Blackout Computer|DWord|0", "First Time|DWord|0", "Test Decomp|DWord|0", "Test Read|DWord|0", "Test Blit|DWord|0", "Unique System ID|String|I29", "Network Default Name|String|Playerer", "Autosave|DWord|1", "Show Combat Grid|DWord|0", "Show Combat Mouse Hex|DWord|0", "Combat Shade Level|DWord|0", "Combat Army Info Level|DWord|0", "Combat Auto Creatures|DWord|1", "Combat Auto Spells|DWord|1", "Combat Catapult|DWord|1", "Combat Ballista|DWord|1", "Combat First Aid Tent|DWord|1", "Combat Speed|DWord|0", "Main Game Show Menu|DWord|1", "Main Game X|DWord|0", "Main Game Y|DWord|0", "Main Game Full Screen|DWord|1")
>> "%PS1%" echo foreach ($nm in $names) {
>> "%PS1%" echo $base = "HKLM:\SOFTWARE\WOW6432Node\New World Computing\" + $nm
>> "%PS1%" echo $null = New-Item -Path ($base + "\1.0") -Force
>> "%PS1%" echo foreach ($it in $list) {
>> "%PS1%" echo $p = $it.Split("|")
>> "%PS1%" echo $v = $p[2]
>> "%PS1%" echo if ($p[0] -eq "AppPath") { $v = $GameDir }
>> "%PS1%" echo if ($p[0] -eq "CDDrive") { $v = $GameDir.Substring(0,2) }
>> "%PS1%" echo if ($p[1] -eq "String") { $null = New-ItemProperty -Path ($base + "\1.0") -Name $p[0] -PropertyType String -Value $v -Force } else { $null = New-ItemProperty -Path ($base + "\1.0") -Name $p[0] -PropertyType DWord -Value ([int]$v) -Force }
>> "%PS1%" echo }
>> "%PS1%" echo $chk = Get-Item -LiteralPath ($base + "\1.0")
>> "%PS1%" echo Write-Host ("[OK] " + $nm + "  共 " + $chk.ValueCount + " 个值")
>> "%PS1%" echo }
>> "%PS1%" echo Write-Host ""
>> "%PS1%" echo Write-Host ("AppPath  = " + $GameDir)
>> "%PS1%" echo Write-Host ("CDDrive  = " + $GameDir.Substring(0,2))
>> "%PS1%" echo Write-Host "写入完成。现在点「启动游戏」试试还崩不崩。"
>> "%PS1%" echo } catch {
>> "%PS1%" echo Write-Host ("[失败] " + $_.Exception.Message)
>> "%PS1%" echo Write-Host "如果提示拒绝访问，请右键本 bat 选「以管理员身份运行」再试一次。"
>> "%PS1%" echo exit 1
>> "%PS1%" echo }
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
set "RC=%ERRORLEVEL%"
del "%PS1%" >nul 2>&1
echo.
pause
exit /b %RC%
