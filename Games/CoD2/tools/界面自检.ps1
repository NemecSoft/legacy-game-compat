param(
  [string]$GameDir = 'D:\YunGame\CoD\CoD2',
  [string]$OutDir = 'D:\AI\legacy-game-compat\Games\CoD2\evidence'
)

# 界面自检（不启动游戏）：开一次 game_starter.exe 的窗口，量三件事 + 存两张证据图。
#   1) 窗口中心与屏幕工作区中心的偏移必须为 0（规则 28：CenterScreen 不许被后来的改尺寸顶歪）；
#   2) 自绘下拉能展开，弹层每一项都要显示全（规则 20：只加宽弹层，框宽不许动）；
#   3) 抓完图进程必须自己退掉，不许留后台窗口。
# 取证只用 PrintWindow 抓启动器自己的窗口，禁止 CopyFromScreen（会把用户桌面别的窗口带进证据，踩过两次）。

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public struct WR { public int L, T, R, B; }
public class UI {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out WR r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out WR r);
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
  [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int index);
  [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static IntPtr Best = IntPtr.Zero; public static int BestArea = 0;
  public static List<IntPtr> Tops = new List<IntPtr>();
  public static List<IntPtr> Kids = new List<IntPtr>();
  public static bool TopCb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    Tops.Add(h);
    StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity);
    if (t.Length == 0) return true;
    WR r; GetWindowRect(h, out r);
    int area = (r.R - r.L) * (r.B - r.T);
    if (area > BestArea) { BestArea = area; Best = h; }
    return true;
  }
  public static bool KidCb(IntPtr h, IntPtr l) { Kids.Add(h); return true; }
  public static void Scan(uint pid) {
    Want = pid; Best = IntPtr.Zero; BestArea = 0; Tops.Clear();
    EnumWindows(new EnumProc(TopCb), IntPtr.Zero);
  }
  public static void KidsOf(IntPtr h) { Kids.Clear(); EnumChildWindows(h, new EnumProc(KidCb), IntPtr.Zero); }
}
'@
function Get-Bmp([IntPtr]$hwnd) {
  $r = New-Object WR
  if (-not [UI]::GetWindowRect($hwnd, [ref]$r)) { return $null }
  $w = $r.R - $r.L; $h = $r.B - $r.T
  if ($w -le 0 -or $h -le 0) { return $null }
  $b = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($b)
  $hdc = $g.GetHdc()
  $ok = [UI]::PrintWindow($hwnd, $hdc, 2)      # PW_RENDERFULLCONTENT
  $g.ReleaseHdc($hdc); $g.Dispose()
  if (-not $ok) { $b.Dispose(); return $null }
  return @{ Bmp = $b; Rect = $r }
}
function Save-Composited([IntPtr]$main, [IntPtr[]]$extras, [string]$path) {
  $m = Get-Bmp $main
  if (-not $m) { throw "PrintWindow 主窗口失败：$path" }
  $cg = [System.Drawing.Graphics]::FromImage($m.Bmp)
  foreach ($h in $extras) {
    $p = Get-Bmp $h
    if (-not $p) { continue }
    $cg.DrawImage($p.Bmp, ($p.Rect.L - $m.Rect.L), ($p.Rect.T - $m.Rect.T))
    $p.Bmp.Dispose()
  }
  $cg.Dispose()
  New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
  $m.Bmp.Save($path); $m.Bmp.Dispose()
}

$fail = 0
function Check($ok, [string]$name, [string]$detail) {
  if ($ok) { Write-Output ('PASS ' + $name) }
  else { $script:fail++; Write-Output ('FAIL ' + $name + '  ' + $detail) }
}

