param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [string]$Exe = 'Roadrash.exe',
  [int]$Seconds = 32,
  [int]$Every = 4,
  [string]$OutPrefix = 'obs',
  [switch]$PressEscAt,
  [int]$EscAt = 12
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ObsWin {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public static void Esc() {
    keybd_event(0x1B, 0, 0, UIntPtr.Zero);
    keybd_event(0x1B, 0, 2, UIntPtr.Zero);
  }
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@

Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
$p = Start-Process -FilePath (Join-Path $GameDir $Exe) -WorkingDirectory $GameDir -PassThru
Write-Output "pid=$($p.Id)"

for ($t = 0; $t -lt $Seconds; $t += $Every) {
  Start-Sleep -Seconds $Every
  $alive = [bool](Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
  $file = Join-Path $PSScriptRoot ("{0}-{1:d2}s.png" -f $OutPrefix, ($t + $Every))
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
  $g.Dispose(); $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  Write-Output ("t={0}s alive={1} screen={2}x{3} saved={4}" -f ($t + $Every), $alive, $b.Width, $b.Height, (Split-Path $file -Leaf))
  if (-not $alive) { break }
  if ($PressEscAt -and ($t + $Every) -eq $EscAt) { [ObsWin]::Esc(); Write-Output 'sent ESC' }
}

if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
  Stop-Process -Id $p.Id -Force
  Start-Sleep -Milliseconds 800
  Write-Output 'killed'
} else {
  $p.Refresh(); Write-Output ('exit code=' + $p.ExitCode)
}
