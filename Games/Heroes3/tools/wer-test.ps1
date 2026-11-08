param([string]$GameDir = 'D:\YunGame\E\Heroes3')
# 崩溃报告取证回归：客户机崩了直接关机，我们能拿到的只有那份 .wer。
# 这脚本用现成的真报告和两种语言（客户机是中文系统，Sig 名字是「异常代码」不是 "Exception Code"）
# 喂给 game_starter.exe /wer，断言每次都能读出「异常码 + 出错地址 + 故障模块」。
$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDir 'game_starter.exe'
if (-not (Test-Path $exe)) { throw "找不到 $exe（先跑 build.ps1）" }
$tmp = Join-Path $env:TEMP ('wer-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$fail = 0
function Check($ok, $name, $detail) {
  if ($ok) { Write-Output ('PASS ' + $name) }
  else { $script:fail++; Write-Output ('FAIL ' + $name + '  ' + $detail) }
}
# 断言不许依赖控制台代码页：让 exe 把结论另存成 UTF-8 文件，脚本读那份字节（和运行日志同一编码口径）
function RunWer([string]$path) {
  $out = Join-Path $tmp ('out-' + [IO.Path]::GetFileNameWithoutExtension($path) + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.txt')
  & $exe /wer $path $out | Out-Null
  $code = $LASTEXITCODE
  $text = if (Test-Path $out) { [IO.File]::ReadAllText($out, [Text.Encoding]::UTF8) } else { '(exe 没写出结论文件)' }
  return @{ text = $text; code = $code }
}
function WriteWer([string]$name, [string[]]$lines) {
  $p = Join-Path $tmp $name
  [IO.File]::WriteAllLines($p, $lines, [Text.Encoding]::Unicode)
  return $p
}

# 1) 中文系统（客户机实况）：按名字认，不按序号猜
$zh = WriteWer 'zh.wer' @(
  'Version=1', 'EventType=APPCRASH', 'AppName=Heroes3.exe',
  'Sig[0].Name=应用程序名', 'Sig[0].Value=Heroes3.exe',
  'Sig[3].Name=故障模块名称', 'Sig[3].Value=WINGRAPH.DLL',
  'Sig[4].Name=故障模块版本', 'Sig[4].Value=3.1.0.0',
  'Sig[6].Name=异常代码', 'Sig[6].Value=c0000094',
  'Sig[7].Name=异常偏移', 'Sig[7].Value=0007e6a6')
$r = RunWer $zh
Check ($r.code -eq 0 -and $r.text -match 'c0000094' -and $r.text -match 'WINGRAPH\.DLL' -and $r.text -match '0007e6a6') `
  '中文 Sig 名字读得出异常码/模块/偏移' ("实际：" + $r.text)

# 2) 英文系统同名报告也要读得出（换语言不许哑）
$en = WriteWer 'en.wer' @(
  'EventType=APPCRASH', 'AppName=Heroes3.exe',
  'Sig[0].Name=Application Name', 'Sig[0].Value=Heroes3.exe',
  'Sig[3].Name=Fault Module Name', 'Sig[3].Value=Heroes3.exe',
  'Sig[4].Name=Fault Module Version', 'Sig[4].Value=3.1.0.0',
  'Sig[6].Name=Exception Code', 'Sig[6].Value=c0000005',
  'Sig[7].Name=Exception Offset', 'Sig[7].Value=000123ab')
$r = RunWer $en
Check ($r.code -eq 0 -and $r.text -match 'c0000005' -and $r.text -match '000123ab') `
  '英文 Sig 名字读得出' ("实际：" + $r.text)

# 3) 「故障模块版本」不许当成模块名（曾经只按序号取，换版式就读错）
$ver = WriteWer 'ver.wer' @(
  'EventType=APPCRASH',
  'Sig[3].Name=故障模块名称', 'Sig[3].Value=WINGRAPH.DLL',
  'Sig[4].Name=故障模块版本', 'Sig[4].Value=3.1.0.0')
$r = RunWer $ver
Check ($r.text -match 'WINGRAPH\.DLL' -and $r.text -notmatch '3\.1\.0\.0') `
  '模块版本号不会被当成模块名' ("实际：" + $r.text)

# 4) 读不懂的文件必须明说「没认出」并给非零退出码，不许假装成功
$junk = WriteWer 'junk.wer' @('Version=1', 'EventType=APPCRASH', 'Foo=Bar')
$r = RunWer $junk
Check ($r.code -ne 0 -and $r.text -match '没认出|没读到') `
  '读不出时会承认读不出（非零退出码）' ("实际：exit=" + $r.code + ' ' + $r.text)

# 5) 本机真有 AppCrash 报告就拿真文件再验一次（不依赖我手写的样例）
$real = Get-ChildItem 'C:\ProgramData\Microsoft\Windows\WER\ReportArchive','C:\ProgramData\Microsoft\Windows\WER\ReportQueue' `
  -Directory -Filter 'AppCrash_*' -ErrorAction SilentlyContinue |
  ForEach-Object { Join-Path $_.FullName 'Report.wer' } | Where-Object { Test-Path $_ } | Select-Object -First 3
if (-not $real) { Write-Output 'SKIP 本机没有现成 AppCrash 报告可验' }
foreach ($f in $real) {
  $r = RunWer $f
  Check ($r.code -eq 0 -and $r.text -match '异常码 0x[0-9a-fA-F]{8}' -and $r.text -match '故障模块') ('真报告读得出异常码：' + (Split-Path $f -Leaf) + ' <- ' + (Split-Path (Split-Path $f) -Leaf).Substring(0, 24)) ("实际：" + $r.text)
}

Remove-Item $tmp -Recurse -Force
Write-Output ('FAILURES=' + $fail)
exit $fail
