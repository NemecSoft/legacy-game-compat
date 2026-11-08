param(
  [string]$GameDir = 'D:\YunGame\E\Heroes3',
  [string]$EvidenceDir = (Join-Path $PSScriptRoot '..\evidence'),
  [switch]$AllowGameLaunch
)

$ErrorActionPreference = 'Stop'
# 这个脚本要真开一次游戏才能送按键（AGENTS 规则 24 第 14 类：收尾不许顺手拉起游戏）
if (-not $AllowGameLaunch) {
  Write-Output '这个脚本会真启动一次游戏来送快捷键。确认要跑就加 -AllowGameLaunch（规则 24 第 14 条）。'
  exit 90
}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class HotkeyTest {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr e);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  public struct RECT { public int Left, Top, Right, Bottom; }
  public static void Tap(byte vk) {
    keybd_event(vk, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(120);
    keybd_event(vk, 0, 2, UIntPtr.Zero);
  }
  public static void AltEnter() {
    keybd_event(0x12, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(0x0D, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(120);
    keybd_event(0x0D, 0, 2, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(0x12, 0, 2, UIntPtr.Zero);
  }
}
"@

function Shot([string]$name) {
  # 只用 PrintWindow 抓游戏自己的窗口；CopyFromScreen 会把用户桌面上的别的东西一起带进证据（pitfalls 77）
  $g = Get-Process -Name Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $g) { return }
  $r = New-Object HotkeyTest+RECT
  [HotkeyTest]::GetWindowRect($g.MainWindowHandle, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  if ($w -le 0 -or $h -le 0 -or $w -gt 16384 -or $h -gt 16384) { return }
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $gr = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $gr.GetHdc()
  [HotkeyTest]::PrintWindow($g.MainWindowHandle, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT
  $gr.ReleaseHdc($hdc)
  $gr.Dispose()
  $path = Join-Path $EvidenceDir $name
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

function StateOf([IntPtr]$h) {
  $r = New-Object HotkeyTest+RECT
  [HotkeyTest]::GetWindowRect($h, [ref]$r) | Out-Null
  $style = [HotkeyTest]::GetWindowLong($h, -16)
  return ("{0}x{1} at {2},{3} style=0x{4:X8}" -f ($r.Right - $r.Left), ($r.Bottom - $r.Top), $r.Left, $r.Top, $style)
}

$starter = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go' -WorkingDirectory $GameDir -PassThru
$p = $null
for ($i = 0; $i -lt 60; $i++) {
  Start-Sleep -Milliseconds 500
  $p = Get-Process Heroes3 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if ($p) { break }
}
if (-not $p) { throw '游戏窗口没有出现' }
Start-Sleep -Seconds 3
$h = $p.MainWindowHandle
$ws = New-Object -ComObject WScript.Shell
$ws.AppActivate($p.Id) | Out-Null
Start-Sleep -Milliseconds 500
[HotkeyTest]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500
$fg = [HotkeyTest]::GetForegroundWindow()
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("handle=$h foreground=$fg")
$lines.Add("before  " + (StateOf $h))
Shot 'h3-hotkey-before.png'

[HotkeyTest]::Tap(0x73)
Start-Sleep -Seconds 2
$p.Refresh()
$lines.Add("after F4 " + (StateOf $p.MainWindowHandle))
$lines.Add("fg after F4=" + [HotkeyTest]::GetForegroundWindow())
Shot 'h3-hotkey-f4.png'

[HotkeyTest]::AltEnter()
Start-Sleep -Seconds 2
$p.Refresh()
$lines.Add("after Alt+Enter " + (StateOf $p.MainWindowHandle))
Shot 'h3-hotkey-altenter.png'

[IO.File]::WriteAllText((Join-Path $EvidenceDir 'hotkey-toggle.txt'), (($lines -join "`r`n") + "`r`n"), [Text.Encoding]::UTF8)
Stop-Process -Id $p.Id -Force
for ($i = 0; $i -lt 20; $i++) { Start-Sleep -Milliseconds 300; if ($starter.HasExited) { break } }
if (-not $starter.HasExited) { Stop-Process -Id $starter.Id -Force }
$lines -join "`r`n"