$exe = Join-Path $GameDir 'game_starter.exe'
if (-not (Test-Path $exe)) { Write-Output ('FAIL 找不到 ' + $exe + '（先跑 build.ps1）'); exit 1 }
# 不带任何参数 = 交互界面，不会拉起游戏（/go 才会）
$p = Start-Process -FilePath $exe -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds 3
try {
  [UI]::Scan([uint32]$p.Id)
  $hwnd = [UI]::Best
  Check ($hwnd -ne [IntPtr]::Zero) '找到启动器主窗口' 'EnumWindows 没量到窗口'
  if ($hwnd -eq [IntPtr]::Zero) { throw '没有窗口可测' }
  [UI]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 400
  [UI]::Scan([uint32]$p.Id)

  $r = New-Object WR
  [UI]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
  $w = $r.R - $r.L; $h = $r.B - $r.T
  # 偏移按「整数像素居中」判：窗口外径是奇数（649）而工作区高度是偶数（1048）时，
  # 中心只能对齐到半像素，真正的判据是左上角落在 floor((工作区 - 窗口)/2) 上。
  $expL = $wa.X + [int][Math]::Floor(($wa.Width - $w) / 2)
  $expT = $wa.Y + [int][Math]::Floor(($wa.Height - $h) / 2)
  $offX = $r.L - $expL; $offY = $r.T - $expT
  Check ($offX -eq 0 -and $offY -eq 0) ('窗口在屏幕正中（左上角比理论居中点偏 ' + $offX + ',' + $offY + '）') `
    ("窗口 $($r.L),$($r.T)-$($r.R),$($r.B)；工作区 $wa；应落 $expL,$expT —— 偏移不为 0 就是 CenterScreen 被后来的改尺寸顶歪了（规则 28）")
  Check ($w -le $wa.Width -and $h -le $wa.Height) ("窗口 $w x $h 在工作区内") '窗口超出屏幕，底边/右边点不到'

  # 客户区必须还是规范基准 900×620（按 DPI 缩放），不许为了塞内容撑大
  $dc = [UI]::GetDC([IntPtr]::Zero)
  $dpiX = [UI]::GetDeviceCaps($dc, 88)      # LOGPIXELSX = 88
  [UI]::ReleaseDC([IntPtr]::Zero, $dc) | Out-Null
  $scale = if ($dpiX -gt 0) { $dpiX / 96.0 } else { 1.0 }
  $cr = New-Object WR
  [UI]::GetClientRect($hwnd, [ref]$cr) | Out-Null
  $expW = [Math]::Min([int][Math]::Round(900 * $scale), $wa.Width - 30)
  $expH = [Math]::Min([int][Math]::Round(620 * $scale), $wa.Height - 30)
  Check ($cr.R -eq $expW -and $cr.B -eq $expH) ("客户区 $($cr.R)x$($cr.B) = 规范基准 900×620（缩放 $scale）") `
    "应为 ${expW}x${expH}；实际 $($cr.R)x$($cr.B) —— 尺寸没在构造函数里一步到位，或被撑大了"

  $plain = Join-Path $OutDir 'launcher.png'
  Save-Composited $hwnd @() $plain
  Write-Output ('SHOT ' + $plain + '（PrintWindow，只含启动器自己）')

  # 展开自绘下拉：走键盘（Tab → 焦点到下拉，Enter → 展开），比合成点击可靠（坐标一偏就点到启动按钮）
  [UI]::KidsOf($hwnd)
  Check ([UI]::Kids.Count -ge 1) '自绘下拉面板是启动器的子窗口' 'EnumChildWindows 没找到面板'
  $before = @([UI]::Tops.ToArray())
  function Key([int]$vk) {
    [UI]::PostMessageW($hwnd, 0x0100, [IntPtr]$vk, [IntPtr]0) | Out-Null   # WM_KEYDOWN
    Start-Sleep -Milliseconds 150
    [UI]::PostMessageW($hwnd, 0x0101, [IntPtr]$vk, [IntPtr]0) | Out-Null   # WM_KEYUP
    Start-Sleep -Milliseconds 250
  }
  Key 9    # Tab
  Key 13   # Enter
  Start-Sleep -Milliseconds 600
  [UI]::Scan([uint32]$p.Id)
  $new = @([UI]::Tops.ToArray() | Where-Object { $before -notcontains $_ })
  Check ($new.Count -ge 1) ('下拉弹层能展开（新增 ' + $new.Count + ' 个顶层窗口）') 'Tab+Enter 后没有新窗口'
  $drop = Join-Path $OutDir 'launcher-dropdown.png'
  Save-Composited $hwnd $new $drop
  Write-Output ('SHOT ' + $drop)
  # 焦点环证据就在 launcher-dropdown.png 里（Tab 之后框上那圈烫金环 + 展开的弹层），不再另存重复图
} finally {
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 500
}
$left = @(Get-Process -Name 'game_starter' -ErrorAction SilentlyContinue)
Check ($left.Count -eq 0) '截完图 game_starter 进程已退出' ('还留着 ' + (($left | ForEach-Object { $_.Id }) -join ','))
$game = @(Get-Process -Name 'CoD2SP_s' -ErrorAction SilentlyContinue)
Check ($game.Count -eq 0) '全程没有拉起游戏' 'CoD2SP_s 在跑 —— 这条脚本不该启动游戏'
Write-Output '===================='
if ($fail -eq 0) { Write-Output '界面自检：全部 PASS（没启动游戏）'; exit 0 }
Write-Output ('界面自检：' + $fail + ' 项 FAIL')
exit $fail
