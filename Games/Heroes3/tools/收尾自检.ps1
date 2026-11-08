param([string]$GameDir = 'D:\YunGame\E\Heroes3')
# 收尾自检：一条命令跑完**不启动游戏**的全部验证，并且逐条判退出码。
# 为什么要这个脚本：以前手工串 `pwsh -File a.ps1 2>&1 | tail -1 && pwsh -File b.ps1`，
# `tail` 的退出码恒为 0，前一步失败照样把后一步跑完 —— 看起来全绿，实际中途已经崩（吃过一次）。
$ErrorActionPreference = 'Stop'
$tools = $PSScriptRoot
$steps = @(
  @{ Name = '读数与断言（不启动游戏）'; Script = 'readings-test.ps1' },
  @{ Name = '崩溃报告解析（中文/英文 Sig 名 + 本机真报告）'; Script = 'wer-test.ps1' },
  # 最后一步跑装/撤包：它会调用一键应用/一键撤回，顺带验证「UTF-8 控制台下代码页已钉死」
  @{ Name = '交付包应用/撤回（含代码页回归）'; Script = 'test-delivery.ps1' },
  @{ Name = '交付包与游戏目录逐字节一致'; Script = '' }
)
$failed = @()

foreach ($s in $steps) {
  if ($s.Script -eq '') {
    # 交付包 payload 与游戏目录里的同名成品必须逐字节一致（规则 24 第 7 类）。
    # 例外：根目录 ddraw.ini —— 它是「挂上某个 cnc-ddraw 档才生成的运行时配置」，
    # 默认档（dgVoodoo2）的收尾会删掉它，所以装包后跑过一次就一定对不上；预设的正本在 _presets\。
    $payload = Join-Path (Split-Path $tools -Parent) 'release\payload'
    $skip = @('ddraw.ini')
    $bad = 0; $n = 0
    Get-ChildItem $payload -Recurse -File | ForEach-Object {
      $rel = $_.FullName.Substring((Resolve-Path $payload).Path.Length + 1)
      if ($rel -eq 'manifest.txt' -or $skip -contains $rel) { return }
      $t = Join-Path $GameDir $rel; $n++
      if (-not (Test-Path $t)) { Write-Output ('   MISSING ' + $rel); $bad++; return }
      if ((Get-FileHash $_.FullName).Hash -ne (Get-FileHash $t).Hash) { Write-Output ('   DIFF ' + $rel); $bad++ }
    }
    # 反过来查一道：默认档挂 dgVoodoo 时根目录不该留 ddraw.ini（留了就说明收尾没做到位）
    $stray = Join-Path $GameDir 'ddraw.ini'
    $voodooDefault = (Get-FileHash (Join-Path $GameDir 'ddraw.dll')).Hash -eq
                     (Get-FileHash (Join-Path $GameDir '_dgvoodoo2\ddraw.dll')).Hash
    if ($voodooDefault -and (Test-Path $stray)) {
      Write-Output '   残留：默认档是 dgVoodoo2，但根目录还有 ddraw.ini'
      $bad++
    }
    if ($bad -gt 0) { $failed += ('{0}（{1} 项不一致）' -f $s.Name, $bad) }
    else { Write-Output ('PASS ' + $s.Name + '（' + $n + ' 个文件一致，根目录 ddraw.ini 状态也对）') }
    continue
  }
  $p = Join-Path $tools $s.Script
  Write-Output ('--- ' + $s.Name)
  & ([ScriptBlock]::Create("Set-Location '$tools'; .\$($s.Script) -GameDir '$GameDir'"))
  $code = $LASTEXITCODE          # 直接在进程内取，不经过管道，吃不掉
  if ($code -ne 0) { $failed += ('{0} 退出码 {1}' -f $s.Name, $code) }
}

# 跑完必须没有残留的游戏进程（它们会锁住 ddraw.dll，让下一条假失败）
$left = @(Get-Process -Name 'Heroes3' -ErrorAction SilentlyContinue)
if ($left.Count -gt 0) { $failed += ('还有 {0} 个游戏进程没退出（{1}）' -f $left.Count, ($left.Id -join ',')) }

Write-Output '===================='
if ($failed.Count -eq 0) { Write-Output '收尾自检：全部 PASS（没有任何一步启动游戏）'; exit 0 }
foreach ($f in $failed) { Write-Output ('FAIL ' + $f) }
Write-Output ('收尾自检：' + $failed.Count + ' 步失败')
exit 1
