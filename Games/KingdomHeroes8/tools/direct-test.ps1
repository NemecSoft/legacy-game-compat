param(
  [string]$GameDir = 'D:\YunGame\F\KingdomHeroes8\GAME',
  [int]$Rounds = 25,
  [string]$Shot = ''
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class E2 {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowTextLengthW(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static System.Collections.Generic.List<string> Out = new System.Collections.Generic.List<string>();
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    int len = GetWindowTextLengthW(h);
    StringBuilder sb = new StringBuilder(Math.Max(len + 1, 2));
    GetWindowTextW(h, sb, sb.Capacity);
    Out.Add(h + " | " + sb.ToString());
    return true;
  }
}
'@
$p = Start-Process -FilePath (Join-Path $GameDir 'SG8.exe') -WorkingDirectory $GameDir -PassThru
Write-Output "SG8.exe pid=$($p.Id)  (直接启动，不带任何环境变量/便携 Steam)"
for ($i = 0; $i -lt $Rounds; $i++) {
  Start-Sleep -Seconds 2
  if ($p.HasExited) {
    Write-Output ("EXITED code=0x{0:X8} after {1}s" -f [uint32]$p.ExitCode, ($i * 2))
    break
  }
  [E2]::Want = [uint32]$p.Id
  [E2]::Out.Clear()
  [E2]::EnumWindows([E2+EnumProc] { param($h, $l) [E2]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
  if ([E2]::Out.Count -gt 0) {
    Write-Output ("WINDOWS after {0}s:" -f ($i * 2))
    [E2]::Out | ForEach-Object { Write-Output ("  " + $_) }
    if ($Shot -ne '') {
      & (Join-Path $PSScriptRoot 'win-shot.ps1') -Pid2 $p.Id -Shot $Shot | Out-Null
      Write-Output ("SHOT " + $Shot)
    }
    break
  }
  if ($i % 5 -eq 4) { Write-Output ("  ... still no window at {0}s" -f (($i + 1) * 2)) }
}
Write-Output ("STATE: " + $(if ($p.HasExited) { 'exited 0x' + ('{0:X8}' -f [uint32]$p.ExitCode) } else { 'running' }))
