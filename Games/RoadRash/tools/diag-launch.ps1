param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [string]$Exe = 'Roadrash.exe',
  [int]$WaitBeforeOk = 3,
  [int]$WaitAfterOk = 8,
  [switch]$LeaveRunning
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class DiagWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static string Text(IntPtr h){ var sb = new StringBuilder(4096); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
  public static string Class(IntPtr h){ var sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }
  public static void Dump(uint pid, string tag) {
    Console.WriteLine("--- windows [" + tag + "] pid=" + pid);
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid) {
        Console.WriteLine("TOP class=" + Class(h) + " visible=" + IsWindowVisible(h) + " text=[" + Text(h) + "]");
        EnumChildWindows(h, (c, l2) => {
          Console.WriteLine("  CHILD class=" + Class(c) + " text=[" + Text(c) + "]");
          return true;
        }, IntPtr.Zero);
      }
      return true;
    }, IntPtr.Zero);
  }
}
"@

function Save-Screen([string]$path) {
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

Add-Type -AssemblyName System.Windows.Forms

Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

$p = Start-Process -FilePath (Join-Path $GameDir $Exe) -WorkingDirectory $GameDir -PassThru
Write-Output "pid=$($p.Id)"
Start-Sleep -Seconds $WaitBeforeOk
if (-not (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)) {
  Write-Output "EXITED before dialog, code=$($p.ExitCode)"
  exit 1
}

[DiagWin]::Dump([uint32]$p.Id, 'before-ok')
Save-Screen (Join-Path $PSScriptRoot 'shot-before-ok.png')

$okClicked = $false
[DiagWin]::EnumWindows({ param($h, $l)
  $wp = 0; [void][DiagWin]::GetWindowThreadProcessId($h, [ref]$wp)
  if ($wp -eq $p.Id -and [DiagWin]::Class($h) -eq '#32770') {
    $btn = [DiagWin]::FindWindowEx($h, [IntPtr]::Zero, 'Button', $null)
    if ($btn -ne [IntPtr]::Zero) {
      [void][DiagWin]::PostMessage($btn, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)  # BM_CLICK
      $script:okClicked = $true
      Write-Output "clicked OK on dialog"
    }
  }
  return $true
}, [IntPtr]::Zero) | Out-Null

Start-Sleep -Seconds $WaitAfterOk
$alive = [bool](Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
Write-Output "after-ok alive=$alive okClicked=$okClicked"
if ($alive) { [DiagWin]::Dump([uint32]$p.Id, 'after-ok') }
Save-Screen (Join-Path $PSScriptRoot 'shot-after-ok.png')

if (-not $LeaveRunning) {
  if ($alive) { Stop-Process -Id $p.Id -Force }
  Start-Sleep -Milliseconds 500
  Write-Output "killed"
}
