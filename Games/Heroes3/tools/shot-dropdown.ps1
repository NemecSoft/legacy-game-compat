param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [string]$Shot = 'D:\AI\legacy-game-compat\Games\Heroes3\evidence\h3-launcher-dropdown.png'
)
# 只用 PrintWindow 抓启动器自己的窗口（含自绘下拉弹层），绝不 CopyFromScreen 截全屏：
# 截全屏会把用户桌面上别的窗口内容一起带进证据里（已经踩过两次）。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public struct RR { public int L, T, R, B; }
public class S2 {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RR r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static IntPtr Best = IntPtr.Zero;
  public static int BestArea = 0;
  public static List<IntPtr> Kids = new List<IntPtr>();
  public static List<IntPtr> Tops = new List<IntPtr>();
  public static bool CbTop(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    Tops.Add(h);
    StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity);
    if (t.Length == 0) return true;
    RR r; GetWindowRect(h, out r);
    int area = (r.R - r.L) * (r.B - r.T);
    if (area > BestArea) { BestArea = area; Best = h; }
    return true;
  }
  public static bool CbKid(IntPtr h, IntPtr l) { Kids.Add(h); return true; }
  public static void Reset() { Best = IntPtr.Zero; BestArea = 0; Kids.Clear(); }
  public static void Mark() { Tops.Clear(); }
}
'@
function Find-Main([uint32]$targetPid) {
  [S2]::Reset(); [S2]::Want = $targetPid
  [S2]::EnumWindows([S2+EnumProc] { param($h, $l) [S2]::CbTop($h, $l) }, [IntPtr]::Zero) | Out-Null
}
function Find-Tops([uint32]$targetPid) {
  [S2]::Mark(); [S2]::Want = $targetPid
  [S2]::EnumWindows([S2+EnumProc] { param($h, $l) [S2]::CbTop($h, $l) }, [IntPtr]::Zero) | Out-Null
  return @([S2]::Tops.ToArray())
}
function Get-Bmp([IntPtr]$hwnd) {
  $r = New-Object RR
  if (-not [S2]::GetWindowRect($hwnd, [ref]$r)) { return $null }
  $w = $r.R - $r.L; $h = $r.B - $r.T
  if ($w -le 0 -or $h -le 0) { return $null }
  $b = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($b)
  $hdc = $g.GetHdc()
  $ok = [S2]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
  $g.ReleaseHdc($hdc); $g.Dispose()
  if (-not $ok) { $b.Dispose(); return $null }
  return @{ Bmp = $b; Rect = $r }
}

$p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds 3
try {
  Find-Main ([uint32]$p.Id)
  $hwnd = [S2]::Best
  if ($hwnd -eq [IntPtr]::Zero) { throw '找不到启动器窗口' }
  [S2]::ShowWindow($hwnd, 9) | Out-Null
  [S2]::SetWindowPos($hwnd, [IntPtr](-1), 60, 40, 0, 0, 0x0001 -bor 0x0004) | Out-Null
  [S2]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 700

  # 自绘下拉是启动器唯一的子窗口，点它自己的矩形中心（不靠猜坐标，避免点到启动按钮）
  [S2]::Kids.Clear()
  [S2]::EnumChildWindows($hwnd, [S2+EnumProc] { param($h, $l) [S2]::CbKid($h, $l) }, [IntPtr]::Zero) | Out-Null
  if ([S2]::Kids.Count -eq 0) { throw '找不到自绘下拉面板' }
  $sel = [S2]::Kids[0]
  $sr = New-Object RR
  [S2]::GetWindowRect($sel, [ref]$sr) | Out-Null
  Write-Output ('下拉面板 ' + ($sr.R - $sr.L) + 'x' + ($sr.B - $sr.T) + ' @ ' + $sr.L + ',' + $sr.T)
  $before = @(Find-Tops ([uint32]$p.Id))
  # 展开菜单：走键盘（Tab 到下拉 -> Enter 展开），比点面板可靠（点击会被 WinForms 的鼠标状态判定吃掉）
  function Key([int]$vk) {
    $k = [IntPtr]$vk
    [S2]::PostMessageW($hwnd, 0x0100, $k, [IntPtr]0) | Out-Null   # WM_KEYDOWN
    Start-Sleep -Milliseconds 120
    [S2]::PostMessageW($hwnd, 0x0101, $k, [IntPtr]0) | Out-Null   # WM_KEYUP
    Start-Sleep -Milliseconds 200
  }
  Key 9     # Tab -> 焦点落到自绘下拉
  Key 13    # Enter -> 展开菜单

  $after = @(Find-Tops ([uint32]$p.Id))
  $new = @($after | Where-Object { $before -notcontains $_ })
  Find-Main ([uint32]$p.Id)
  $main = Get-Bmp ([S2]::Best)
  if (-not $main) { throw 'PrintWindow 主窗口失败' }
  $canvas = New-Object System.Drawing.Bitmap ($main.Rect.R - $main.Rect.L), ($main.Rect.B - $main.Rect.T)
  $cg = [System.Drawing.Graphics]::FromImage($canvas)
  $cg.DrawImage($main.Bmp, 0, 0)
  $note = '没发现新弹层窗口'
  foreach ($h2 in $new) {
    $pb = Get-Bmp $h2
    if ($pb) {
      $cg.DrawImage($pb.Bmp, ($pb.Rect.L - $main.Rect.L), ($pb.Rect.T - $main.Rect.T))
      $note = ('弹层 {0}x{1} @ {2},{3}' -f ($pb.Rect.R - $pb.Rect.L), ($pb.Rect.B - $pb.Rect.T),
               ($pb.Rect.L - $main.Rect.L), ($pb.Rect.T - $main.Rect.T))
      $pb.Bmp.Dispose()
    }
  }
  $cg.Dispose()
  $canvas.Save($Shot); $canvas.Dispose(); $main.Bmp.Dispose()
  Write-Output ('窗口 ' + ($main.Rect.R - $main.Rect.L) + 'x' + ($main.Rect.B - $main.Rect.T))
  Write-Output $note
  Write-Output ('SHOT ' + $Shot + '（PrintWindow，不含桌面其他窗口）')
} finally {
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
}
