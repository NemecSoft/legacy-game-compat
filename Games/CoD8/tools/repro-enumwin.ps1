# 试验 D：裸跑游戏 + 枚举它的全部窗口/对话框，抓弹框真实文字
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinEnum {
  public delegate bool EnumProc(IntPtr h, IntPtr lp);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string title);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextLengthW(IntPtr h);
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowTextW(h, sb, 512); return sb.ToString(); }
  public static string Cls(IntPtr h) { var sb = new StringBuilder(256); GetClassNameW(h, sb, 256); return sb.ToString(); }
  public static List<string> ChildTexts(IntPtr dlg) {
    var r = new List<string>(); IntPtr c = IntPtr.Zero;
    while ((c = FindWindowExW(dlg, c, null, null)) != IntPtr.Zero) {
      string t = Title(c); if (t.Length > 0) r.Add(t);
    }
    return r;
  }
}
"@

$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
Write-Output ("游戏 pid=" + $p.Id)
$seen = @{}
for ($i = 1; $i -le 12; $i++) {
  Start-Sleep -Milliseconds 2500
  if ($p.HasExited) { Write-Output ("t=" + ($i*2.5) + "s 游戏已退出 退出码=" + $p.ExitCode); break }
  [WinEnum+EnumProc]$cb = {
    param($h, $lp)
    try {
      [uint32]$wpid = 0; [WinEnum]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
      if ($wpid -eq $p.Id -and [WinEnum]::IsWindowVisible($h)) {
        $cls = [WinEnum]::Cls($h); $title = [WinEnum]::Title($h)
        $key = $cls + '|' + $title
        if (-not $seen[$key]) {
          $seen[$key] = $true
          Write-Output ("t=" + ($i*2.5) + "s 窗口: 类=" + $cls + " 标题=[" + $title + "]")
          if ($cls -eq '#32770') {
            [WinEnum]::ChildTexts($h) | ForEach-Object { Write-Output ("    框内文字: " + $_) }
          }
        }
      }
    } catch {}
    return $true
  }
  [WinEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; Write-Output '观察结束，已结束游戏进程' }
