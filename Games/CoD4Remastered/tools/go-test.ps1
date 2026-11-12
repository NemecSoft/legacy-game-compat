# --go 零交互回归：在临时目录里用一个假游戏跑完整链路，不拉真游戏、不改游戏目录。
# 保留 -AllowGameLaunch 门禁（规则 24 第 14 类）：本脚本确实会起一个进程，虽然只是假游戏。
param(
  [string]$Exe = 'D:\YunGame\CoD\CoD4.Remastered\game_starter.exe',
  [switch]$AllowGameLaunch
)

$ErrorActionPreference = 'Stop'
if (-not $AllowGameLaunch) { Write-Host '需要显式 -AllowGameLaunch 才会运行'; exit 1 }

$tmp = Join-Path $env:TEMP 'cod4-go-test'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$fake = Join-Path $tmp 'fake.cs'
[IO.File]::WriteAllText($fake,
  'class P { static int Main(string[] a) { System.Threading.Thread.Sleep(3000); return 7; } }',
  (New-Object Text.UTF8Encoding($false)))
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc -nologo -target:winexe -out:(Join-Path $tmp 'h1_sp64_ship.exe') $fake
if ($LASTEXITCODE -ne 0) { throw '假游戏编译失败' }
Copy-Item $Exe (Join-Path $tmp 'game_starter.exe') -Force

$fail = 0
$starter = Join-Path $tmp 'game_starter.exe'
$p = Start-Process -FilePath $starter -ArgumentList @('--go') -Wait -PassThru
Write-Host ("--go 启动器退出码 = " + $p.ExitCode + "（期望 7，即假游戏的退出码）")
if ($p.ExitCode -ne 7) { Write-Host 'FAIL 退出码没有原样回传'; $fail++ }

$logPath = Join-Path $tmp '运行日志.txt'
$logText = [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8)
Write-Host '===== 临时目录运行日志 ====='
foreach ($ln in [IO.File]::ReadAllLines($logPath, [Text.Encoding]::UTF8)) { Write-Host $ln }

foreach ($want in @('零交互：是', '点「启动游戏」', '已启动游戏', '游戏退出，退出码 0x00000007')) {
  if ($logText -notmatch [regex]::Escape($want)) { Write-Host "FAIL 日志缺自证行：$want"; $fail++ }
}
Write-Host ("结果：" + $(if ($fail -eq 0) { '全部通过' } else { "$fail 项失败" }))
exit $fail
