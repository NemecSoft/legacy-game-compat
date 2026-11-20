# 自动穿透 attract 进战斗：每 3s  tap 一次 J（P1Button1）。attract->标题->模式选择->战斗 全链 J 可确认；战斗中 J=射击无害。
param([int]$Rounds = 40, [int]$GapSec = 3)
for ($i = 1; $i -le $Rounds; $i++) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { 'no game, stop'; exit 1 }
    & 'D:\Temp\send-start.ps1' -Keys '24' -WaitMs 0 | Out-Null
    Start-Sleep -Seconds $GapSec
}
'auto-enter done'