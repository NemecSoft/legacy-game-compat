# 试验 C：save 改名对照（引擎无存档启动）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$save = Join-Path $game 'players2\save'
$saveBak = Join-Path $game 'players2\save_off'

if (Test-Path $save) { Move-Item $save $saveBak -Force; Write-Output 'save 已改名 save_off（稍后原样移回）' }
$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 5; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-nosave-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(10000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode)
# 无论结果如何：把 save 移回来（玩家存档一秒都不能多留在外面）
if (Test-Path $saveBak) { Move-Item $saveBak $save -Force; Write-Output 'save 已原样移回' }
