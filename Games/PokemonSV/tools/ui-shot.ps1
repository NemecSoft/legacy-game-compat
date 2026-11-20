<#
宝可梦朱紫启动器界面取证（只抓启动器自己的窗口，PrintWindow，不截用户桌面）：
  1 主窗口屏幕居中：窗口中心与工作区中心偏移必须为 0
  2 主窗口截图 evidence\ui-main.png
  3 键盘 Tab×5 + Enter 打开「详细按键」独立普通窗口（非模态）
  4 两窗口并排重叠面积必须为 0；详细窗口截图 evidence\ui-cheatsheet.png
  5 详细窗口日志自证「需滚动 0px」（一页看全，不用滚轮）
  6 Esc 关详细窗口后主窗口放回原位（截图 evidence\ui-main-back.png）
  7 Esc 关主窗口，启动器正常退出（退出码 0）

用法：pwsh -NoProfile -File Games\PokemonSV\tools\ui-shot.ps1
结论另存 evidence\ui-shot-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\Z\PokemonScarletandViolet'
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
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr e);
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
function Tap([byte]$vk) {
  [UI]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [UI]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 120
}

$exe = Join-Path $GameDir 'game_starter.exe'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$logPath = Join-Path $GameDir '运行日志.txt'
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
  Shot $main (Join-Path $evidence 'ui-main.png')

  # Tab×5 焦点走到「详细按键」，Enter 打开独立普通窗口
  [UI]::SetForegroundWindow($main) | Out-Null
  Start-Sleep -Milliseconds 300
  for ($i = 0; $i -lt 5; $i++) { Tap 0x09 }
  Tap 0x0D
  Start-Sleep -Seconds 2
  $wins2 = Get-Windows $p.Id
  $sheet = [IntPtr]::Zero
  foreach ($h in $wins2) {
    $sb = New-Object System.Text.StringBuilder 256
    [UI]::GetWindowTextW($h, $sb, 256) | Out-Null
    if ($sb.ToString().Contains('完整按键速查')) { $sheet = $h }
  }
  if ($sheet -eq [IntPtr]::Zero) { Assert '详细窗口已打开' $false 'Tab×5+Enter 后没找到「完整按键速查」窗口' }
  else {
    $sr = Get-Rect $sheet
    $mr2 = Get-Rect $main
    $ox = [Math]::Max(0, [Math]::Min($mr2.R, $sr.R) - [Math]::Max($mr2.L, $sr.L))
    $oy = [Math]::Max(0, [Math]::Min($mr2.B, $sr.B) - [Math]::Max($mr2.T, $sr.T))
    Assert '两窗口并排重叠为 0' (($ox * $oy) -eq 0) ("主窗口 ($($mr2.L),$($mr2.T))-($($mr2.R),$($mr2.B)) 详细 ($($sr.L),$($sr.T))-($($sr.R),$($sr.B)) 重叠=$($ox*$oy)px²")
    Shot $sheet (Join-Path $evidence 'ui-cheatsheet.png')
    $logText = if (Test-Path $logPath) { [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) } else { '' }
    $m = [regex]::Matches($logText, '详细按键窗口：(\d+)×(\d+)，内容高 (\d+)，视口 (\d+)，需滚动 (\d+)px')
    $last = if ($m.Count -gt 0) { $m[$m.Count - 1] } else { $null }
    $scrollOk = $last -and $last.Groups[5].Value -eq '0'
    Assert '详细窗口一页看全（滚动 0）' $scrollOk $(if ($last) { "窗口 $($last.Groups[1].Value)×$($last.Groups[2].Value) 内容 $($last.Groups[3].Value) 视口 $($last.Groups[4].Value) 滚动 $($last.Groups[5].Value)px" } else { '日志里没有详细窗口尺寸行' })

    # Esc 关详细窗口，主窗口应放回原位
    [UI]::SetForegroundWindow($sheet) | Out-Null
    Start-Sleep -Milliseconds 300
    Tap 0x1B
    Start-Sleep -Seconds 1
    $mr3 = Get-Rect $main
    $backOk = ($mr3.L -eq $mr.L) -and ($mr3.T -eq $mr.T)
    Assert '关详细窗口后主窗口回原位' $backOk ("原位 ($($mr.L),$($mr.T)) 现在 ($($mr3.L),$($mr3.T))")
    Shot $main (Join-Path $evidence 'ui-main-back.png')
  }

  # Esc 关主窗口（未启动游戏，应直接退出，退出码 0）
  [UI]::SetForegroundWindow($main) | Out-Null
  Start-Sleep -Milliseconds 300
  Tap 0x1B
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