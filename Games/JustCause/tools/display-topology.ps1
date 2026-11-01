Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class MON {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfoW(IntPtr h, ref MONITORINFO mi);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool EnumDisplayDevicesW(string dev, uint n, ref DISPLAY_DEVICE dd, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct DISPLAY_DEVICE {
    public int cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DeviceString;
    public int StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DeviceKey;
  }
}
"@
$i=0
$cb=[MON+EnumProc]{
  param($h,$l)
  $mi=New-Object MON+MONITORINFO
  $mi.cbSize=[Runtime.InteropServices.Marshal]::SizeOf([type][MON+MONITORINFO])
  [MON]::GetMonitorInfoW($h,[ref]$mi) | Out-Null
  $script:i++
  Write-Output ("MON$script:i {0},{1} - {2},{3} primary={4}" -f $mi.rcMonitor.L,$mi.rcMonitor.T,$mi.rcMonitor.R,$mi.rcMonitor.B,(($mi.dwFlags -band 1) -ne 0))
  return $true
}
[MON]::EnumDisplayMonitors([IntPtr]::Zero,[IntPtr]::Zero,$cb,[IntPtr]::Zero) | Out-Null
Write-Output '--- display adapters (EnumDisplayDevices) ---'
for($n=0;$n -lt 8;$n++){
  $dd=New-Object MON+DISPLAY_DEVICE
  $dd.cb=[Runtime.InteropServices.Marshal]::SizeOf([type][MON+DISPLAY_DEVICE])
  if(-not [MON]::EnumDisplayDevicesW($null,$n,[ref]$dd,0)){ break }
  $attached = (($dd.StateFlags -band 1) -ne 0)
  $primary = (($dd.StateFlags -band 4) -ne 0)
  Write-Output ("ADAPTER {0} name={1} desc={2} attached={3} primary={4} id={5}" -f $n,$dd.DeviceName,$dd.DeviceString,$attached,$primary,$dd.DeviceID)
  for($m=0;$m -lt 4;$m++){
    $md=New-Object MON+DISPLAY_DEVICE
    $md.cb=[Runtime.InteropServices.Marshal]::SizeOf([type][MON+DISPLAY_DEVICE])
    if(-not [MON]::EnumDisplayDevicesW($dd.DeviceName,[uint32]$m,[ref]$md,0)){ break }
    if($md.DeviceString){ Write-Output ("   MONITOR {0} {1} flags={2}" -f $m,$md.DeviceString,$md.StateFlags) }
  }
}
Write-Output '--- video controllers ---'
Get-CimInstance Win32_VideoController | ForEach-Object { Write-Output ("VC " + $_.Name + " | status=" + $_.Status + " | current=" + $_.CurrentHorizontalResolution + "x" + $_.CurrentVerticalResolution + " @ " + $_.CurrentRefreshRate + "Hz | driver=" + $_.DriverVersion + " | inf=" + $_.InfSection) }