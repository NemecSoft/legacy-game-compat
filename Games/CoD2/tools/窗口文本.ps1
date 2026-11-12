# 抓某个进程当前所有窗口/子控件的文本（错误框常是乱码截图，读控件文本才准）。
# 用法：pwsh -NoProfile -File Games\CoD2\tools\窗口文本.ps1 -Process CoD2SP_s
param(
  [Parameter(Mandatory = $true)][string]$Process,
  [int]$TimeoutSec = 20,
  # 中文一律另存 UTF-8 文件再读，不靠控制台代码页（AGENTS 规则 38）
  [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
using System.Collections.Generic;
public class WText {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLengthA(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextA(IntPtr h, byte[] s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RR r);
  public struct RR { public int L, T, R, B; }
  public static uint Want;
  public static List<string> Lines = new List<string>();
  static string Geo(IntPtr h) {
    RR q; GetWindowRect(h, out q);
    return string.Format(" @{0},{1} {2}x{3}", q.L, q.T, q.R - q.L, q.B - q.T);
  }
  static string Txt(IntPtr h) {
    StringBuilder s = new StringBuilder(1024); GetWindowTextW(h, s, 1024);
    StringBuilder c = new StringBuilder(256); GetClassNameW(h, c, 256);
    // 繁中汉化包把 Big5 字节按 ACP=936 转成 Unicode，界面就成了乱码。
    // ANSI 侧取回来的字节才是原始 Big5（WideCharToMultiByte 会把 PUA 还原成原来那对字节），
    // 所以这里同时打出十六进制，交给 Python 按 Big5 反读。
    int len = GetWindowTextLengthA(h);
    string hex = "";
    if (len > 0) {
      byte[] b = new byte[len + 1];
      int n = GetWindowTextA(h, b, b.Length);
      StringBuilder hb = new StringBuilder(n * 3);
      for (int i = 0; i < n; i++) { hb.Append(b[i].ToString("X2")); hb.Append(' '); }
      hex = "  ANSI[" + hb.ToString().TrimEnd() + "]";
    }
    return c + " | " + s + hex;
  }
  public static bool Child(IntPtr h, IntPtr l) { Lines.Add("    " + Txt(h) + Geo(h)); return true; }
  public static bool Top(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want) return true;
    Lines.Add(string.Format("窗口 vis={0}{1}", IsWindowVisible(h), Geo(h)) + " " + Txt(h));
    EnumChildWindows(h, new EnumProc(Child), IntPtr.Zero);
    return true;
  }
  public static List<string> Dump(uint pid) {
    Want = pid; Lines = new List<string>();
    EnumWindows(new EnumProc(Top), IntPtr.Zero);
    return Lines;
  }
}
'@
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$p = $null
while ((Get-Date) -lt $deadline) {
  $p = Get-Process $Process -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($p) { break }
  Start-Sleep -Milliseconds 200
}
if (-not $p) { Write-Output "没找到进程 $Process"; exit 2 }
$lines = [WText]::Dump([uint32]$p.Id)
Write-Output ("pid={0} 窗口/控件 {1} 项" -f $p.Id, $lines.Count)
$lines | ForEach-Object { Write-Output $_ }
if ($Out) {
  [IO.File]::WriteAllLines([IO.Path]::GetFullPath($Out), $lines, (New-Object System.Text.UTF8Encoding($false)))
  Write-Output ("已存 UTF-8：" + $Out)
}
exit 0
