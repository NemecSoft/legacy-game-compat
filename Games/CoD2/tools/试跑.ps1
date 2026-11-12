# CoD2 取证跑一次：启动 -> 等窗口 -> 只用 PrintWindow 抓图（规则：禁止 CopyFromScreen）
# -> 关窗 -> 回传退出码，并列出这段时间游戏目录里新增/改动的文件（找配置落点）。
# 用法：pwsh -NoProfile -File Games\CoD2\tools\试跑.ps1 -Tag native
param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD2',
  [string]$Exe = 'CoD2SP_s.exe',
  [string]$ArgLine = '',
  [string]$Tag = 'run',
  [int]$WaitSec = 45,
  [int]$SecondShotSec = 0,
  [hashtable]$Env = @{},
  [switch]$KeepOpen
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
using System.Collections.Generic;
public class Shot {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint flags, IntPtr extra);
  public struct R { public int L, T, Rt, B; }

  // 弹框处理：CoD2 上次没正常退出会弹「是否以安全模式執行？」，
  // 繁中 Big5 在简化字机器上显示成乱码，玩家看不懂也不敢点；安全模式还会 exec safemode.cfg
  // 把画面降回 640×480 + dx7。取证时统一按「否」，不动玩家的画面设置。
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  public static uint Want; public static IntPtr Hit = IntPtr.Zero;
  public static IntPtr Btn = IntPtr.Zero;
  // 真正的游戏窗口是 class "CoD2"（全屏时 1920x1080），而 MainWindowHandle 会指到
  // 已经隐藏的 "CoD Splash Screen"（642x482）——按 MainWindowHandle 抓图会一直抓到开场那张小窗。
  public static IntPtr Best = IntPtr.Zero; public static int BestArea = 0;
  public static bool OnlyGame = false;
  public static bool Pick(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    bool isGame = Cls(h) == "CoD2";
    // OnlyGame：只认 class "CoD2" 的真游戏窗口。开场动画那张 "CoD Splash Screen"（642x482）
    // 也是可见窗口，不区分就会一直抓到小窗，量不到真实分辨率。
    if (OnlyGame && !isGame) return true;
    R q; GetWindowRect(h, out q);
    int area = (q.Rt - q.L) * (q.B - q.T);
    if (area <= 0) return true;
    bool isBest = Cls(Best) == "CoD2";
    if (Best == IntPtr.Zero || (isGame && (!isBest || area > BestArea)) || (!isGame && !isBest && area > BestArea)) {
      Best = h; BestArea = area;
    }
    return true;
  }
  public static IntPtr PickWindow(uint pid, bool onlyGame) {
    Want = pid; Best = IntPtr.Zero; BestArea = 0; OnlyGame = onlyGame;
    EnumWindows(new EnumProc(Pick), IntPtr.Zero);
    return Best;
  }
  static string Cls(IntPtr h) { StringBuilder s = new StringBuilder(64); GetClassNameW(h, s, 64); return s.ToString(); }
  public static bool IsDialog(IntPtr h) { return h != IntPtr.Zero && Cls(h) == "#32770"; }
  static string Txt(IntPtr h) { StringBuilder s = new StringBuilder(256); GetWindowTextW(h, s, 256); return s.ToString(); }
  public static bool Ch(IntPtr h, IntPtr l) {
    string t = Txt(h).Replace(" ", "");
    if (t.Contains("(&N)") || t.Contains("否(&N)") || t.StartsWith("否")) Btn = h;
    return true;
  }
  public static bool Tp(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h) || Cls(h) != "#32770") return true;
    Hit = h; Btn = IntPtr.Zero;
    EnumChildWindows(h, new EnumProc(Ch), IntPtr.Zero);
    return false;
  }
  // 返回 true = 点掉了弹框
  public static bool DismissNo(uint pid) {
    Want = pid; Hit = IntPtr.Zero; Btn = IntPtr.Zero;
    EnumWindows(new EnumProc(Tp), IntPtr.Zero);
    if (Hit == IntPtr.Zero) return false;
    IntPtr b = Btn != IntPtr.Zero ? Btn : Hit;
    PostMessage(b, 0x00F5, IntPtr.Zero, IntPtr.Zero);   // BM_CLICK
    return true;
  }
}
'@

function DarkRatio([System.Drawing.Bitmap]$bmp) {
  $sum = 0L; $n = 0
  for ($y = 4; $y -lt $bmp.Height; $y += 23) {
    for ($x = 4; $x -lt $bmp.Width; $x += 23) {
      $c = $bmp.GetPixel($x, $y); $sum += ($c.R + $c.G + $c.B); $n++
    }
  }
  if ($n -eq 0) { return -1.0 }
  [math]::Round($sum / $n / 765.0, 3)
}

$full = [IO.Path]::GetFullPath((Join-Path $GameDir $Exe))
if (-not (Test-Path $full)) { throw "主程序不存在：$full" }
$outDir = Join-Path $PSScriptRoot '..\evidence'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
$png = [IO.Path]::GetFullPath((Join-Path $outDir ("shot-{0}.png" -f $Tag)))
$png2 = [IO.Path]::GetFullPath((Join-Path $outDir ("shot-{0}-late.png" -f $Tag)))

