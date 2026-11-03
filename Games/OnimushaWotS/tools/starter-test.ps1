param(
  [string]$GameDir = 'D:\YunGame\F\OnimushaWotS',
  [int]$WaitSec = 40,
  [string]$Shot = '',
  [string]$Mode = '',
  [switch]$KillGame
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$argList = if ($Mode) { @($Mode, '/go') } else { @('/go') }
$starter = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList $argList -WorkingDirectory $GameDir -PassThru
Write-Output ('STARTER_PID=' + $starter.Id)
Start-Sleep -Seconds $WaitSec

$game = Get-Process -Name 'OnimushaWotS' -ErrorAction SilentlyContinue |
  Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($game) {
  Add-Type @'
using System;
using System.Runtime.InteropServices;
public class StarterWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
}
'@
  $r = New-Object StarterWin+RECT
  [StarterWin]::GetWindowRect($game.MainWindowHandle, [ref]$r) | Out-Null
  $c = New-Object StarterWin+RECT
  [StarterWin]::GetClientRect($game.MainWindowHandle, [ref]$c) | Out-Null
  $parent = (Get-CimInstance Win32_Process -Filter "ProcessId = $($game.Id)").ParentProcessId
  Write-Output ('GAME_PID=' + $game.Id + ' PARENT_PID=' + $parent)
  Write-Output ('WINDOW=' + ($r.Right - $r.Left) + 'x' + ($r.Bottom - $r.Top))
  Write-Output ('CLIENT=' + ($c.Right - $c.Left) + 'x' + ($c.Bottom - $c.Top))
  Write-Output ('STARTER_ALIVE=' + (-not $starter.HasExited))
} else {
  Write-Output 'NO_GAME_WINDOW'
}

if ($Shot) {
  $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
  $bmp.Save($Shot, [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Output ('SHOT=' + $Shot)
}

if ($KillGame -and $game) {
  Stop-Process -Id $game.Id -Force
  if (-not $starter.WaitForExit(15000)) {
    Write-Output 'STARTER_DID_NOT_EXIT'
  } else {
    Write-Output ('STARTER_EXIT=' + $starter.ExitCode)
  }
}
