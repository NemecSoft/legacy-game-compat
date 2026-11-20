# 雷电4 真机验证（只跑一次）：--go 零交互启动 -> 取证截图 -> 窗口状态断言 -> 强退游戏 -> 退出码回传
# 强退是契约测试手段，不是崩溃；结论写 evidence\run-verify.txt
$ErrorActionPreference = 'Stop'
$g = 'D:\YunGame\G\Raiden4'
$ev = 'D:\AI\legacy-game-compat\Games\Raiden4\evidence'
$repo = 'D:\AI\legacy-game-compat'
New-Item -ItemType Directory -Force -Path $ev | Out-Null
$fail = 0
function Check([string]$Name, [bool]$Ok) {
  if ($Ok) { Write-Output ("合格 " + $Name) } else { Write-Output ("不合格 " + $Name); $script:fail = 1 }
}

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public struct RRC2 { public int L, T, R, B; }
public class WV {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RRC2 r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want; public static IntPtr Best = IntPtr.Zero; public static int Area = 0;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity);
    if (t.Length == 0) return true;
    RRC2 r; GetWindowRect(h, out r);
    int a = (r.R - r.L) * (r.B - r.T);
    if (a > Area) { Area = a; Best = h; }
    return true;
  }
  public static IntPtr Find(uint pid) { Want = pid; Best = IntPtr.Zero; Area = 0; EnumWindows(Cb, IntPtr.Zero); return Best; }
}
'@

# 启动前确认无旧局
Check '启动前无 TeknoParrotUi 残留' (@(Get-Process -Name TeknoParrotUi -ErrorAction SilentlyContinue).Count -eq 0)

$lp = Start-Process -FilePath (Join-Path $g 'game_starter.exe') -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("启动器 pid=" + $lp.Id)

# 等游戏进程（最长 120 秒）
$game = $null
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 2
  foreach ($p in @(Get-Process -Name game -ErrorAction SilentlyContinue)) {
    try { if ($p.MainModule.FileName -ieq (Join-Path $g 'game.exe')) { $game = $p; break } } catch {}
  }
  if ($game) { break }
  if ($lp.HasExited) { break }
}
Check '游戏进程已起来' ($null -ne $game)
if (-not $game) {
  Get-Content (Join-Path $g '运行日志.txt') -Encoding UTF8 -Tail 20
  exit 1
}
$null = $game.Handle  # 趁活持句柄，退出后才能读真实退出码
Write-Output ("游戏 pid=" + $game.Id + "，等画面渲染 10 秒")
Start-Sleep -Seconds 10

# 取证：游戏画面（统一走仓库 helper）+ 启动器窗口（PrintWindow 只抓自己）
$shotOk = $true
try {
  & (Join-Path $repo 'scripts\capture-window.ps1') -Process game -Out (Join-Path $ev 'game-running.png') -KeepOpen -WaitSeconds 45
} catch { $shotOk = $false; Write-Output ("游戏截图失败：" + $_.Exception.Message) }
Check '游戏画面截图' $shotOk
$lh = [WV]::Find([uint32]$lp.Id)
Check '启动器窗口可见（陪跑不隐藏）' ($lh -ne [IntPtr]::Zero -and -not [WV]::IsIconic($lh))
if ($lh -ne [IntPtr]::Zero) {
  $r = New-Object RRC2
  [WV]::GetWindowRect($lh, [ref]$r) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap(($r.R - $r.L), ($r.B - $r.T))
  $gr = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $gr.GetHdc()
  [WV]::PrintWindow($lh, $hdc, 2) | Out-Null
  $gr.ReleaseHdc($hdc); $gr.Dispose()
  $bmp.Save((Join-Path $ev 'launcher-running.png'), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output '已存 launcher-running.png'
}

# 窗口状态：UI 白窗口与 loader 控制台都应最小化，不抢游戏前台
$ui = Get-Process -Name TeknoParrotUi -ErrorAction SilentlyContinue | Select-Object -First 1
if ($ui) {
  $uh = [WV]::Find([uint32]$ui.Id)
  Check 'UI 窗口起始最小化' ($uh -eq [IntPtr]::Zero -or [WV]::IsIconic($uh))
} else { Check 'UI 进程在跑' $false }
$loaders = @(Get-Process -Name OpenParrotLoader* -ErrorAction SilentlyContinue)
$allMin = $true
foreach ($ld in $loaders) {
  $h = [WV]::Find([uint32]$ld.Id)
  if ($h -ne [IntPtr]::Zero -and -not [WV]::IsIconic($h)) { $allMin = $false }
}
Check 'loader 控制台不可见（隐藏或最小化）' ($loaders.Count -gt 0 -and $allMin)

# 强退游戏，验证退出码原样回传（契约测试，非崩溃）
$game.Kill()
$game.WaitForExit()
$gcode = $game.ExitCode
Write-Output ("游戏被强退，退出码=" + $gcode)
$exited = $lp.WaitForExit(30000)
Check '启动器随游戏退出' $exited
if ($exited) { Check '退出码原样回传' ($lp.ExitCode -eq $gcode) }
Check 'UI 进程已收尾' (@(Get-Process -Name TeknoParrotUi -ErrorAction SilentlyContinue).Count -eq 0)

# 日志自证
$log = Get-Content (Join-Path $g '运行日志.txt') -Encoding UTF8 -Raw
Check '日志：零交互=是' ($log -match '零交互：是')
Check '日志：原始参数 --go' ($log -match '启动参数：--go')
Check '日志：找到游戏进程' ($log -match '找到游戏进程 pid=')
Check '日志：退出码记录' ($log -match '游戏进程退出，退出码 0x')

$verdict = if ($fail -eq 0) { '真机验证全部通过' } else { '真机验证有不合格项' }
$lines = @($verdict, ('游戏退出码=' + $gcode), ('启动器退出码=' + $(if ($exited) { $lp.ExitCode } else { '未退出' })))
[IO.File]::WriteAllLines((Join-Path $ev 'run-verify.txt'), $lines, [Text.Encoding]::UTF8)
Write-Output $verdict
exit $fail