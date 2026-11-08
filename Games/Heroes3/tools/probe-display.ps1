param([int]$Mode = 6, [int]$WaitSec = 22, [string]$Shot = '', [switch]$AllowGameLaunch)
# 这个脚本用 /go /mode 零交互启动，会把游戏真拉起来一次（AGENTS 规则 24 第 14 类：收尾不许顺手起游戏）
if (-not $AllowGameLaunch) {
  Write-Output '这个脚本会真启动一次游戏（/go /mode=…）。确认要跑就加 -AllowGameLaunch（规则 24 第 14 条）。'
  exit 90
}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public class W {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public uint dmFields;
    public int dmPositionX, dmPositionY;
    public uint dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public ushort dmLogPixels;
    public uint dmBitsPerPel;
    public uint dmPelsWidth, dmPelsHeight;
    public uint dmDisplayFlags;
    public uint dmDisplayFrequency;
    public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
    public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
  }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  public static extern bool EnumDisplaySettingsW(string dev, int mode, ref DEVMODE dm);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public static int[] CurMode() {
    DEVMODE dm = new DEVMODE();
    dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
    bool ok = EnumDisplaySettingsW(null, -1, ref dm);   // ENUM_CURRENT_SETTINGS
    return new int[] { ok ? 1 : 0, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency };
  }
}
'@
$GameDir = 'D:\YunGame\E\Heroes3'
$before = [W]::CurMode()
Write-Output ('游戏前桌面 ' + $before[1] + 'x' + $before[2] + ' @' + $before[3] + 'Hz')
$p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go', "/mode=$Mode" `
  -WorkingDirectory $GameDir -PassThru
Start-Sleep -Seconds $WaitSec
$g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $g) { Write-Output 'NO GAME WINDOW' }
else {
  $h = $g.MainWindowHandle
  $r = New-Object W+RECT
  [W]::GetWindowRect($h, [ref]$r) | Out-Null
  $style = [W]::GetWindowLong($h, -16); $ex = [W]::GetWindowLong($h, -20)
  $sb = New-Object System.Text.StringBuilder 256
  [W]::GetWindowTextW($h, $sb, 256) | Out-Null
  $w = $r.R - $r.L; $ht = $r.B - $r.T
  $during = [W]::CurMode()
  $scr = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  Write-Output ('游戏中桌面 ' + $during[1] + 'x' + $during[2] + ' @' + $during[3] + 'Hz｜Screen ' + $scr.Width + 'x' + $scr.Height)
  Write-Output ('游戏窗口 ' + $r.L + ',' + $r.T + ' ' + $w + 'x' + $ht +
    '  占满桌面=' + ($w -ge $during[1] -and $ht -ge $during[2]))
  $hasCaption = ($style -band 0x00C00000) -ne 0    # WS_CAPTION | WS_BORDER
  $hasFrame = ($style -band 0x00040000) -ne 0      # WS_THICKFRAME
  Write-Output ('style=0x' + ('{0:X8}' -f ($style -band -1)) + ' exstyle=0x' + ('{0:X8}' -f ($ex -band -1)) +
    '  标题栏=' + $hasCaption + '  可拖边框=' + $hasFrame + '  置顶=' + (($ex -band 0x8) -ne 0))
  if ($Shot -ne '') {
    $b = New-Object System.Drawing.Bitmap $w, $ht
    $gr = [System.Drawing.Graphics]::FromImage($b)
    $hdc = $gr.GetHdc()
    [W]::PrintWindow($h, $hdc, 2) | Out-Null
    $gr.ReleaseHdc($hdc); $gr.Dispose()
    $b.Save($Shot); $b.Dispose()
    Write-Output ('SHOT ' + $Shot)
  }
}
Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 4
if (-not $p.HasExited) { try { $p.WaitForExit(15000) | Out-Null } catch { } }
$after = [W]::CurMode()
Write-Output ('退出后桌面 ' + $after[1] + 'x' + $after[2] + ' @' + $after[3] + 'Hz')
