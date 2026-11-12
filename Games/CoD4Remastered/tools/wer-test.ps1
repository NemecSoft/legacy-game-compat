# 崩溃报告解析回归：全程不启动游戏、不改系统、不解析 stdout 中文。
# 结论由 game_starter.exe --wer 另存成 UTF-8 文件，本脚本只读文件断言（规则 35 / 38）。
param([string]$Exe = 'D:\YunGame\CoD\CoD4.Remastered\game_starter.exe')

$ErrorActionPreference = 'Stop'
$tmp = Join-Path $env:TEMP 'cod4-wer-test'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$fail = 0

function Run-Wer([string]$werPath, [string]$outPath) {
  $p = Start-Process -FilePath $Exe -ArgumentList @('--wer', $werPath, $outPath) -Wait -PassThru
  return $p.ExitCode
}

# 1) 手写样例：UTF-16LE，中文系统字段名；顺带放一个「故障模块版本」验证不会被当成模块名
$sample = @'
Version=1
EventType=APPCRASH
Sig[0].Name=Application Name
Sig[0].Value=h1_sp64_ship.exe
Sig[1].Name=异常代码
Sig[1].Value=c0000005
Sig[2].Name=异常偏移
Sig[2].Value=0000000000041234
Sig[3].Name=故障模块名称
Sig[3].Value=h1_sp64_ship.exe
Sig[4].Name=故障模块版本
Sig[4].Value=1.0.0.0
'@
$wer = Join-Path $tmp 'sample.wer'
$out = Join-Path $tmp 'sample.txt'
[IO.File]::WriteAllText($wer, $sample, (New-Object Text.UnicodeEncoding($false, $true)))
$rc = Run-Wer $wer $out
$text = [IO.File]::ReadAllText($out, [Text.Encoding]::UTF8)
if ($rc -ne 0 -or $text -notmatch '异常码 0xC0000005' -or $text -notmatch '故障模块 h1_sp64_ship' -or $text -match '故障模块版本') {
  Write-Host "FAIL 中文样例解析不对（rc=$rc）：$text"
  $fail++
} else {
  Write-Host "PASS 中文样例：$text"
}

# 2) 英文系统字段名
$sampleEn = $sample -replace '异常代码', 'Exception Code' -replace '异常偏移', 'Exception Offset' -replace '故障模块名称', 'Fault Module Name' -replace '故障模块版本', 'Fault Module Version'
$wer2 = Join-Path $tmp 'sample-en.wer'
$out2 = Join-Path $tmp 'sample-en.txt'
[IO.File]::WriteAllText($wer2, $sampleEn, (New-Object Text.UnicodeEncoding($false, $true)))
$rc2 = Run-Wer $wer2 $out2
$text2 = [IO.File]::ReadAllText($out2, [Text.Encoding]::UTF8)
if ($rc2 -ne 0 -or $text2 -notmatch '异常码 0xC0000005') {
  Write-Host "FAIL 英文样例解析不对（rc=$rc2）：$text2"
  $fail++
} else {
  Write-Host "PASS 英文样例：$text2"
}

# 3) 本机现成的真报告（不只验手写样例）
$root = Join-Path $env:ProgramData 'Microsoft\Windows\WER'
$real = Get-ChildItem $root -Recurse -File -Filter *.wer -ErrorAction SilentlyContinue | Select-Object -First 1
if ($real) {
  $out3 = Join-Path $tmp 'real.txt'
  $rc3 = Run-Wer $real.FullName $out3
  $text3 = [IO.File]::ReadAllText($out3, [Text.Encoding]::UTF8)
  if ($rc3 -notin @(0, 2) -or $text3.Length -eq 0) {
    Write-Host "FAIL 真报告解析异常（rc=$rc3）：$($real.FullName)"
    $fail++
  } else {
    Write-Host "PASS 真报告（$($real.Name)）：$text3"
  }
} else {
  Write-Host "SKIP 本机没有现成的 WER 报告，真报告验证跳过"
}

Write-Host ("结果：" + $(if ($fail -eq 0) { '全部通过' } else { "$fail 项失败" }))
exit $fail
