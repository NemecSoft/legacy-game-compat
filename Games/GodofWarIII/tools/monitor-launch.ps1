param(
  [string]$Exe = 'D:\YunGame\F\GodofWarIIIRemastered\游戏设置.exe',
  [int]$Seconds = 14
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Mon {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static System.Collections.Generic.List<string> Hits = new System.Collections.Generic.List<string>();
  public static System.Collections.Generic.List<IntPtr> Handles = new System.Collections.Generic.List<IntPtr>();
  public static uint Target;
  public static bool Cb(IntPtr h, IntPtr l) {
    if (!IsWindowVisible(h)) return true;
    uint pid;
    GetWindowThreadProcessId(h, out pid);
    if (pid == Target) {
      var sb = new StringBuilder(512);
      GetWindowTextW(h, sb, 512);
      string t = sb.ToString();
      if (t.Length > 0) { Hits.Add(pid + " :: [" + t + "]"); Handles.Add(h); }
    }
    return true;
  }
}
'@
$dir = Split-Path $Exe
$before = Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-30) } | Select-Object -ExpandProperty FullName
$p = Start-Process -FilePath $Exe -WorkingDirectory $dir -PassThru
"started pid=$($p.Id) name=$([IO.Path]::GetFileName($Exe))"
$deadline = (Get-Date).AddSeconds($Seconds)
$seen = @{}
while ((Get-Date) -lt $deadline) {
  Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $p.Id -or $_.ProcessId -eq $p.Id } | ForEach-Object {
    $key = "$($_.ProcessId)"
    if (-not $seen.ContainsKey($key)) {
      $seen[$key] = $true
      "CHILD pid=$($_.ProcessId) ppid=$($_.ParentProcessId) name=$($_.Name) cmd=$($_.CommandLine)"
    }
  }
  foreach ($cp in (Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'SpartanPort.exe' })) {
    [Mon]::Target = [uint32]$cp.ProcessId
    $hitsBefore = [Mon]::Hits.Count
    [Mon]::EnumWindows([Mon+EnumProc] { param($h, $l) [Mon]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
    if ([Mon]::Hits.Count -gt $hitsBefore) {
      foreach ($hh in $([Mon]::Hits | Select-Object -Skip $hitsBefore)) { "WINDOW $hh" }
      $h0 = [Mon]::Handles[[Mon]::Handles.Count - 1]
      $r = New-Object Mon+RECT
      [Mon]::GetWindowRect($h0, [ref]$r) | Out-Null
      $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
      if ($w -gt 40 -and $ht -gt 40) {
        $bmp = New-Object System.Drawing.Bitmap $w, $ht
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        [Mon]::PrintWindow($h0, $hdc, 2) | Out-Null
        $g.ReleaseHdc($hdc); $g.Dispose()
        $out = 'D:\AI\legacy-game-compat\Games\GodofWarIII\evidence\original-settings-ui.png'
        $bmp.Save($out)
        $bmp.Dispose()
        "SAVED $out (${w}x${ht})"
      }
    }
  }
  Start-Sleep -Milliseconds 400
}
"exited=$($p.HasExited) exit=$(if ($p.HasExited) { $p.ExitCode } else { 'running' })"
Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'SpartanPort' } | ForEach-Object { "ALIVE pid=$($_.ProcessId) cmd=$($_.CommandLine)" }
$after = Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-30) } | Select-Object -ExpandProperty FullName
"NEW/TOUCHED FILES:"
Compare-Object -ReferenceObject @($before) -DifferenceObject @($after) | ForEach-Object { "  $($_.SideIndicator) $($_.InputObject)" }
