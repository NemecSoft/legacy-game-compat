param(
  [string]$Exe = 'D:\YunGame\F\KingdomHeroes8\SG8Launcher.exe',
  [int]$WaitSec = 8,
  [switch]$KeepRunning
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinEnum {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLengthW(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static System.Collections.Generic.List<string> Out = new System.Collections.Generic.List<string>();
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (Want == 0 || pid == Want) {
      if (IsWindowVisible(h) && GetWindowTextLengthW(h) > 0) {
        StringBuilder sb = new StringBuilder(512);
        GetWindowTextW(h, sb, sb.Capacity);
        Out.Add(pid + " | hwnd=" + h + " | " + sb.ToString());
      }
    }
    return true;
  }
}
'@

function Snap-Temp {
  Get-ChildItem $env:TEMP -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName + '|' + $_.LastWriteTime.Ticks }
}

$before = Snap-Temp
$p = Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
Write-Output ("LAUNCHER pid=$($p.Id) $Exe")
Start-Sleep -Seconds $WaitSec

Write-Output '--- 进程树（含命令行）---'
$all = Get-CimInstance Win32_Process
$map = @{}
foreach ($x in $all) { $map[[int]$x.ProcessId] = $x }
function Show-Tree([int]$pid2, [int]$depth) {
  $x = $map[$pid2]
  if ($null -ne $x) {
    ('  ' * $depth) + "$($x.Name) pid=$($x.ProcessId) ppid=$($x.ParentProcessId)"
    if ($x.CommandLine) { ('  ' * ($depth + 1)) + 'CMD: ' + $x.CommandLine }
    ('  ' * ($depth + 1)) + 'PATH: ' + $x.ExecutablePath
  }
  foreach ($c in $all) { if ([int]$c.ParentProcessId -eq $pid2) { Show-Tree ([int]$c.ProcessId) ($depth + 1) } }
}
Show-Tree $p.Id 0

Write-Output '--- 窗口标题 ---'
[WinEnum]::Want = [uint32]$p.Id
[WinEnum]::EnumWindows([WinEnum+EnumProc] { param($h, $l) [WinEnum]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
[WinEnum]::Out | ForEach-Object { Write-Output $_ }
[WinEnum]::Want = 0
[WinEnum]::Out.Clear()
[WinEnum]::EnumWindows([WinEnum+EnumProc] { param($h, $l) [WinEnum]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
Write-Output '  (全部可见窗口，前 25 个)'
[WinEnum]::Out | Select-Object -First 25 | ForEach-Object { Write-Output "  $_" }

Write-Output '--- 新增/改动的临时目录 ---'
$after = Snap-Temp
$new = Compare-Object $before $after | Where-Object { $_.SideIndicator -eq '=>' }
if ($null -eq $new) { Write-Output '  (无)' } else { $new | ForEach-Object { Write-Output ('  ' + (($_.InputObject -split '\|')[0])) } }

if (-not $KeepRunning) {
  Write-Output '--- 结束启动器 ---'
  Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Launcher*' -or $_.Name -like '*SG8*' } |
    ForEach-Object { Write-Output ('  kill ' + $_.Name + ' pid=' + $_.ProcessId); Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
