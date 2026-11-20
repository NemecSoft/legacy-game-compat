# DXVK 实测：--go 启动 → 游戏稳定运行验证（跑 30 秒再杀）+ 模块路径确认 + 截图
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$ev = 'D:\AI\legacy-game-compat\Games\Shanghai\evidence'

$action = New-ScheduledTaskAction -Execute (Join-Path $game 'game_starter.exe') -Argument '--go' -WorkingDirectory $game
Register-ScheduledTask -TaskName 'ShanghaiDxvk' -Action $action -Force | Out-Null
Start-ScheduledTask -TaskName 'ShanghaiDxvk'
$deadline = (Get-Date).AddSeconds(40); $gp = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    $p = Get-Process shanghai -ErrorAction SilentlyContinue | Where-Object { $_.Threads.Count -gt 0 } | Select-Object -First 1
    if ($p) { $gp = $p.Id; break }
}
Write-Host "游戏 pid=$gp"

# 模块确认：d3d8 应来自游戏目录（DXVK），d3d9/dxgi 同
if ($gp) {
    try {
        (Get-Process -Id $gp -Module -ErrorAction Stop) | Where-Object { $_.ModuleName -match '^(d3d8|d3d9|dxgi)\.dll$' } |
            ForEach-Object { Write-Host ("加载: " + $_.ModuleName + " <- " + $_.FileName) }
    } catch { Write-Host ("模块枚举失败: " + $_.Exception.Message) }
}

Start-Sleep -Seconds 25
$alive = Get-Process -Id $gp -ErrorAction SilentlyContinue
Write-Host ("35 秒后游戏存活: " + [bool]$alive + (if ($alive) { "（DXVK 稳定）" } else { "（崩了）" }))

& 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process 'shanghai' `
   -Out (Join-Path $ev 'verify-dxvk.png') -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { Write-Host "截图: $_" }

if ($alive) { Stop-Process -Id $gp -Force }
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) { $s = Get-Process game_starter -ErrorAction SilentlyContinue; if (-not $s) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 2
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host ("桌面: $($b.Width)x$($b.Height)")
Unregister-ScheduledTask -TaskName 'ShanghaiDxvk' -Confirm:$false
Get-Content (Join-Path $game '运行日志.txt') -Tail 6