$before = @{}
Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue |
  ForEach-Object { $before[$_.FullName] = $_.LastWriteTimeUtc }

foreach ($k in $Env.Keys) { if ($Env[$k]) { Set-Item -Path ("Env:{0}" -f $k) -Value $Env[$k] } else { Remove-Item ("Env:{0}" -f $k) -ErrorAction SilentlyContinue } }

$argl = @()
if ($ArgLine.Trim()) { $argl = $ArgLine.Trim() -split '\s+' }

# 上次没正常退出会留下标记文件（__CoD2SP_s），下次启动就弹「安全模式？」乱码框。
# 先清掉再跑；交付的启动器同样要做（规则 34：玩家只面对一个界面，不许让他猜乱码）。
$marker = Join-Path $GameDir ('__' + [IO.Path]::GetFileNameWithoutExtension($Exe))
if (Test-Path $marker) {
  Remove-Item $marker -Force
  Write-Output ("清掉上次运行标记：{0}" -f [IO.Path]::GetFileName($marker))
}

$t0 = Get-Date
$p = Start-Process -FilePath $full -ArgumentList $argl -WorkingDirectory $GameDir -PassThru
Write-Output ("pid={0} 参数=[{1}]" -f $p.Id, $ArgLine)

$h = [IntPtr]::Zero; $reason = '超时没等到窗口'
while (((Get-Date) - $t0).TotalSeconds -lt $WaitSec) {
  $p.Refresh()
  if ($p.HasExited) { $reason = '进程已退出'; break }
  # 先处理可能弹出的「安全模式？」框，再认主窗口
  if ([Shot]::DismissNo([uint32]$p.Id)) {
    Write-Output ("点掉弹框「安全模式？」→ 否（{0:HH:mm:ss}）" -f (Get-Date))
    Start-Sleep -Milliseconds 500; continue
  }
  $mw = [Shot]::PickWindow([uint32]$p.Id, $true)   # 只认 class "CoD2" 的真游戏窗口
  if ($mw -ne [IntPtr]::Zero) { $h = $mw; $reason = '有窗口'; break }
  Start-Sleep -Milliseconds 300
}

function TakeShot([IntPtr]$hwnd, [string]$path) {
  $r = New-Object Shot+R
  [Shot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L; $hh = $r.B - $r.T
  if ($w -le 0 -or $hh -le 0) { return "窗口尺寸无效 $w x $hh" }
  $bmp = New-Object System.Drawing.Bitmap $w, $hh
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc(); $ok = [Shot]::PrintWindow($hwnd, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
  if (-not $ok) { $bmp.Dispose(); return 'PrintWindow 失败' }
  $bmp.Save($path)
  $d = DarkRatio $bmp
  $bmp.Dispose()
  Write-Output ("  图={0} 尺寸={1}x{2} 平均亮度={3}（0=全黑）" -f $path, $w, $hh, $d)
  return "{0}x{1}" -f $w, $hh
}

$exitTxt = '仍在运行'
$shotTxt = '没抓到窗口'
$rectTxt = '-'
if ($reason -eq '有窗口') {
  $sb = New-Object System.Text.StringBuilder 256
  [Shot]::GetWindowTextW($h, $sb, 256) | Out-Null
  $shotTxt = ("标题=[{0}]" -f $sb.ToString())
  $rectTxt = TakeShot $h $png
  if ($SecondShotSec -gt 0 -and -not $p.HasExited) {
    Write-Output ("  等 {0}s 让片头放完进菜单" -f $SecondShotSec)
    Start-Sleep -Seconds $SecondShotSec
    $p.Refresh()
    if (-not $p.HasExited) {
      $h2 = [Shot]::PickWindow([uint32]$p.Id, $true)
      if ($h2 -ne [IntPtr]::Zero) { $h = $h2 }
      [void](TakeShot $h $png2)
    }
  }
  if (-not $KeepOpen) {
    [Shot]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null   # WM_CLOSE
    $p.WaitForExit(15000) | Out-Null
  }
}
if ($p.HasExited) {
  $c = $p.ExitCode
  $exitTxt = "0x{0:X8}（{0}）" -f $c
} elseif (-not $KeepOpen) {
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 500
}
Write-Output ("{0}｜{1}｜用时 {2}s｜退出码 {3}" -f $reason, $shotTxt, [math]::Round(((Get-Date) - $t0).TotalSeconds, 1), $exitTxt)
if ($dark -ge 0) { Write-Output ("图={0} 尺寸={1} 平均亮度={2}（0=全黑）" -f $png, $rectTxt, $dark) }

$changed = @()
Get-ChildItem $GameDir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
  if (-not $before.ContainsKey($_.FullName)) { $changed += ("新增 " + $_.FullName.Substring($GameDir.Length + 1)) }
  elseif ($_.LastWriteTimeUtc -gt $before[$_.FullName]) { $changed += ("改动 " + $_.FullName.Substring($GameDir.Length + 1)) }
}
Write-Output ("目录内变化 {0} 项" -f $changed.Count)
$changed | Select-Object -First 25 | ForEach-Object { Write-Output ("   " + $_) }
