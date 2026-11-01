param(
  [Parameter(Mandatory=$true)][string]$GameDir,
  [Parameter(Mandatory=$true)][string]$ProcessName,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [string]$Launcher = 'game_starter.exe',
  [int]$Seconds = 70
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class HudWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
"@

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

try {
  $env:DXVK_HUD = 'version,api,fps'

  $launcherPath = Join-Path $GameDir $Launcher
  $lp = Start-Process -FilePath $launcherPath -ArgumentList '/go' -WorkingDirectory $GameDir -PassThru
  Write-Output ("launcher pid={0}" -f $lp.Id)

  $deadline = (Get-Date).AddSeconds(40)
  $game = $null
  while ((Get-Date) -lt $deadline) {
    $procs = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    $game = $procs | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($game) { break }
    Start-Sleep -Milliseconds 500
  }
  if (-not $game) { throw "game process $ProcessName with a window not found" }
  Write-Output ("game pid={0} hwnd={1} title={2}" -f $game.Id, $game.MainWindowHandle, $game.MainWindowTitle)

  $start = Get-Date
  $marks = @(12, 30, 50, 75, 100, 130, $Seconds) | Sort-Object -Unique | Where-Object { $_ -le $Seconds }
  foreach ($t in $marks) {
    $wait = $t - [int]((Get-Date) - $start).TotalSeconds
    if ($wait -gt 0) { Start-Sleep -Seconds $wait }
    $game.Refresh()
    if ($game.HasExited) { Write-Output ("game exited before t={0}s" -f $t); break }
    $h = $game.MainWindowHandle
    if ($h -ne 0) {
      [HudWin]::ShowWindowAsync($h, 9) | Out-Null
      [HudWin]::SetForegroundWindow($h) | Out-Null
      [HudWin]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, 0, 0, 0x0043) | Out-Null
    }
    Start-Sleep -Milliseconds 800
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bounds.Size)
    $file = Join-Path $OutDir ("shot-{0:000}s.png" -f $t)
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("shot t={0}s {1}x{2} -> {3}" -f $t, $bounds.Width, $bounds.Height, $file)
  }

  $game = Get-Process -Id $game.Id -ErrorAction SilentlyContinue
  if ($game -and -not $game.HasExited) {
    $game.CloseMainWindow() | Out-Null
    if (-not $game.WaitForExit(10000)) { Stop-Process -Id $game.Id -Force }
  }
  if (-not $lp.WaitForExit(15000)) { Stop-Process -Id $lp.Id -Force; Write-Output 'launcher killed' }
  else { Write-Output ("launcher exit={0}" -f $lp.ExitCode) }
}
finally {
  Remove-Item Env:\DXVK_HUD -ErrorAction SilentlyContinue
  Write-Output 'DXVK_HUD cleared'
}
