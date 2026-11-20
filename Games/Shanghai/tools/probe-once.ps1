# 一次性实机实验体：由 run-probe.ps1 通过一次性计划任务拉起（脱离工具会话的进程树）。
# 只做：切桌面(可选) → 放层(可选) → 启动游戏 → 采集证据 → 无条件还原/清理。
param(
  [ValidateSet('none','dxvk','dgvoodoo')][string]$Layer = 'none',
  [ValidateSet('keep','800')][string]$Desktop = 'keep',
  [string]$GameArgs = '-windows',
  [string]$Tag = 'probe'
)
$ErrorActionPreference = 'Continue'
$game = 'D:\YunGame\IndieGame\shanghai'
$repo = 'D:\AI\legacy-game-compat'
$work = Join-Path $repo 'Games\Shanghai'
$evi  = Join-Path $work 'evidence'
if (-not (Test-Path $evi)) { New-Item -ItemType Directory -Force -Path $evi | Out-Null }
$log = Join-Path $evi ("probe-$Tag.txt")
if (Test-Path $log) { Remove-Item -LiteralPath $log -Force }

function W([string]$s) {
  Add-Content -LiteralPath $log -Value ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss.fff'), $s) -Encoding UTF8
}
function CurMode() {
  $v = Get-CimInstance Win32_VideoController |
       Where-Object { $_.CurrentHorizontalResolution -gt 0 -and $_.CurrentVerticalResolution -gt 0 } |
       Select-Object -First 1
  if ($v) { return @($v.CurrentHorizontalResolution, $v.CurrentVerticalResolution, $v.CurrentRefreshRate) }
  return @(1920, 1080, 60)
}
function SetMode([int]$w, [int]$h) {
  & (Join-Path $game 'SETRES.EXE') ("h$w") ("v$h") 2>&1 | ForEach-Object { W "setres: $_" }
  Start-Sleep -Milliseconds 900
}

$orig = CurMode
W "实验 $Tag / 层=$Layer / 桌面=$Desktop / 参数=$GameArgs"
W ("原始桌面 {0}x{1} @ {2}Hz" -f $orig[0], $orig[1], $orig[2])

$layerDir = Join-Path $work "tools\probe\$Layer"
$copied = New-Object System.Collections.Generic.List[string]
$dxvkLog = ''
try {
  if ($Desktop -eq '800') { SetMode 800 600; $m = CurMode; W ("切后桌面 {0}x{1}" -f $m[0], $m[1]) }

  if ($Layer -ne 'none') {
    foreach ($f in (Get-ChildItem -LiteralPath $layerDir -File |
                    Where-Object { $_.Name -ne 'D3D8Probe.exe' -and $_.Extension -ne '.log' })) {
      Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $game $f.Name) -Force
      $copied.Add($f.Name)
    }
    if ($Layer -eq 'dxvk') {
      $dxvkLog = Join-Path $game 'dxvk.conf'
      [IO.File]::WriteAllText($dxvkLog, "dxvk.hud = fps`r`n", [Text.UTF8Encoding]::new($false))
      $copied.Add('dxvk.conf')
    }
    W ("放入层文件：" + ($copied -join ', '))
    if ($Layer -eq 'dgvoodoo') {
      # 按本机实测桌面填强制输出分辨率（不硬编码 1920x1080），测试期临时开水印取证
      $conf = Join-Path $game 'dgVoodoo.conf'
      $mm = CurMode
      $txt = [IO.File]::ReadAllText($conf, [Text.Encoding]::UTF8)
      $txt = $txt.Replace('@DESKTOP@', ("{0}x{1}" -f $mm[0], $mm[1]))
      $txt = $txt.Replace('dgVoodooWatermark                   = false',
                          'dgVoodooWatermark                   = true')
      [IO.File]::WriteAllText($conf, $txt, [Text.UTF8Encoding]::new($false))
      W ("强制输出按实测桌面 = {0}x{1}；水印=开（仅本次取证）" -f $mm[0], $mm[1])
    }
  }

  if ([string]::IsNullOrWhiteSpace($GameArgs)) {
    $p = Start-Process -FilePath (Join-Path $game 'shanghai.exe') -WorkingDirectory $game -PassThru
  } else {
    $p = Start-Process -FilePath (Join-Path $game 'shanghai.exe') -ArgumentList $GameArgs `
                       -WorkingDirectory $game -PassThru
  }
  W ("已启动游戏 pid={0}" -f $p.Id)
  Start-Sleep -Seconds 16   # 让片头动画走完，截图才是主菜单而不是 LOGO

  $alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
  W ("16 秒后进程存活={0}" -f [bool]$alive)
  if ($alive) {
    W ("主窗口句柄=0x{0:X} 标题='{1}'" -f [int64]$alive.MainWindowHandle, $alive.MainWindowTitle)
    W ("进程 CPU={0}s 线程={1} 内存={2}MB" -f [math]::Round($alive.CPU,2), $alive.Threads.Count,
       [math]::Round($alive.WorkingSet64 / 1MB, 1))
  }
  $m2 = CurMode
  W ("游戏运行时桌面 {0}x{1} @ {2}Hz" -f $m2[0], $m2[1], $m2[2])

  # 游戏自己的日志最后几行（判断设备/初始化是否走通）
  foreach ($f in 'Logs\A3D.log','Logs\Errors.log') {
    $p2 = Join-Path $game $f
    if (Test-Path $p2) {
      W "---- $f (尾部) ----"
      Get-Content -LiteralPath $p2 -Tail 8 -Encoding Default | ForEach-Object { W "  $_" }
    }
  }

  # 截图取证：窗口自身矩形，允许 CopyFromScreen；不杀游戏，清理交给 finally
  $shot = Join-Path $evi ("probe-$Tag.png")
  try {
    & (Join-Path $repo 'scripts\capture-window.ps1') -Process 'shanghai' -Out $shot `
       -WaitSeconds 6 -KeepOpen 2>&1 | ForEach-Object { W "截图: $_" }
  } catch { W ("截图失败：" + $_.Exception.Message) }
}
catch { W ("实验异常：" + $_.Exception.Message) }
finally {
  # 无条件还原：桌面 / 进程 / 层文件 / 排障日志，一条不落
  Stop-Process -Name 'shanghai' -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 400
  SetMode $orig[0] $orig[1]
  foreach ($f in $copied) {
    Remove-Item -LiteralPath (Join-Path $game $f) -Force -ErrorAction SilentlyContinue
  }
  Get-ChildItem -LiteralPath $game -Filter '*_d3d9.log' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
  $m3 = CurMode
  W ("还原后桌面 {0}x{1} @ {2}Hz" -f $m3[0], $m3[1], $m3[2])
  W ("清理层文件：" + (($copied | ForEach-Object { $_ }) -join ', '))
  W '清理完成'
}
