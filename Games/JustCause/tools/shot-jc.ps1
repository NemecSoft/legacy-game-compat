param([int]$Wait=25,[string]$Out='jc-start.png')
$d='D:\YunGame\Z\JustCause1'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$p=Start-Process -FilePath (Join-Path $d 'JustCause.exe') -WorkingDirectory $d -PassThru
Start-Sleep -Seconds $Wait
$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height)
$g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($b.Location,[System.Drawing.Point]::Empty,$b.Size)
$bmp.Save((Join-Path (Split-Path $PSCommandPath -Parent) $Out),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output ("pid=" + $p.Id + " alive=" + $(if(Get-Process -Id $p.Id -EA SilentlyContinue){'yes'}else{'no'}))
