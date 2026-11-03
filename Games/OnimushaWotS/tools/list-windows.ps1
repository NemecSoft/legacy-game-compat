param([Parameter(Mandatory = $true)][string]$ProcessName)

$p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'NO_PROCESS'; exit 1 }
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class EnumWin {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
'@
$target = $p.Id
$cb = [EnumWin+Proc]{
  param($h, $l)
  $pid2 = [uint32]0
  [EnumWin]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
  if ($pid2 -eq $target) {
    $t = New-Object System.Text.StringBuilder 256
    $c = New-Object System.Text.StringBuilder 256
    [EnumWin]::GetWindowText($h, $t, 256) | Out-Null
    [EnumWin]::GetClassName($h, $c, 256) | Out-Null
    $r = New-Object EnumWin+RECT
    [EnumWin]::GetWindowRect($h, [ref]$r) | Out-Null
    Write-Output ('HWND=0x{0:X} VISIBLE={1} CLASS={2} TITLE={3} RECT={4},{5} {6}x{7}' -f $h.ToInt64(), [EnumWin]::IsWindowVisible($h), $c.ToString(), $t.ToString(), $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top))
  }
  return $true
}
[EnumWin]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
