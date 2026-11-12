# loc_language 验证 v2：窗口被最小化时自动还原再截图
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@

if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }
$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $proc.Id)

foreach ($t in @(10, 18, 26)) {
  Start-Sleep -Seconds $(if ($t -eq 10) { 10 } elseif ($t -eq 18) { 8 } else { 8 })
  $game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $game) { Write-Output ("t=$t`s 游戏已退出"); break }
  if ($game.MainWindowHandle -eq 0) { Write-Output ("t=$t`s 无主窗口 pid=" + $game.Id); continue }
  if ([Win32]::IsIconic($game.MainWindowHandle)) {
    [Win32]::ShowWindow($game.MainWindowHandle, 9) | Out-Null   # SW_RESTORE
    [Win32]::SetForegroundWindow($game.MainWindowHandle) | Out-Null
    Start-Sleep -Seconds 2
    Write-Output ("t=$t`s 窗口已从最小化还原")
  }
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\loclang2-t$($t)s.png" -WaitSeconds 2 2>$null
}
Write-Output 'done'
