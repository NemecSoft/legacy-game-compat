<#
命令与征服：泰伯利亚黎明 game_starter.exe 命令行开关回归（全程不启动游戏、不动系统）：
  --checkfiles  Engine 核心 13 文件存在性，退出码 0，日志自证
  --readings    只读数 + 区间断言进日志，退出码 0
  --selftest    cmd.exe /c exit 7 → 启动器退出码必须是 7（退出码回传链路）
  --wer         本机真实 WER 报告 + 中/英文手写样例各解析一次，结论另存 UTF-8 再断言
  日志自证      「启动参数：」行原样含 -- 开关；不许出现「参数疑似被 shell 路径转换」

用法：pwsh -NoProfile -File Games\CC1TiberianDawn\tools\switch-test.ps1
结论另存 evidence\switch-test-result.txt（UTF-8），脚本退出码 = 失败项数。
#>
param(
  [string]$GameDir = 'D:\YunGame\F\CC1-TiberianDawn'
)
$ErrorActionPreference = 'Stop'
$script:fails = 0
$script:lines = New-Object System.Collections.Generic.List[string]
function Assert([string]$name, [bool]$cond, [string]$detail) {
  $tag = if ($cond) { 'PASS' } else { $script:fails++; 'FAIL' }
  $line = "$tag  $name  $detail"
  Write-Host $line
  $script:lines.Add($line)
}
$exe = Join-Path $GameDir 'game_starter.exe'
$logPath = Join-Path $GameDir '运行日志.txt'
$evidence = Join-Path $PSScriptRoot '..\evidence'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$work = Join-Path $PSScriptRoot 'switch-test-tmp'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Run-Switch([string[]]$argList, [string]$stdoutFile = '') {
  $psi = @{ FilePath = $exe; ArgumentList = $argList; WorkingDirectory = $GameDir; PassThru = $true; Wait = $true }
  if ($stdoutFile) { $psi.RedirectStandardOutput = $stdoutFile }
  $p = Start-Process @psi
  return $p.ExitCode
}
function Log-Text { if (Test-Path $logPath) { [IO.File]::ReadAllText($logPath, [Text.Encoding]::UTF8) } else { '' } }

