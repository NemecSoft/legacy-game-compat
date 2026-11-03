param(
  [string]$ProcessName = 'OnimushaWotS',
  [ValidateSet('click', 'dblclick', 'key', 'wheel', 'focus', 'comboNext', 'comboPrev')][string]$Action = 'click',
  [int]$X = 0,
  [int]$Y = 0,
  [string]$Key = '',
  [int]$Wheel = 0,
  [string]$Shot = '',
  [int]$ShotDelayMs = 1200
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class UiWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool fAttach);
  [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004, KEYUP = 0x0002;
}
'@

$p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output 'NO_WINDOW'; exit 1 }
$hwnd = $p.MainWindowHandle
$fg = [UiWin]::GetForegroundWindow()
$fgPid = [uint32]0
$fgThread = [UiWin]::GetWindowThreadProcessId($fg, [ref]$fgPid)
$myThread = [UiWin]::GetCurrentThreadId()
[UiWin]::AttachThreadInput($myThread, $fgThread, $true) | Out-Null
[UiWin]::ShowWindow($hwnd, 9) | Out-Null
[UiWin]::BringWindowToTop($hwnd) | Out-Null
[UiWin]::SetForegroundWindow($hwnd) | Out-Null
[UiWin]::SetActiveWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 200
[UiWin]::AttachThreadInput($myThread, $fgThread, $false) | Out-Null
$fg = [UiWin]::GetForegroundWindow()
$fgPid = [uint32]0
[UiWin]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
if ($fgPid -ne $p.Id) { Write-Output ('FOREGROUND_FAILED foreground=' + $fgPid + ' game=' + $p.Id); exit 2 }
Start-Sleep -Milliseconds 300

switch ($Action) {
  'click' {
    [UiWin]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [UiWin]::mouse_event([UiWin]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [UiWin]::mouse_event([UiWin]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  }
  'dblclick' {
    [UiWin]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    for ($i = 0; $i -lt 2; $i++) {
      [UiWin]::mouse_event([UiWin]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
      Start-Sleep -Milliseconds 40
      [UiWin]::mouse_event([UiWin]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
      Start-Sleep -Milliseconds 90
    }
  }
  'key' {
    $vk = [int][char]$Key.ToUpper()[0]
    [UiWin]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [UiWin]::keybd_event([byte]$vk, 0, [UiWin]::KEYUP, [UIntPtr]::Zero)
  }
  'wheel' {
    [UiWin]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    $d = if ($Wheel -lt 0) { [uint32]([int64]$Wheel + 4294967296) } else { [uint32]$Wheel }
    [UiWin]::mouse_event(0x0800, 0, 0, $d, [UIntPtr]::Zero)
  }
  'focus' { }
  'comboNext' {
    [UiWin]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [UiWin]::mouse_event([UiWin]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [UiWin]::mouse_event([UiWin]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    [UiWin]::keybd_event(0x28, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [UiWin]::keybd_event(0x28, 0, [UiWin]::KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [UiWin]::keybd_event(0x0D, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [UiWin]::keybd_event(0x0D, 0, [UiWin]::KEYUP, [UIntPtr]::Zero)
  }
  'comboPrev' {
    [UiWin]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [UiWin]::mouse_event([UiWin]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [UiWin]::mouse_event([UiWin]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    [UiWin]::keybd_event(0x26, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [UiWin]::keybd_event(0x26, 0, [UiWin]::KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [UiWin]::keybd_event(0x0D, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [UiWin]::keybd_event(0x0D, 0, [UiWin]::KEYUP, [UIntPtr]::Zero)
  }
}

Start-Sleep -Milliseconds $ShotDelayMs
$fg = [UiWin]::GetForegroundWindow()
$fgPid = [uint32]0
[UiWin]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
Write-Output ('FOREGROUND_PID=' + $fgPid + ' GAME_PID=' + $p.Id)

if ($Shot) {
  $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
  $bmp.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output ('SHOT=' + $Shot)
}
