param([string]$Save = '存档-Milwaukee Jon(荘臣)-5级.rrs', [string]$Out = 'save-name-check.png', [int]$Wait = 14)
$d = 'D:\YunGame\Race\RoadRash'
Add-Type -AssemblyName System.Drawing
$p = Start-Process -FilePath (Join-Path $d 'Roadrash.exe') -ArgumentList $Save -WorkingDirectory $d -PassThru
Start-Sleep -Seconds $Wait
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Write-Output "shot=$Out  $((Get-Item $Out).Length) bytes"
