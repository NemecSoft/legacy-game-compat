$ErrorActionPreference = 'SilentlyContinue'
function W($s){ Write-Output $s }
Get-Process game_starter | Where-Object { $_.Path -like '*CS1.6*' } | Stop-Process -Force
Start-Sleep -Seconds 1

$exe = 'D:\YunGame\CS\CS1.6\game_starter.exe'
$dir = 'D:\YunGame\CS\CS1.6'
$p = Start-Process -FilePath $exe -ArgumentList '/go' -WorkingDirectory $dir -PassThru
W ('launcher pid=' + $p.Id)
Start-Sleep -Seconds 35

W '=== PROCESSES ==='
Get-Process cstrike,hl,CSOverlay,game_starter -ErrorAction SilentlyContinue |
  Select-Object Id, ProcessName, MainWindowTitle | Format-Table -AutoSize | Out-String | ForEach-Object { W $_ }

W '=== SCREEN CAPTURE ==='
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $b.Size)
$out = 'D:\AI\legacy-game-compat\Games\CS16\evidence\cs16-native.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
W ('saved ' + $out + ' ' + $b.Width + 'x' + $b.Height)

W '=== LAUNCHER LOG TAIL ==='
if (Test-Path (Join-Path $dir '运行日志.txt')) {
  Get-Content (Join-Path $dir '运行日志.txt') -Tail 25 | ForEach-Object { W $_ }
}
