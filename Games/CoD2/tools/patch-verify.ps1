param([string]$GameDir = 'D:\YunGame\CoD\CoD2', [switch]$AllowGameLaunch)

# 一次性验证「安全模式询问框补丁」：种一个外来 PID 的标记 → 直接双击路径拉起游戏 exe（不走启动器，
# 证明不靠启动器删标记也不弹框）→ 全程枚举该 pid 的顶层窗口，出现 #32770 对话框即 FAIL →
# 等到真正的 CoD2 游戏窗口 → WM_CLOSE 正常退出 → 标记应被游戏自己删掉。
# 会启动游戏一次：没有 -AllowGameLaunch 直接 exit 90（规则 24 第 14 类 / pitfalls 84 的门禁）。
# 结论另存 UTF-8 文件再读，不解析 stdout 中文（规则 38）。

if (-not $AllowGameLaunch) {
  Write-Output '拒绝：本脚本会真启动游戏一次。确认要跑请加 -AllowGameLaunch（用户明令禁止反复启动游戏）'
  exit 90
}
$ErrorActionPreference = 'Stop'
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public struct WR2 { public int L, T, R, B; }
public class PV {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out WR2 r);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want; public static List<string> Seen = new List<string>();
  public static IntPtr Game = IntPtr.Zero; public static bool Dialog;
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    StringBuilder c = new StringBuilder(64); GetClassNameW(h, c, 64);
    string cls = c.ToString();
    if (!Seen.Contains(cls)) Seen.Add(cls);
    if (cls == "#32770") Dialog = true;
    if (cls == "CoD2") { WR2 r; GetWindowRect(h, out r); if (r.R - r.L > 200) Game = h; }
    return true;
  }
  public static void Scan(uint pid) { Want = pid; Game = IntPtr.Zero; Dialog = false; Seen.Clear(); EnumWindows(new EnumProc(Cb), IntPtr.Zero); }
}
'@
$out = Join-Path $PSScriptRoot '..\evidence\patch-verify.txt'
$fail = 0
$lines = New-Object System.Collections.Generic.List[string]
function Check($ok, [string]$name, [string]$detail) {
  if ($ok) { $lines.Add('PASS ' + $name) } else { $script:fail++; $lines.Add('FAIL ' + $name + '  ' + $detail) }
}
$mark = Join-Path $GameDir '__CoD2SP_s'
[IO.File]::WriteAllBytes($mark, [byte[]](0x04, 0x00, 0x00, 0x00))   # 外来 PID=4，原版逻辑必弹框
$p = Start-Process -FilePath (Join-Path $GameDir 'CoD2SP_s.exe') -WorkingDirectory $GameDir -PassThru
$dialogSeen = $false; $gameAt = 0; $rect = ''
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt 40 -and -not $p.HasExited) {
  [PV]::Scan([uint32]$p.Id)
  if ([PV]::Dialog) { $dialogSeen = $true }
  if ([PV]::Game -ne [IntPtr]::Zero -and $gameAt -eq 0) {
    $gameAt = [int]$sw.Elapsed.TotalSeconds
    $r = New-Object WR2; [PV]::GetWindowRect([PV]::Game, [ref]$r) | Out-Null
    $rect = ('{0}x{1}' -f ($r.R - $r.L), ($r.B - $r.T))
    break
  }
  Start-Sleep -Milliseconds 400
}
Check (-not $dialogSeen) '种了标记也没弹任何对话框（#32770 全程没出现）' '补丁没生效，玩家还是会看到乱码询问框'
Check ($gameAt -gt 0) ('游戏真窗口出现（第 ' + $gameAt + 's，' + $rect + '）') '40s 内没等到 CoD2 窗口，补丁可能把启动弄坏了'
if ([PV]::Game -ne [IntPtr]::Zero) { [PV]::PostMessageW([PV]::Game, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
if (-not $p.WaitForExit(20000)) { $p.Kill(); $lines.Add('NOTE 20s 没退完，强杀收尾') }
Check (-not (Test-Path $mark)) '退出后标记被游戏自己删掉（补丁不再写标记）' '标记还在：看是不是强杀收尾'
$lines.Add('INFO 窗口类 seen=' + ([string]::Join(',', [PV]::Seen)) + ' exit=' + $p.ExitCode)
[IO.File]::WriteAllLines($out, $lines, (New-Object Text.UTF8Encoding $false))
$lines | ForEach-Object { Write-Output $_ }
Write-Output ('结论文件：' + $out)
exit $fail