param([string]$GameDir = 'D:\YunGame\E\Heroes3', [int]$WaitSec = 4)
# 键盘可达性实测：Tab 能不能走到下拉、↑/↓ 能不能换档（只看日志，不启动游戏）
$ErrorActionPreference = 'Stop'
Add-Type @'
using System; using System.Runtime.InteropServices;
public struct RR2 { public int L, T, R, B; }
public class K {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RR2 r);
  [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want; public static IntPtr Best = IntPtr.Zero; public static int Area = 0;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    System.Text.StringBuilder t = new System.Text.StringBuilder(256); GetWindowTextW(h, t, 256);
    if (t.Length == 0) return true;
    RR2 r; GetWindowRect(h, out r);
    int a = (r.R - r.L) * (r.B - r.T);
    if (a > Area) { Area = a; Best = h; }
    return true;
  }
}
'@
$log = Join-Path $GameDir '运行日志.txt'
if (Test-Path $log) { Remove-Item $log -Force }
$p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds $WaitSec
[K]::Want = [uint32]$p.Id
[K]::EnumWindows([K+EnumProc] { param($h, $l) [K]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
$hwnd = [K]::Best
if ($hwnd -eq [IntPtr]::Zero) { throw '找不到启动器窗口' }
[K]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 400
function Key([int]$vk) {
  $k = [IntPtr]$vk
  [K]::PostMessageW($hwnd, 0x0100, $k, [IntPtr]0) | Out-Null   # WM_KEYDOWN
  Start-Sleep -Milliseconds 120
  [K]::PostMessageW($hwnd, 0x0101, $k, [IntPtr]0) | Out-Null   # WM_KEYUP
  Start-Sleep -Milliseconds 250
}
Key 9    # Tab -> 焦点应当落到下拉
Key 40   # Down -> 第 1 档 -> 第 2 档
Key 40   # Down -> 第 2 档 -> 第 3 档
Key 39   # Right（不该有任何反应，确认没有误接按键）
Start-Sleep -Seconds 1
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
$txt = if (Test-Path $log) { Get-Content $log -Encoding UTF8 -Raw } else { '' }
$hits = @([regex]::Matches($txt, '选择显示方式：(.+)') | ForEach-Object { $_.Groups[1].Value })
Write-Output ('日志里的换档记录 ' + $hits.Count + ' 条：' + ($hits -join ' ｜ '))
Write-Output $(if ($hits.Count -ge 2) { 'PASS 键盘 Tab 能走到下拉，↓ 能换档' } else { 'FAIL 键盘换档没反应' })
exit $(if ($hits.Count -ge 2) { 0 } else { 1 })
