param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [int]$Runs = 3,
  [int]$WaitSec = 14
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$outDir = Join-Path $PSScriptRoot 'verify'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Get-Res {
  $v = Get-CimInstance Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution } | Select-Object -First 1
  if ($v) { "$($v.CurrentHorizontalResolution)x$($v.CurrentVerticalResolution)x$($v.CurrentBitsPerPixel)" } else { 'unknown' }
}

function Save-Shot([string]$path) {
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

  $sum = 0.0; $nonBlack = 0; $n = 0
  $rect = New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  try {
    $bytes = New-Object byte[] ($data.Stride * $bmp.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    for ($i = 0; $i -lt $bytes.Length; $i += 4) {
      $lum = 0.114 * $bytes[$i] + 0.587 * $bytes[$i + 1] + 0.299 * $bytes[$i + 2]
      $sum += $lum
      if ($lum -gt 25) { $nonBlack++ }
      $n++
    }
  } finally { $bmp.UnlockBits($data) }
  $bmp.Dispose()
  return [pscustomobject]@{
    MeanLum = [math]::Round($sum / $n, 1)
    NonBlackPct = [math]::Round(100.0 * $nonBlack / $n, 1)
    Size = "$($b.Width)x$($b.Height)"
  }
}

Write-Output ("display before: " + (Get-Res))

for ($i = 1; $i -le $Runs; $i++) {
  Get-Process -Name Roadrash -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 800

  $p = Start-Process -FilePath (Join-Path $GameDir 'Roadrash.exe') -WorkingDirectory $GameDir -PassThru
  Start-Sleep -Seconds $WaitSec
  $alive = [bool](Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
  $res = Get-Res
  $shot = Join-Path $outDir ("run{0}.png" -f $i)
  $stats = $null
  if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
    $stats = Save-Shot $shot
  } elseif (Test-Path $shot) { Remove-Item $shot -Force }

  Write-Output ("run {0}: alive={1} res={2} shot={3} meanLum={4} nonBlack={5}%" -f $i, $alive, $res, (Split-Path $shot -Leaf), $stats.MeanLum, $stats.NonBlackPct)

  Get-Process -Name Roadrash -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 1.5
  Write-Output ("        after exit res=" + (Get-Res))
}
