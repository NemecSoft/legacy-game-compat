# 试验 G：最低配 config 决定性试验（显存爆猜设）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
$cfg = Join-Path $game 'players2\config.cfg'
$raw = [Text.Encoding]::GetEncoding(28591)

# 0 字节坏 config 先收进 backup 留证
$cur = [IO.File]::ReadAllBytes($cfg)
if ($cur.Length -eq 0) { [IO.File]::WriteAllBytes((Join-Path $ev 'config.cfg.was-zero-bytes'), $cur) }

# 以备份 config 为底，改成最低配
$txt = $raw.GetString([IO.File]::ReadAllBytes('D:\AI\legacy-game-compat\Games\CoD8\backup\config.cfg.before-preloadtest'))
$patch = @{
  'r_displayMode'   = 'fullscreen'
  'r_mode'          = '1920x1080'
  'r_displayRefresh' = '59 Hz'
  'r_aaSamples'     = '1'
  'r_ssao'          = '0_Off'
  'sm_enable'       = '0'
  'sm_maxLights'    = '1'
  'r_picmip'        = '2'
  'r_picmip_bump'   = '2'
  'r_picmip_spec'   = '2'
  'r_glow_allowed'  = '0'
  'r_dof_enable'    = '0'
  'r_distortion'    = '0'
  'r_vsync'         = '0'
  'r_multiGpu'      = '0'
  'r_imageQuality'  = '0'
  'r_autopriority'  = '0'
  'r_preloadShaders'= '0'
  'intro'           = '0'
}
foreach ($k in $patch.Keys) {
  $pat = '(?m)^seta[ \t]+' + [Regex]::Escape($k) + '[ \t]+"[^"]*"'
  if ([Regex]::IsMatch($txt, $pat)) { $txt = [Regex]::Replace($txt, $pat, ('seta ' + $k + ' "' + $patch[$k] + '"')) }
}
[IO.File]::WriteAllBytes($cfg, $raw.GetBytes($txt))
Write-Output '最低配 config 已写入'

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
$alive = $false
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $alive = $true
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-lowcfg-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 主窗=$($gp.MainWindowHandle -ne 0) 响应=$($gp.Responding)")
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if (-not $p.HasExited) { $p.WaitForExit(10000) | Out-Null }
Write-Output ("退出码=" + $p.ExitCode + "（活过 80 秒=成功）")
