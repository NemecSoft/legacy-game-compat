param([int]$Mode = 6, [int]$WaitSec = 26)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public class W {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
}
'@
function Desk { $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; return '' + $b.Width + 'x' + $b.Height }
$GameDir = 'D:\YunGame\E\Heroes3'
Write-Output ('DESK_BEFORE ' + (Desk))
$p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go', "/mode=$Mode" `
  -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds $WaitSec
$g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $g) {
  Write-Output 'NO GAME WINDOW'
} else {
  $h = $g.MainWindowHandle
  $r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
  $c = New-Object W+RECT; [W]::GetClientRect($h, [ref]$c) | Out-Null
  $style = [W]::GetWindowLong($h, -16); $ex = [W]::GetWindowLong($h, -20)
  $sb = New-Object System.Text.StringBuilder 256; [W]::GetWindowTextW($h, $sb, 256) | Out-Null
  $w = $r.R - $r.L; $ht = $r.B - $r.T
  Write-Output ('DESK_DURING ' + (Desk))
  Write-Output ('WIN_POS ' + $r.L + ',' + $r.T + ' WIN_SIZE ' + $w + 'x' + $ht + ' CLIENT ' + ($c.R - $c.L) + 'x' + ($c.B - $c.T))
  Write-Output ('STYLE 0x' + ('{0:X8}' -f ($style -band -1)) + ' EXSTYLE 0x' + ('{0:X8}' -f ($ex -band -1)))
  Write-Output ('HAS_CAPTION ' + (($style -band 0x00C00000) -ne 0) + ' HAS_THICKFRAME ' + (($style -band 0x00040000) -ne 0) + ' TOPMOST ' + (($ex -band 0x8) -ne 0))
  Write-Output ('FILLS_DESKTOP ' + ($w -ge [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width -and $ht -ge [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height))
  Write-Output ('TITLE ' + $sb.ToString())
}
Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 4
if (-not $p.HasExited) { try { $p.WaitForExit(15000) | Out-Null } catch { } }
Write-Output ('DESK_AFTER ' + (Desk))
