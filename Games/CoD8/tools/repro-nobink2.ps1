# 试验 H2：binkw32.dll 移走对照（截图全容错版）
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$bk = 'D:\AI\legacy-game-compat\Games\CoD8\backup'

# 0. 先收尾上一轮残留：结束可能还在跑的游戏进程 + 确保 binkw32.dll 归位
Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Test-Path "$game\binkw32.dll.off") {
  if (Test-Path "$game\binkw32.dll") { Remove-Item "$game\binkw32.dll" -Force }
  Move-Item "$game\binkw32.dll.off" "$game\binkw32.dll" -Force
}
Write-Output ("binkw32.dll 状态: " + (Test-Path "$game\binkw32.dll"))

# 1. 移开 binkw32.dll
Copy-Item "$game\binkw32.dll" "$bk\binkw32.dll.before-test" -Force
Move-Item "$game\binkw32.dll" "$game\binkw32.dll.off" -Force
Write-Output 'binkw32.dll 已移开'

# 2. 跑
$p = Start-Process -FilePath (Join-Path $game 'iw5sp.exe') -WorkingDirectory $game -PassThru
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $cpu = [Math]::Round($gp.CPU, 1)
    Write-Output ("t+$($i*10)s 活着 主窗句柄=" + $gp.MainWindowHandle + " 响应=" + $gp.Responding + " CPU累计=" + $cpu + "s 线程=" + $gp.Threads.Count)
  } else { Write-Output "t+$($i*10)s 游戏已退出"; break }
}
if ($gp) {
  # 活着就截图（容错）
  $code = @'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class WS {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  public struct RECT { public int L, T, R, B; }
}
"@
'@
  Start-Sleep -Seconds 1
  Write-Output '游戏仍在运行（试验成功迹象），观察结束前不杀进程改为保留 5 秒后结束'
  Start-Sleep -Seconds 5
  Get-Process iw5sp -ErrorAction SilentlyContinue | Stop-Process -Force
}
Write-Output ("试验结束")

# 3. binkw32.dll 归位
if (Test-Path "$game\binkw32.dll.off") { Move-Item "$game\binkw32.dll.off" "$game\binkw32.dll" -Force }
Write-Output ("binkw32.dll 最终状态: " + (Test-Path "$game\binkw32.dll"))
