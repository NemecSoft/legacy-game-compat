# 已验证进战斗链：清残留 -> /go -> 等 hwnd -> +15s 起密集 start(0.7s*40) 覆盖交互标题窗口 -> J*2 确认 -> 截图。
# 真战斗判据：左下命数图标 + 2UP=0 + 右下 PUSH START/FREE PLAY + 无中央 logo 水印；demo=中央水印+2UP=最高分。
# 交互标题=全 logo+免费游戏；白底+免费游戏=加载屏不收键；attract(logo/排行/demo)忽略所有键。
$ErrorActionPreference = 'Stop'
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'trainer\.ps1' } | ForEach-Object { 'kill trainer pid=' + $_.ProcessId; Stop-Process -Id $_.ProcessId -Force }
Get-Process -Name TeknoParrotUi, game_starter, game -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Start-Process -FilePath 'D:\YunGame\G\Raiden4\game_starter.exe' -ArgumentList '/go' -WorkingDirectory 'D:\YunGame\G\Raiden4'
$p = $null
for ($i = 0; $i -lt 90; $i++) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.MainWindowHandle -ne 0) { break }
    Start-Sleep -Seconds 1
}
if (-not $p) { 'no game'; exit 1 }
'game pid=' + $p.Id + ' hwnd=' + $p.MainWindowHandle
Start-Sleep -Seconds 15
for ($i = 0; $i -lt 40; $i++) {
    & 'D:\Temp\send-start.ps1' -Keys '06' -WaitMs 0 | Out-Null
    Start-Sleep -Milliseconds 700
}
& 'D:\Temp\send-start.ps1' -Keys '24' -WaitMs 2000 | Out-Null
& 'D:\Temp\send-start.ps1' -Keys '24' -WaitMs 2000 | Out-Null
& 'D:\Temp\shot-game.ps1'