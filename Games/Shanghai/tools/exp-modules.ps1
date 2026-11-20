# 终极鉴定：跑游戏 8 秒，列出进程实际加载的 d3d/dgvo/ddraw 模块路径
$game = 'D:\YunGame\IndieGame\shanghai'
$action = New-ScheduledTaskAction -Execute (Join-Path $game 'shanghai.exe') -Argument '-windows' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiMod' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiMod'
$deadline = (Get-Date).AddSeconds(35); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 700
    $p = Get-Process shanghai -ErrorAction SilentlyContinue
    if ($p) { $gp = $p.Id; break }
}
Start-Sleep -Seconds 8
$p = Get-Process -Id $gp -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "进程模块（d3d/ddraw/dgvo/dxgi 相关）："
    $p.Modules | Where-Object { $_.ModuleName -match 'd3d|ddraw|dgvo|dxgi' } |
        ForEach-Object { Write-Host ("  {0,-16} {1}" -f $_.ModuleName, $_.FileName) }
    Stop-Process -Id $gp -Force
}
$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) { $s = Get-Process shanghai -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Milliseconds 800 }
Unregister-ScheduledTask -TaskName 'ShanghaiMod' -Confirm:$false -ErrorAction SilentlyContinue
