# 界面自检：起启动器 -> 实机量窗口中心偏移（规则 28）-> PrintWindow 抓窗口自己（不截全屏）-> 关进程。
param(
  [string]$Exe = 'D:\YunGame\CoD\CoD4.Remastered\game_starter.exe',
  [string]$Shot = 'D:\AI\legacy-game-compat\Games\CoD4Remastered\evidence\cod4-launcher.png'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class WinCheck {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
'@

$shotDir = Split-Path $Shot -Parent
New-Item -ItemType Directory -Force -Path $shotDir | Out-Null
$p = Start-Process -FilePath $Exe -PassThru
try {
  $p.WaitForInputIdle(5000) | Out-Null
  Start-Sleep -Milliseconds 1500
  $r = New-Object WinCheck+RECT
  [WinCheck]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $wa = [System.Windows.Forms.Screen]::FromHandle($p.MainWindowHandle).WorkingArea
  $cx = ($r.Left + $r.Right) / 2
  $cy = ($r.Top + $r.Bottom) / 2
  $dx = [math]::Round($cx - ($wa.Left + $wa.Width / 2))
  $dy = [math]::Round($cy - ($wa.Top + $wa.Height / 2))
  Write-Host ("窗口：$($r.Right - $r.Left) x $($r.Bottom - $r.Top)，中心 ($cx,$cy)")
  Write-Host ("工作区中心：($($wa.Left + $wa.Width / 2),$($wa.Top + $wa.Height / 2))，偏移 ($dx,$dy)")
  Write-Host ("居中判定：" + $(if ([math]::Abs($dx) -le 2 -and [math]::Abs($dy) -le 2) { '通过' } else { '不通过' }))
  $repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
  & (Join-Path $repo 'Games\GodofWarIII\tools\window-shot.ps1') -Shot $Shot
} finally {
  # 只关自己拉起来的这个进程，别顺手杀掉别的游戏开着的启动器
  if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
