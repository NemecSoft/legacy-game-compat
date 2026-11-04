param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$Exe = 'game_starter.exe',
  [string]$Out = '',
  [int]$WaitSec = 4
)
# 通用启动器取证截图：只用 PrintWindow 抓启动器自己的窗口（技能配方 §9）。
# 禁止 CopyFromScreen 截全屏——那会把用户桌面上的其他窗口一起带进证据里。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public struct RRC { public int L, T, R, B; }
public class LS {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RRC r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want; public static int Area = 0;
  public static IntPtr Best2 = IntPtr.Zero;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity);
    if (t.Length == 0) return true;
    RRC r; GetWindowRect(h, out r);
    int a = (r.R - r.L) * (r.B - r.T);
    if (a > Area) { Area = a; Best2 = h; }
    return true;
  }
}
'@
$exePath = Join-Path $GameDir $Exe
if (-not (Test-Path $exePath)) { throw "找不到 $exePath" }
if ($Out -eq '') { $Out = Join-Path $GameDir 'launcher-shot.png' }
$p = Start-Process -FilePath $exePath -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds $WaitSec
try {
  [LS]::Want = [uint32]$p.Id
  [LS]::EnumWindows([LS+EnumProc] { param($h, $l) [LS]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
  $hwnd = [LS]::Best2
  if ($hwnd -eq [IntPtr]::Zero) { throw '找不到启动器窗口' }
  $r = New-Object RRC
  [LS]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  $ok = [LS]::PrintWindow($hwnd, $hdc, 2)     # PW_RENDERFULLCONTENT
  $g.ReleaseHdc($hdc); $g.Dispose()
  if (-not $ok) { $bmp.Dispose(); throw 'PrintWindow 失败' }
  $bmp.Save($Out); $bmp.Dispose()
  Write-Output ("SHOT $Out  ($w x $h，只含启动器自己的窗口)")
} finally {
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
}
