param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [int]$WaitSec = 18
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class V {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static uint Want;
  public static System.Collections.Generic.List<string> Out = new System.Collections.Generic.List<string>();
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (pid != Want || !IsWindowVisible(h)) return true;
    StringBuilder sb = new StringBuilder(256);
    GetWindowTextW(h, sb, sb.Capacity);
    R rc; GetWindowRect(h, out rc);
    Out.Add("[" + sb.ToString() + "] " + (rc.Rt - rc.L) + "x" + (rc.B - rc.T));
    return true;
  }
}
'@

function Try-Conf([string]$name, [string]$base, [string[]]$lines) {
  $conf = Join-Path $GameDir 'dgVoodoo.conf'
  $text = [IO.File]::ReadAllText($base, [Text.Encoding]::UTF8)
  foreach ($l in $lines) {
    # 形如 "DirectX|Resolution|640x480"：在指定小节里替换键值
    $p = $l.Split('|')
    $text = [regex]::Replace($text, ('(?m)^' + [regex]::Escape($p[1]) + '\s*=.*$'), ($p[1] + ' = ' + $p[2]))
  }
  [IO.File]::WriteAllText($conf, $text, [Text.Encoding]::UTF8)
  Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
  $proc = Start-Process -FilePath (Join-Path $GameDir 'Heroes3.exe') -WorkingDirectory $GameDir -PassThru
  Start-Sleep -Seconds $WaitSec
  [V]::Want = [uint32]$proc.Id
  [V]::Out.Clear()
  [V]::EnumWindows([V+EnumProc] { param($h, $l) [V]::Cb($h, $l) }, [IntPtr]::Zero) | Out-Null
  $titles = [V]::Out -join '  '
  $bad = $titles -match '意外终止|Error'
  $verdict = if ($bad) { 'FAIL(报错)' } elseif ($titles -match '英雄无敌') { 'PASS' } else { '没窗口' }
  Write-Output ("{0,-26} {1}  {2}" -f $name, $verdict, $titles)
  Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
}

# 先把 dgVoodoo2 顶上去
Copy-Item (Join-Path $GameDir '_dgvoodoo2\ddraw.dll') (Join-Path $GameDir 'ddraw.dll') -Force
Remove-Item (Join-Path $GameDir 'ddraw.ini') -Force -ErrorAction SilentlyContinue
$base = Join-Path $GameDir '_presets\dgVoodoo-full.conf'

Try-Conf '0 原始 centered+unforced' $base @()
Try-Conf 'A 全屏+强制640x480' $base @('DirectX|Resolution|640x480')
Try-Conf 'B 全屏+stretched' $base @('General|ScalingMode|stretched')
Try-Conf 'C 全屏+640x480+stretched' $base @('DirectX|Resolution|640x480', 'General|ScalingMode|stretched')
Try-Conf 'D 全屏+Bilinear2D' $base @('DirectX|Resolution|640x480', 'DirectX|Bilinear2DOperations|true')

# 恢复默认层
Copy-Item (Join-Path $GameDir '_cnc-ddraw\ddraw.dll') (Join-Path $GameDir 'ddraw.dll') -Force
Copy-Item (Join-Path $GameDir '_presets\ddraw.ini') (Join-Path $GameDir 'ddraw.ini') -Force
Remove-Item (Join-Path $GameDir 'dgVoodoo.conf') -Force -ErrorAction SilentlyContinue
Write-Output '已恢复 cnc-ddraw GDI 默认'
