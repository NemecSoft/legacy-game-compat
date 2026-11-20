<#
红警1复刻版启动器界面取证（只抓启动器自己的窗口，PrintWindow，不截用户桌面）：
  1 启动前无残留 game_starter 进程
  2 主窗口屏幕居中：窗口中心与工作区中心偏移必须 ≤1
  3 只有一个窗口（用户定案 2026-11-20：右栏一屏放全说明，不开独立详细窗口）
  4 主窗口截图 evidence\ui-main.png
  5 右栏一屏自证：日志「右栏按键表自检：行数=9 … 需要滚动=False」
  6 Esc 关主窗口，启动器正常退出（退出码 0）

用法：pwsh -NoProfile -File Games\CC1RedAlert\tools\ui-shot.ps1
结论另存 evidence\ui-shot-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\F\CC1-RedAlert'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public struct UIR { public int L, T, R, B; }
public class UI {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out UIR r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static System.Collections.Generic.List<IntPtr> Hits = new System.Collections.Generic.List<IntPtr>();
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid == Want && IsWindowVisible(h)) {
      StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity);
      if (t.Length > 0) Hits.Add(h);
    }
    return true;
  }
}
'@
$script:fails = 0
$script:lines = New-Object System.Collections.Generic.List[string]
function Assert([string]$name, [bool]$cond, [string]$detail) {
  $tag = if ($cond) { 'PASS' } else { $script:fails++; 'FAIL' }
  $line = "$tag  $name  $detail"
  Write-Host $line
  $script:lines.Add($line)
}
function Get-Windows([int]$procId) {
  [UI]::Want = [uint32]$procId
  [UI]::Hits = New-Object System.Collections.Generic.List[IntPtr]
  [UI]::EnumWindows([UI+EnumProc] { param($h, $l) [UI]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
  return [UI]::Hits
}
function Get-Rect([IntPtr]$h) {
  $r = New-Object UIR
  [UI]::GetWindowRect($h, [ref]$r) | Out-Null
  return $r
}
function Shot([IntPtr]$h, [string]$path) {
  $r = Get-Rect $h
  $w = $r.R - $r.L; $ht = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap $w, $ht
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [UI]::PrintWindow($h, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}
$exe = Join-Path $GameDir 'game_starter.exe'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$logPath = Join-Path $GameDir '运行日志.txt'

$leftover = @(Get-Process -Name 'game_starter' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
Assert '启动前无残留启动器进程' ($leftover.Count -eq 0) ("残留 $($leftover.Count) 个")
if ($leftover.Count -gt 0) { exit 1 }

$p = Start-Process -FilePath $exe -WorkingDirectory $GameDir -PassThru
try {
  Start-Sleep -Seconds 3
  $wins = Get-Windows $p.Id
  if ($wins.Count -lt 1) { throw '找不到启动器主窗口' }
  $main = $wins[0]
  $mr = Get-Rect $main
  $wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
  $cx = ($mr.L + $mr.R) / 2.0; $cy = ($mr.T + $mr.B) / 2.0
  $dx = [Math]::Abs($cx - ($wa.Left + $wa.Width / 2.0)); $dy = [Math]::Abs($cy - ($wa.Top + $wa.Height / 2.0))
  Assert '主窗口屏幕居中' ($dx -le 1 -and $dy -le 1) ("窗口 $($mr.R-$mr.L)×$($mr.B-$mr.T) 中心偏移 dx=$dx dy=$dy（工作区 $($wa.Width)×$($wa.Height)）")
  Assert '只有一个窗口（无独立详细窗口）' ($wins.Count -eq 1) ("可见窗口 $($wins.Count) 个")
  Shot $main (Join-Path $evidence 'ui-main.png')

  # 右栏一屏自证：启动器自己量的内容高 / 视口高，需要滚动必须为 False
  $logText = if (Test-Path $logPath) { [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) } else { '' }
  $m = [regex]::Matches($logText, '右栏按键表自检：行数=(\d+) 内容高=(\d+) 视口高=(\d+) 需要滚动=(True|False)')
  $last = if ($m.Count -gt 0) { $m[$m.Count - 1] } else { $null }
  $fitOk = $last -and $last.Groups[1].Value -eq '9' -and $last.Groups[4].Value -eq 'False'
  Assert '右栏一屏放全（行数 9、需要滚动 False）' $fitOk $(if ($last) { "行数 $($last.Groups[1].Value) 内容高 $($last.Groups[2].Value) 视口高 $($last.Groups[3].Value) 需要滚动 $($last.Groups[4].Value)" } else { '日志里没有右栏自检行' })

  # Esc 关主窗口（未启动游戏，应直接退出，退出码 0）。
  # 键消息 Post 进主窗口线程队列：WinForms 的 ProcessCmdKey 只在消息循环
  # PreTranslateMessage 里走，SendMessage 直入 WndProc 会绕过它；
  # keybd_event 走系统输入队列又依赖前台（后台脚本抢前台会被锁拒绝）。
  [UI]::PostMessageW($main, 0x0100, [IntPtr]0x1B, [IntPtr]::Zero) | Out-Null
  [UI]::PostMessageW($main, 0x0101, [IntPtr]0x1B, [IntPtr]::Zero) | Out-Null
  $exited = $p.WaitForExit(8000)
  Assert 'Esc 关窗正常退出' ($exited -and $p.ExitCode -eq 0) ("退出=$exited 退出码=$($p.ExitCode)")
}
finally {
  if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  $summary = "ui-shot 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'ui-shot-result.txt'), $script:lines, (New-Object Text.UTF8Encoding($false)))
  Write-Host $summary
}
exit $script:fails