try {
  $logBefore = Log-Text
  $lenBefore = $logBefore.Length

  # 1. --checkfiles
  $out = Join-Path $work 'checkfiles.txt'
  $rc = Run-Switch @('--checkfiles') $out
  $stdout = if (Test-Path $out) { [IO.File]::ReadAllText($out) } else { '' }
  $newLog = (Log-Text).Substring([Math]::Min($lenBefore, (Log-Text).Length))
  Assert 'checkfiles 退出码与自证' ($rc -eq 0 -and $stdout.Contains('FILES OK') -and
    $newLog.Contains('文件检查：Engine 核心 13 个文件齐全')) `
    ("rc=$rc stdout=$($stdout.Trim()) 日志齐全=$($newLog.Contains('13 个文件齐全'))")

  # 2. --readings
  $lenBefore = (Log-Text).Length
  $rc = Run-Switch @('--readings')
  $newLog = (Log-Text).Substring([Math]::Min($lenBefore, (Log-Text).Length))
  Assert 'readings 退出码与断言进日志' ($rc -eq 0 -and $newLog.Contains('读数校验：全部合理') -and $newLog.Contains('刷新率')) `
    ("rc=$rc 全部合理=$($newLog.Contains('读数校验：全部合理'))")

  # 3. --selftest 退出码回传
  $rc = Run-Switch @('--selftest', 'cmd.exe', '/c', 'exit 7')
  Assert 'selftest 退出码回传' ($rc -eq 7) "rc=$rc（期望 7）"

  # 4. --wer 本机真实报告（规则 35：只验手写样例等于没验）
  $roots = @(
    (Join-Path $env:ProgramData 'Microsoft\Windows\WER\ReportQueue'),
    (Join-Path $env:ProgramData 'Microsoft\Windows\WER\ReportArchive'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\WER\ReportQueue')
  )
  $realWer = $null
  foreach ($r in $roots) {
    if (-not (Test-Path $r)) { continue }
    $cand = Get-ChildItem $r -Directory -Filter 'AppCrash_*' -ErrorAction SilentlyContinue |
      Sort-Object LastWriteTime -Descending |
      ForEach-Object { Get-ChildItem $_.FullName -Filter '*.wer' -ErrorAction SilentlyContinue } |
      Select-Object -First 3
    foreach ($w in $cand) {
      $dst = Join-Path $work 'real.wer'
      Copy-Item $w.FullName $dst -Force
      $concl = Join-Path $work 'real-conclusion.txt'
      if (Test-Path $concl) { Remove-Item $concl -Force }
      $rc = Run-Switch @('--wer', $dst, $concl)
      if ($rc -eq 0) { $realWer = $w.FullName; break }
    }
    if ($realWer) { break }
  }
  $realText = ''
  $conclPath = Join-Path $work 'real-conclusion.txt'
  if (Test-Path $conclPath) { $realText = [IO.File]::ReadAllText($conclPath, [Text.Encoding]::UTF8) }
  Assert 'wer 真实报告解析' ($null -ne $realWer -and $realText.Contains('异常码 0x') -and $realText.Contains('故障模块')) `
    ("报告=$realWer 结论=$($realText.Trim())")

  # 5/6. --wer 中/英文手写样例（Sig 名字随系统语言变，两种都要认；防「故障模块版本」被当模块名）
  $samples = @{
    'zh' = "EventType=APPCRASH`r`nSig[0].Name=异常代码`r`nSig[0].Value=c0000005`r`nSig[1].Name=异常偏移`r`nSig[1].Value=00012345`r`nSig[2].Name=故障模块名称`r`nSig[2].Value=fake.dll`r`nSig[3].Name=故障模块版本`r`nSig[3].Value=1.2.3.4`r`n"
    'en' = "EventType=APPCRASH`r`nSig[0].Name=Exception Code`r`nSig[0].Value=c0000005`r`nSig[1].Name=Exception Offset`r`nSig[1].Value=00012345`r`nSig[2].Name=Fault Module Name`r`nSig[2].Value=fake.dll`r`nSig[3].Name=Fault Module Version`r`nSig[3].Value=1.2.3.4`r`n"
  }
  foreach ($k in 'zh', 'en') {
    $src = Join-Path $work "sample-$k.wer"
    [IO.File]::WriteAllText($src, $samples[$k], [Text.Encoding]::Unicode)
    $concl = Join-Path $work "sample-$k-conclusion.txt"
    $rc = Run-Switch @('--wer', $src, $concl)
    $text = if (Test-Path $concl) { [IO.File]::ReadAllText($concl, [Text.Encoding]::UTF8) } else { '' }
    $want = '崩溃捕获：异常码 0xc0000005，出错地址 +0x00012345，故障模块 fake.dll'
    Assert "wer $k 样例" ($rc -eq 0 -and $text.Contains($want)) ("rc=$rc 结论=$($text.Trim())")
  }

  # 7. 日志自证：原始参数原样、无 shell 路径转换警告
  $all = Log-Text
  $noConv = -not $all.Contains('参数疑似被 shell 路径转换')
  $proof = $all.Contains('启动参数：--checkfiles') -and $all.Contains('启动参数：--readings') -and
    $all.Contains('启动参数：--selftest cmd.exe /c exit 7') -and $all.Contains('启动参数：--wer')
  Assert '日志参数自证' ($noConv -and $proof) ("无路径转换警告=$noConv 原始参数行齐全=$proof")
}
finally {
  if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
  $summary = "switch-test 结果：失败 $script:fails 项  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
  $script:lines.Add($summary)
  [IO.File]::WriteAllLines((Join-Path $evidence 'switch-test-result.txt'), $script:lines, (New-Object Text.UTF8Encoding($false)))
  Write-Host $summary
}
exit $script:fails