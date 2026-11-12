# busy 期间窗口可见性回归（2026-11-12 用户定案：点「启动游戏」后窗口必须保留，
# 右栏操作说明要一直留着，除非玩家自己点退出）。
# 用假游戏跑 --go，量启动器主窗口在游戏运行期间是否可见、进程是否陪跑、退出码是否回传。
# 不拉真游戏、不改游戏目录。-AllowGameLaunch 门禁照旧（会起一个假游戏进程）。
param(
  [Parameter(Mandatory=$true)][string]$StarterExe,
  [string]$GameExeName = 'h1_sp64_ship.exe',
  [int]$FakeSleepMs = 8000,
  [int]$ExpectCode = 7,
  [switch]$AllowGameLaunch
)
$ErrorActionPreference = 'Stop'
if (-not $AllowGameLaunch) { Write-Host '需要显式 -AllowGameLaunch 才会运行'; exit 1 }

$tmp = Join-Path $env:TEMP ('busy-visible-' + [IO.Path]::GetFileNameWithoutExtension($StarterExe))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$fake = Join-Path $tmp 'fake.cs'
[IO.File]::WriteAllText($fake,
  'class P { static int Main(string[] a) { System.Threading.Thread.Sleep(' + $FakeSleepMs + '); return ' + $ExpectCode + '; } }',
  (New-Object Text.UTF8Encoding($false)))
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc -nologo -target:winexe -out:(Join-Path $tmp $GameExeName) $fake
if ($LASTEXITCODE -ne 0) { throw '假游戏编译失败' }
Copy-Item $StarterExe (Join-Path $tmp 'game_starter.exe') -Force

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win32Vis {
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
'@

$p = Start-Process -FilePath (Join-Path $tmp 'game_starter.exe') -ArgumentList @('--go') -PassThru
$fail = 0
try {
  Start-Sleep -Milliseconds 2500
  $h = $p.MainWindowHandle
  if ($h -eq [IntPtr]::Zero) { Write-Host 'FAIL 拿不到主窗口句柄'; $fail++ }
  else {
    $vis = [Win32Vis]::IsWindowVisible($h)
    Write-Host ("游戏运行中主窗口可见 = " + $vis + "（期望 True）")
    if (-not $vis) { Write-Host 'FAIL 游戏运行中窗口被藏起来了'; $fail++ }
    elseif ([Win32Vis]::IsIconic($h)) { Write-Host 'FAIL 窗口被最小化'; $fail++ }
  }
  $alive = -not $p.HasExited
  Write-Host ("游戏运行中启动器存活 = " + $alive + "（期望 True，陪跑）")
  if (-not $alive) { Write-Host 'FAIL 启动器提前退出'; $fail++ }
}
finally {
  if (-not $p.HasExited) { $p.WaitForExit(20000) | Out-Null }
}
$code = $p.ExitCode
Write-Host ("最终退出码 = " + $code + "（期望 " + $ExpectCode + "，游戏码原样回传）")
if ($code -ne $ExpectCode) { Write-Host 'FAIL 退出码没回传'; $fail++ }
Write-Host ("结果：" + $(if ($fail -eq 0) { '全部通过' } else { "$fail 项失败" }))
exit $fail
