param(
  [string]$Launcher = 'D:\YunGame\F\KingdomHeroes8\SG8Launcher.exe',
  [int]$WaitGameSec = 120,
  [switch]$LeaveRunning,
  [switch]$Attach,
  [switch]$NoInvoke,
  [switch]$KeepOnScreen
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$log = 'D:\YunGame\F\KingdomHeroes8\Runtime\logs\wpf-launcher.log'
$before = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$existing = Get-Process -Name SG8Launcher -ErrorAction SilentlyContinue
if ($Attach) {
  if (-not $existing) { throw '没有可附加的 SG8Launcher 实例' }
  $p = $existing | Select-Object -First 1
  Write-Output ("附加到已有实例 pid=$($p.Id)")
} elseif ($existing) {
  Write-Output ('注意：已有实例 ' + ($existing.Id -join ','))
  exit 2
} else {
  $p = Start-Process -FilePath $Launcher -WorkingDirectory (Split-Path $Launcher) -PassThru
  Write-Output ("launcher pid=$($p.Id)")
}

$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
$win = $null
for ($i = 0; $i -lt 20; $i++) {
  Start-Sleep -Milliseconds 700
  $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, [int]$p.Id)
  $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
  if ($null -ne $win) { break }
}
if ($null -eq $win) { throw 'no UIA window' }
Write-Output ('窗口标题: ' + $win.Current.Name + '  rect=' + $win.Current.BoundingRectangle)

# 默认把窗口移出屏幕（验证「玩家看不到原版界面」也能启动）；-KeepOnScreen 保持原位
$h = [IntPtr]$win.Current.NativeWindowHandle
$rc = New-Object M+RECT
[M]::GetWindowRect($h, [ref]$rc) | Out-Null
$w = $rc.Right - $rc.Left; $ht = $rc.Bottom - $rc.Top
if (-not $KeepOnScreen) {
  [M]::SetWindowPos($h, [IntPtr](-2), -32000, -32000, 0, 0, 0x0001 -bor 0x0004) | Out-Null
  [M]::GetWindowRect($h, [ref]$rc) | Out-Null
  Write-Output ("已移出屏幕 -> $($rc.Left),$($rc.Top) 可见=" + [M]::IsWindowVisible($h) + " 尺寸=${w}x${ht}")
} else {
  Write-Output ("保持原位 rect=$($rc.Left),$($rc.Top) 尺寸=${w}x${ht}")
}

# 找「启动游戏」按钮
$btn = $null
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
  if ($e.Current.ControlType.ProgrammaticName -notmatch 'Button') { continue }
  $kids = $e.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($k in $kids) {
    if ($k.Current.Name -match '启动游戏') { $btn = $e; break }
  }
  if ($null -ne $btn) { break }
}
if ($null -eq $btn) { throw '找不到启动游戏按钮' }
if ($NoInvoke) {
  Write-Output '按钮已定位（-NoInvoke：本次不点击）'
  exit 0
}
$inv = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
Write-Output '按钮已定位，准备 Invoke'
$inv.Invoke()
Write-Output ('Invoke 已发出 ' + (Get-Date -Format HH:mm:ss))

$gamePid = 0
for ($i = 0; $i -lt ($WaitGameSec / 3); $i++) {
  Start-Sleep -Seconds 3
  $now = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
  if ($now -gt $before) { Write-Output ("  日志有新内容（+$($now - $before) 字节）t+" + (($i + 1) * 3) + 's') }
  $g = Get-Process -Name SG8 -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($g) { $gamePid = $g.Id; Write-Output ("  SG8.exe pid=$gamePid 已出现 t+" + (($i + 1) * 3) + 's'); break }
  if ($p.HasExited) { Write-Output '  启动器自己退出了'; break }
}
Write-Output '--- 会话进程 ---'
Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'SG8|steam' } | ForEach-Object {
  Write-Output ('  ' + $_.ProcessId + ' ppid=' + $_.ParentProcessId + ' ' + $_.Name)
}
Write-Output '--- 日志尾部 ---'
if (Test-Path $log) {
  Get-Content $log -Tail 8 | ForEach-Object { Write-Output ('  ' + $_) }
}
if (-not $LeaveRunning) {
  if ($gamePid -gt 0) { Write-Output '（游戏留着运行，方便你看画面；启动器窗口在屏幕外）' }
}
