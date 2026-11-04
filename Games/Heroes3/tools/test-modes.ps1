param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [string]$Modes = '1,2,3,4,5,6,7',
  [int]$WaitSec = 26
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Shot([string]$path) {
  $g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $g) { return $null }
  $b = New-Object System.Drawing.Bitmap 1920, 1080
  $gr = [System.Drawing.Graphics]::FromImage($b)
  $gr.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size 1920, 1080))
  $b.Save($path)
  $gr.Dispose(); $b.Dispose()
  return $g.MainWindowTitle
}

$fail = 0
$list = $Modes.Split(',') | ForEach-Object { [int]$_ }
foreach ($m in $list) {
  $log = Join-Path $GameDir '运行日志.txt'
  if (Test-Path $log) { Remove-Item $log -Force }
  $p = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList "/go", "/mode=$m" `
    -WorkingDirectory $GameDir -PassThru
  Start-Sleep -Seconds $WaitSec
  $g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  $title = $null
  $png = Join-Path $PSScriptRoot ("..\evidence\mode-{0}.png" -f $m)
  if ($g) { $title = Shot $png }
  $txt = ''
  if (Test-Path $log) { $txt = (Get-Content $log -Encoding UTF8 -Raw) }
  $mounted = if ($txt -match '已挂载 (\S+) \+ (\S+)') { $Matches[1] + '/' + $Matches[2] } else { '没记录' }
  $ok = ($null -ne $g)
  if (-not $ok) { $fail++ }
  Write-Output ('{0} mode {1}：层/配置={2}｜日志 {3} 字节｜窗口={4}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $m, $mounted, $txt.Length, $title)
  Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 3
  if (-not $p.HasExited) { try { $p.WaitForExit(15000) | Out-Null } catch { } }
  # 收尾必须回到默认档（第 6 档 dgVoodoo2）：层是 dgvoodoo 的、dgVoodoo.conf 在、ddraw.ini 不留
  $voo = (Get-FileHash (Join-Path $GameDir 'ddraw.dll')).Hash -eq (Get-FileHash (Join-Path $GameDir '_dgvoodoo2\ddraw.dll')).Hash
  $conf = Test-Path (Join-Path $GameDir 'dgVoodoo.conf')
  $left = Test-Path (Join-Path $GameDir 'ddraw.ini')
  if (-not ($voo -and $conf -and -not $left)) { $fail++; Write-Output ("     FAIL 收尾不对：层回到dgvoodoo=$voo dgVoodoo.conf=$conf ddraw.ini残留=$(( -not $left ))") }
  else { Write-Output '     收尾 OK（已回到默认档 dgVoodoo2）' }
}
Write-Output ('FAILURES=' + $fail)
exit $fail
