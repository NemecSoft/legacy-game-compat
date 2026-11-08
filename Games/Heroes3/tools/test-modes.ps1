param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [string]$Modes = '1,2,3,4,5,6,7',
  [int]$WaitSec = 26,
  [switch]$AllowGameLaunch
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# 这个脚本会**真的把游戏拉起 7 次**。用户明确说过别再反复运行游戏（AGENTS 规则 24 第 14 类），
# 所以默认直接退出；确实要跑六档验证，必须显式加 -AllowGameLaunch。
if (-not $AllowGameLaunch) {
  Write-Output ('这个脚本会逐个档位真启动游戏（本次会拉起 ' + $Modes.Split(',').Count + ' 次）。' +
    '规则 24 第 14 条：收尾只跑不启动游戏的测试（readings-test / wer-test / test-delivery / 规范自检）。' +
    '确认要跑就加 -AllowGameLaunch。')
  exit 90
}

Add-Type -Namespace H3Shot -Name PW -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr h, System.IntPtr dc, uint flags);
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
'@

# 取证只用 PrintWindow 抓游戏自己的窗口（规则：禁止 CopyFromScreen 截全屏，会把用户桌面带进证据）
function Shot([string]$path) {
  $g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $g) { return $null }
  $r = New-Object H3Shot.PW+RECT
  [H3Shot.PW]::GetWindowRect($g.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  if ($w -le 0 -or $h -le 0 -or $w -gt 16384 -or $h -gt 16384) { return $g.MainWindowTitle }
  $b = New-Object System.Drawing.Bitmap $w, $h
  $gr = [System.Drawing.Graphics]::FromImage($b)
  $hdc = $gr.GetHdc()
  [H3Shot.PW]::PrintWindow($g.MainWindowHandle, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT
  $gr.ReleaseHdc($hdc)
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
