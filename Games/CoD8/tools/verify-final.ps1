# 精简版最终验证：--go 启动，确认不碰 bink/键位、进菜单正常
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
$ev = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32b {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@
if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 15
$game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($game) {
  if ($game.MainWindowHandle -ne 0 -and [Win32b]::IsIconic($game.MainWindowHandle)) {
    [Win32b]::ShowWindow($game.MainWindowHandle, 9) | Out-Null
    [Win32b]::SetForegroundWindow($game.MainWindowHandle) | Out-Null
    Start-Sleep -Seconds 2
  }
  & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out "$ev\final-native-t15s.png" -WaitSeconds 3 2>$null
  Write-Output 't=15s 截图完成'
} else {
  Write-Output 't=15s 游戏未运行'
}
Write-Output 'done'
