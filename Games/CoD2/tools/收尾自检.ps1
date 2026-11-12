param([string]$GameDir = 'D:\YunGame\CoD\CoD2')

# 使命召唤2 收尾自检：一条命令跑完**全程不启动游戏**的检查，逐步判退出码。
# 为什么每条都单独判退出码：以前手工串 `pwsh -File a.ps1 2>&1 | tail -1 && ...`，
# tail 的退出码恒为 0，前一步崩了后一步照跑，看着全绿其实中途就挂了（规则 24 第 13 类）。
# 为什么不解析 stdout 的中文：exe 按控制台代码页写、PowerShell 按另一种读，换台机器就假失败
# （规则 38）→ 要断中文结论就读「运行日志.txt」和 /wer 另存的那份 UTF-8 文件。
# 本脚本只用 /readings、/clearmark、/layer=、/wer、/selftest 这五个不启动游戏的开关；
# 绝不出现 /go 或 直接拉起 CoD2SP_s.exe（用户明令：别再反复运行游戏）。

$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDir 'game_starter.exe'
$log = Join-Path $GameDir '运行日志.txt'
$src = Join-Path $PSScriptRoot '..\src'
$build = Join-Path $PSScriptRoot '..\build'
$fail = 0
function Check($ok, [string]$name, [string]$detail) {
  if ($ok) { Write-Output ('PASS ' + $name) }
  else { $script:fail++; Write-Output ('FAIL ' + $name + '  ' + $detail) }
}
# winexe 用 & 调用不会等、也不填 $LASTEXITCODE → 一律 Start-Process -Wait 拿真退出码
function Run-Exe([string[]]$Arguments) {
  $p = Start-Process -FilePath $exe -ArgumentList $Arguments -WorkingDirectory $GameDir -PassThru -Wait
  return $p.ExitCode
}
function LogText() {
  if (-not (Test-Path $log)) { return '' }
  return [IO.File]::ReadAllText($log, [Text.Encoding]::UTF8)
}
# 只看刚写进去的那几行：整份日志里留着上一次运行的结论，全文件匹配会假通过 / 假失败
function LogTail([int]$n) {
  if (-not (Test-Path $log)) { return '' }
  $all = [IO.File]::ReadAllLines($log, [Text.Encoding]::UTF8)
  if ($all.Count -le $n) { return ($all -join "`r`n") }
  return (($all | Select-Object -Last $n) -join "`r`n")
}

Write-Output '===== 1) 前置文件'
if (-not (Test-Path $exe)) {
  Write-Output ('FAIL 找不到 ' + $exe + '（先跑 pwsh -NoProfile -File Games\CoD2\build.ps1）')
  exit 1
}
Check (Test-Path (Join-Path $GameDir 'launcher.ini')) '游戏目录有 launcher.ini' '配置没部署过去'
Check (Test-Path (Join-Path $GameDir 'CoD2SP_s.exe')) '游戏目录有单人主程序 CoD2SP_s.exe' '主程序不在，启动器无从启动'
# 玩家的画面 / 按键设置：本启动器设计上一个字都不改（规则：玩家在游戏里的设置必须留着）。
# 真正的断言是「跑完这一整套自检，这份文件字节没变」，见最后一步。
$cfg = Join-Path $GameDir 'main\players\3dm\config.cfg'
$cfgBefore = if (Test-Path $cfg) { (Get-FileHash $cfg).Hash } else { '' }
if ($cfgBefore -eq '') { Write-Output 'SKIP 还没有 main\players\3dm\config.cfg（游戏没运行过）' }

Write-Output '===== 2) 层自检 /layer=1（恢复到默认 DXVK 档，不启动游戏）'
$code = Run-Exe @('/layer=1')
Check ($code -eq 0) '/layer=1 退出码 0' ("实际 $code")
$txt = LogTail 8
Check ($txt -match '层自检（/layer=1）：1') '日志记了这次层自检用的是第 1 档' ("日志尾部：" + $txt)
Check ($txt -notmatch [regex]::Escape('请把运行日志发给我们')) '层文件自检没报「修不了」' ("日志尾部：" + $txt)

Write-Output '===== 3) 现场读数 /readings（只读数、不启动游戏）'
$code = Run-Exe @('/readings')
Check ($code -eq 0) '/readings 退出码 0' ("实际 $code")
$txt = LogTail 12
Check ($txt -match '现场快照') '日志里有现场快照' '没有现场快照行'
Check ($txt -match '读数校验：全部合理') '读数校验通过' '日志里不是「全部合理」，看上面那行的可疑项'
if ($txt -match '刷新率=(\-?\d+)') {
  $r = [int]$Matches[1]
  Check ($r -ge 1 -and $r -le 1000) ('刷新率合理（' + $r + '）') '刷新率不在 1..1000：VREFRESH 常量又抄错了（0 是 DRIVERVERSION）'
} else { Check $false '读到刷新率' '日志里没有刷新率' }
if ($txt -match '显示 (\d+)×(\d+)') {
  $w = [int]$Matches[1]; $h = [int]$Matches[2]
  Check ($w -gt 0 -and $h -gt 0) ('桌面宽高合理（' + $w + '×' + $h + '）') '读出 0：结构错位或取不到，兜底没生效'
} else { Check $false '读到桌面宽高' '日志里没有显示尺寸' }
if ($txt -match 'DPI=(\d+)/(\d+)') {
  $dx = [int]$Matches[1]; $dy = [int]$Matches[2]
  Check ($dx -ge 96 -and $dx -le 480 -and $dy -ge 96 -and $dy -le 480) ('DPI 合理（' + $dx + '/' + $dy + '）') 'DPI 读出 0 或离谱值'
} else { Check $false '读到 DPI' '现场快照里没有 DPI' }
if ($txt -match '屏幕数=(\d+)') {
  Check ([int]$Matches[1] -ge 1) ('屏幕数合理（' + $Matches[1] + '）') '屏幕数为 0'
} else { Check $false '读到屏幕数' '现场快照里没有屏幕数' }
if ($txt -match 'DEVMODEW (\d+) 字节') {
  Check ([int]$Matches[1] -eq 220) ('DEVMODEW 结构 220 字节' ) '结构大小不是 220：字段少写会整体错位 4 字节'
} else { Check $false '读到 DEVMODEW 结构大小' '日志里没有这一行' }
if ($txt -match '游戏盘\s+\S+\s*（(.+?)）') {
  $dt = $Matches[1]
  Check ($dt -in '本地硬盘', '网络盘', '光盘', '可移动盘', '内存盘', '未知', '路径无效') ('游戏盘类型认出来了（' + $dt + '）') `
    ("盘类型是「" + $dt + "」：GetDriveType 常量抄错")
} else { Check $false '读到游戏盘类型' '现场快照里没有盘类型' }
if ($txt -match '每扇区字节=(\d+) 每簇扇区=(\d+)') {
  $bps = [int]$Matches[1]; $spc = [int]$Matches[2]
  Check (($bps -in 512, 1024, 2048, 4096) -and ($spc -ge 1 -and $spc -le 128)) ("磁盘几何合理（$bps/$spc）") '每扇区字节或每簇扇区不合理'
} else { Check $false '读到磁盘几何' '日志里没有扇区参数' }
if ($txt -match '簇 总(\d+)/可用(\d+)') {
  $total = [long]$Matches[1]; $free = [long]$Matches[2]
  Check ($free -le $total) ('可用簇 ≤ 总簇（' + $free + '/' + $total + '）') '可用簇大于总簇：读数不自洽'
} else { Check $false '读到簇数' '日志里没有簇总数' }
Check ($txt -match 'DXVK 日志级别=error') 'DXVK 日志级别钉成 error（玩家目录不留流水日志）' '日志里没有这一行'
# 这一行只有真正画过一次界面才会有（launcher-shot.ps1 截图时跑过），所以查整份日志
$all = LogText
if ($all -match '右栏按键表自检：行数=(\d+) 内容高=(\d+) 视口高=(\d+) 需要滚动=(\S+)') {
  Check ([int]$Matches[1] -ge 19) ('按键表 ' + $Matches[1] + ' 行全在（default.cfg 的 19 条）') '行数少了，键位表被裁 = 给玩家错键位'
  if ($Matches[4] -eq 'False') { Write-Output ('PASS 界面一屏放得下整张键位表（内容 ' + $Matches[2] + ' / 视口 ' + $Matches[3] + '）') }
  else { Write-Output ('注意 键位表需要滚动才能看全（内容 ' + $Matches[2] + ' / 视口 ' + $Matches[3] + '）：允许，但截图要能看出有滚动条') }
} else { Write-Output 'SKIP 右栏自检行要开一次界面才有（先用 scripts\launcher-shot.ps1 抓一次图）' }

Write-Output '===== 3b) 安全模式标记自动清理 /clearmark（不启动游戏）'
# 上次游戏没正常退出会留 4 字节标记 __CoD2SP_s，下次启动弹繁体 Big5 的「是否以安全模式執行？」，
# 简化字系统上是乱码，选「是」还把画面降回 640×480。这段只在拉起游戏时才跑到，所以给它一个
# 不启动游戏的开关来验：造一个标记 → /clearmark → 标记必须没了 + 日志必须记一行。
$mark = Join-Path $GameDir '__CoD2SP_s'
$hadReal = Test-Path $mark
[IO.File]::WriteAllBytes($mark, [byte[]](0x78, 0x00, 0x00, 0x00))
$code = Run-Exe @('/clearmark')
Check ($code -eq 0) '/clearmark 退出码 0' ("实际 $code")
Check (-not (Test-Path $mark)) '标记文件被静默删掉（玩家不会看到那个乱码询问框）' '文件还在 = 下次启动照样弹框'
$txt = LogTail 4
Check ($txt -match '安全模式标记：已静默删掉') '日志记了这次清理' ("日志尾部：" + $txt)
if ($hadReal) { Write-Output '注意 游戏目录本来就有一个残留标记（上次游戏没正常退出），已按默认逻辑清掉' }

Write-Output '===== 3d) 画面默认值规范 /display（全屏 + 本机桌面，玩家改过就不再动；不启动游戏）'
# 交付档案在开发机测试后是「窗口 1280x720」，直接放到客户机会开窗。启动器每次启动前按账本比对一次：
# 没记录 → 规范成全屏+本机桌面；config 还等于我们上次写的 → 跟机刷新；不一样 → 玩家自己改过 → 永远不动。
# 这段用临时 cfg/state 验三条路都真实存在，全程不碰玩家档案（第 8 节的字节不变断言仍然成立）。
$tf = Join-Path $env:TEMP ('cod2-display-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tf | Out-Null
$cfgT = Join-Path $tf 'config.cfg'
$stT = Join-Path $tf 'display.state'
function Write-Cfg([string]$fs, [string]$mode) {
  # 注释里种 Latin1 可表示的高字节字符（À=C0 é=E9 ï=EF ß=DF），和繁体注释的 Big5 高字节同区间。
  # 不能直接种中文：Latin1 GetBytes 对 >U+00FF 一律变 '?'，种下去就丢了——那是测试自己的坑，不是启动器的。
  $lines = @('seta cl_language "zhant" # Latin1 高字节 Àéï ßß 要保得住', "seta r_fullscreen `"$fs`"",
             "seta r_mode `"$mode`"", 'seta cg_drawFPS "Off"')
  [IO.File]::WriteAllBytes($cfgT, [Text.Encoding]::GetEncoding(28591).GetBytes(($lines -join "`n") + "`n"))
}
function CfgVal([string]$name) {
  $m = [regex]::Match([Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($cfgT)),
        ("(?m)^seta\s+$name\s+\`"([^`"]+)\`""))
  return $m.Groups[1].Value
}
Write-Cfg '0' '1280x720'
$code = Run-Exe @('/display', ('"' + $cfgT + '"'), ('"' + $stT + '"'))
$txt = LogTail 6
Check ($code -eq 0) '/display 首次规范退出码 0' ("实际 $code")
Check ($txt -match '首次规范') '日志记了首次规范' ("日志尾部：" + $txt)
Check ((CfgVal 'r_fullscreen') -eq '1') ('初始档被规范成全屏（现在 ' + (CfgVal 'r_fullscreen') + '）') '还是窗口档 = 客户机开机还是开窗'
Check ((CfgVal 'r_mode') -match '^\d+x\d+$') ('r_mode 是本机桌面分辨率（' + (CfgVal 'r_mode') + '）') 'r_mode 没被写成一个分辨率字符串'
Check (([Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($cfgT))) -match 'Àéï ßß') '其余行逐字节没被吃掉' 'Latin1 往返破了，高字节注释没了'
Check ((CfgVal 'cg_drawFPS') -eq 'Off') '不相干的键没被顺手改' '写入逻辑改了别的行'
if (Test-Path $stT) { Write-Output ('PASS 账本 display.state 生成了（' + (Get-Content $stT -Raw).Trim() + '）') }
else { $fail++; Write-Output 'FAIL 账本没生成：下次无法分辨是玩家改的还是我们写的' }
Write-Cfg '0' '1024x768'      # 模拟「玩家在游戏里改成窗口 1024x768」——值不再等于账本
$code = Run-Exe @('/display', ('"' + $cfgT + '"'), ('"' + $stT + '"'))
$txt = LogTail 4
Check ($code -eq 0 -and $txt -match '玩家自己改成') '账本外改动被认成玩家设置并保留' ("日志尾部：" + $txt)
Check ((CfgVal 'r_mode') -eq '1024x768' -and (CfgVal 'r_fullscreen') -eq '0') '玩家改的窗口档一字没被盖回去' '启动器又覆盖了玩家设置（规则：玩家在游戏里的设置必须留着）'
Write-Cfg '1' (CfgVal 'r_mode') | Out-Null   # 玩家自己选回全屏；值若等于账本则走「已是全屏/跟机刷新」，不许再报玩家改动
Remove-Item $tf -Recurse -Force -ErrorAction SilentlyContinue

Write-Output '===== 3c) 主程序安全模式补丁在位 + 回滚往返（不启动游戏）'
# 补丁 = CoD2SP_s.exe 文件偏移 0x50700 处 6 字节 B8 01 00 00 00 C3（MOV EAX,1; RET），
# 跳过「是否以安全模式执行」的 MessageBoxA；原版整文件哈希钉死在下面，_orig\ 必须是它。
$gexe = Join-Path $GameDir 'CoD2SP_s.exe'
$origExe = Join-Path $GameDir '_orig\CoD2SP_s.exe'
$origSha = '2be375ec149c54470a6d2ce3b04b5edf4dd3e7a679ea5db58420a0841c1fa386'
$patchSha = '8d997ffa927c8224eb6057c40433ba77d2720358fec63dfe97c283b12ede9827'
function ExeBytes([string]$path, [int]$o, [int]$n) {
  $fs = [IO.File]::OpenRead($path); $fs.Position = $o
  $b = New-Object byte[] $n; [void]$fs.Read($b, 0, $n); $fs.Close(); return $b
}
$pb = ExeBytes $gexe 0x50700 6
Check (($pb | ForEach-Object { $_.ToString('X2') }) -join ' ' -eq 'B8 01 00 00 00 C3') `
  '主程序补丁在位（安全模式询问框任何启动路径都不弹）' '偏移 0x50700 不是补丁字节：exe 被换过或回滚过，跑 tools\patch-safemode.ps1'
Check ((Test-Path $origExe) -and ((Get-FileHash $origExe).Hash.ToLowerInvariant() -eq $origSha)) `
  '_orig\ 留的是取证时的原版主程序' '备份缺失或被改，回滚路断了'
$code = Run-Exe @('/restore-exe')
Check ($code -eq 0 -and (Get-FileHash $gexe).Hash.ToLowerInvariant() -eq $origSha) '/restore-exe 真能回滚到原版' ("exit=$code hash=" + (Get-FileHash $gexe).Hash.ToLowerInvariant())
$patchScript = Join-Path $PSScriptRoot 'patch-safemode.ps1'
& pwsh -NoProfile -File $patchScript -GameDir $GameDir | Out-Null
Check ($LASTEXITCODE -eq 0 -and (Get-FileHash $gexe).Hash.ToLowerInvariant() -eq $patchSha) '回滚后能重新打上补丁（往返一致）' ("exit=$LASTEXITCODE hash=" + (Get-FileHash $gexe).Hash.ToLowerInvariant())

Write-Output '===== 4) 崩溃报告解析 /wer（中文/英文 Sig 名 + 版本陷阱 + 真报告）'
# 结论一律另存 UTF-8 文件再读：断言不许依赖控制台代码页
$tmp = Join-Path $env:TEMP ('cod2-wer-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
function Write-Wer([string]$name, [string[]]$lines) {
  $p = Join-Path $tmp $name
  [IO.File]::WriteAllLines($p, $lines, [Text.Encoding]::Unicode)
  return $p
}
function Run-Wer([string]$werPath) {
  $out = Join-Path $tmp ('out-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.txt')
  $code = Run-Exe @('/wer', ('"' + $werPath + '"'), ('"' + $out + '"'))
  $text = if (Test-Path $out) { [IO.File]::ReadAllText($out, [Text.Encoding]::UTF8) } else { '(exe 没写出结论文件)' }
  return @{ code = $code; text = $text }
}
$zh = Write-Wer 'zh.wer' @(
  'Version=1', 'EventType=APPCRASH', 'AppName=CoD2SP_s.exe',
  'Sig[0].Name=应用程序名', 'Sig[0].Value=CoD2SP_s.exe',
  'Sig[3].Name=故障模块名称', 'Sig[3].Value=d3d9.dll',
  'Sig[4].Name=故障模块版本', 'Sig[4].Value=10.0.19041.1',
  'Sig[6].Name=异常代码', 'Sig[6].Value=c0000005',
  'Sig[7].Name=异常偏移', 'Sig[7].Value=0008f2a1')
$r = Run-Wer $zh
Check ($r.code -eq 0 -and $r.text -match 'c0000005' -and $r.text -match 'd3d9\.dll' -and $r.text -match '0008f2a1') `
  '中文 Sig 名字读得出异常码/模块/偏移' ("实际：" + $r.text.Trim())
$en = Write-Wer 'en.wer' @(
  'EventType=APPCRASH', 'AppName=CoD2SP_s.exe',
  'Sig[0].Name=Application Name', 'Sig[0].Value=CoD2SP_s.exe',
  'Sig[3].Name=Fault Module Name', 'Sig[3].Value=nvoglgpuext.dll',
  'Sig[4].Name=Fault Module Version', 'Sig[4].Value=31.0.15.2647',
  'Sig[6].Name=Exception Code', 'Sig[6].Value=c0000094',
  'Sig[7].Name=Exception Offset', 'Sig[7].Value=000123ab')
$r = Run-Wer $en
Check ($r.code -eq 0 -and $r.text -match 'c0000094' -and $r.text -match 'nvoglgpuext\.dll' -and $r.text -match '000123ab') `
  '英文 Sig 名字读得出（换语言不许哑）' ("实际：" + $r.text.Trim())
$ver = Write-Wer 'ver.wer' @(
  'EventType=APPCRASH',
  'Sig[3].Name=故障模块名称', 'Sig[3].Value=gfx_d3d_x86_s.dll',
  'Sig[4].Name=故障模块版本', 'Sig[4].Value=9.9.9.9')
$r = Run-Wer $ver
Check ($r.text -match 'gfx_d3d_x86_s\.dll' -and $r.text -notmatch '9\.9\.9\.9') `
  '「故障模块版本」不会被当成模块名' ("实际：" + $r.text.Trim())
$junk = Write-Wer 'junk.wer' @('Version=1', 'EventType=APPCRASH', 'Foo=Bar')
$r = Run-Wer $junk
Check ($r.code -ne 0 -and $r.text -match '没认出|没读到') `
  '读不出时会承认读不出（非零退出码）' ("实际：exit=" + $r.code + ' ' + $r.text.Trim())
$real = Get-ChildItem 'C:\ProgramData\Microsoft\Windows\WER\ReportArchive', 'C:\ProgramData\Microsoft\Windows\WER\ReportQueue' `
  -Directory -Filter 'AppCrash_*' -ErrorAction SilentlyContinue |
  ForEach-Object { Join-Path $_.FullName 'Report.wer' } | Where-Object { Test-Path $_ } | Select-Object -First 3
if (-not $real) { Write-Output 'SKIP 本机没有现成 AppCrash 报告可验（只验了手写样例）' }
$i = 0
foreach ($f in $real) {
  $i++
  $r = Run-Wer $f
  Check ($r.code -eq 0 -and $r.text -match '异常码 0x[0-9a-fA-F]{8}' -and $r.text -match '故障模块') `
    ("本机真报告读得出（第 $i 份）") ("实际：" + $r.text.Trim())
}

Write-Output '===== 5) 退出码回传 /selftest（托管契约第 3 条，用一个退 7 的小程序代替游戏）'
$code = Run-Exe @('/selftest', 'cmd.exe', '/c', 'exit 7')
Check ($code -eq 7) '/selftest 把子程序退出码 7 原样回传' ("game_starter.exe 实际退出码 $code")
$code = Run-Exe @('/selftest', 'cmd.exe', '/c', 'exit 0')
Check ($code -eq 0) '/selftest 回传 0 也对' ("实际 $code")

Write-Output '===== 6) 体积与 sha256（构建产物 == 游戏目录那份）'
$gameExeItem = Get-Item $exe
Check ($gameExeItem.Length -le 1MB) ('game_starter.exe 体积 ' + [Math]::Round($gameExeItem.Length / 1KB, 1) + ' KB ≤ 1 MB') '超过体积红线'
$built = Join-Path $build 'game_starter.exe'
if (Test-Path $built) {
  Check ((Get-FileHash $built).Hash -eq (Get-FileHash $exe).Hash) '游戏目录的 exe 与 build\ 那份逐字节一致' '部署完又被改动，或忘了重跑 build.ps1'
} else { Write-Output 'SKIP 没有 build\game_starter.exe（只查游戏目录那份）' }
$shaFile = Join-Path $GameDir 'game_starter.sha256'
if (Test-Path $shaFile) {
  $want = ((Get-Content -LiteralPath $shaFile -Raw) -split '\s+')[0].ToLowerInvariant()
  Check ($want -eq (Get-FileHash $exe).Hash.ToLowerInvariant()) 'game_starter.sha256 与 exe 实际哈希一致' '校验文件与 exe 对不上，平台复核会失败'
} else { Check $false '游戏目录有 game_starter.sha256' '缺校验文件' }

Write-Output '===== 7) 兼容层文件逐字节一致（src / 根目录 / _dxvk 三处）'
function Same-Bytes([string]$a, [string]$b) {
  if (-not (Test-Path $a) -or -not (Test-Path $b)) { return $false }
  return (Get-FileHash $a).Hash -eq (Get-FileHash $b).Hash
}
$confSrc = Join-Path $src 'dxvk.conf'
$confRoot = Join-Path $GameDir 'dxvk.conf'
$confBak = Join-Path $GameDir '_dxvk\dxvk.conf'
Check (Same-Bytes $confSrc $confRoot) 'dxvk.conf：src 与游戏目录根一致' '两处不同 → 重新跑 build.ps1'
Check (Same-Bytes $confRoot $confBak) 'dxvk.conf：根目录与 _dxvk 留底一致' '两处不同 → 交付包与留底会互相打架'
Check ((Test-Path $confRoot) -and (([IO.File]::ReadAllText($confRoot, [Text.Encoding]::UTF8)) -match 'dxvk\.hud\s*=\s*fps')) 'dxvk.conf 交付只开 FPS（规则 11）' '没有 dxvk.hud = fps'
foreach ($dll in 'd3d9.dll', 'dxgi.dll') {
  $root = Join-Path $GameDir $dll
  $keep = Join-Path $GameDir ('_dxvk\' + $dll)
  Check ((Test-Path $root) -and (Test-Path $keep) -and (Same-Bytes $root $keep)) ($dll + '：根目录与 _dxvk 留底逐字节一致') '缺文件或字节不同 → 跑 game_starter.exe /layer=1 自愈'
  $repo = Join-Path $PSScriptRoot ('..\..\..\tools\dxvk\x86-2.6.2\' + $dll)
  if (Test-Path $repo) { Check (Same-Bytes $keep $repo) ($dll + '：留底与仓库 tools\dxvk\x86-2.6.2 同一份（DXVK 2.6.2，坑 118 升级）') '留底不是仓库这份，版本对不上' }
}
$aside = Join-Path $GameDir '_dxvk\禁用'
$dup = @(Get-ChildItem $aside -File -ErrorAction SilentlyContinue)
if ($dup.Count -gt 0) {
  $bad = 0
  foreach ($d in $dup) { if (Same-Bytes $d.FullName (Join-Path $GameDir ('_dxvk\' + $d.Name))) { $bad++ } }
  Check ($bad -eq 0) ('_dxvk\禁用\ 没有重复留底（现有 ' + $dup.Count + ' 个文件）') '禁用目录里留着和留底同一份的重复文件，自愈逻辑没清干净'
} else { Write-Output 'PASS _dxvk\禁用\ 是空的（没用过原生档或被清理过）' }
$stale = @(Get-ChildItem $GameDir -File -Recurse -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch '\\(main|pb|miles|Docs|3dm)\\' -and
                 $_.Name -match '^(game_starter\.(exe|sha256)|launcher\.ini|dxvk\.conf)-bak$' })
Check ($stale.Count -eq 0) '游戏目录（含 _dxvk）不堆我们自己的交付残留（*-bak）' `
  (($stale | ForEach-Object { $_.FullName }) -join ', ')
$empty = Join-Path $GameDir '_dxvk\禁用'
if (Test-Path $empty) {
  Check (@(Get-ChildItem $empty -Recurse -File).Count -gt 0) '_dxvk\禁用\ 只在真存过不同版本时才留着' '空目录留在交付目录里，看着像少了东西'
} else { Write-Output 'PASS 没有空的 _dxvk\禁用\ 目录' }

Write-Output '===== 8) 没碰玩家自己的设置 / 残留进程'
if ($cfgBefore -ne '') {
  Check ((Get-FileHash $cfg).Hash -eq $cfgBefore) '跑完整套自检，引擎配置 main\players\3dm\config.cfg 一字节没变' `
    '启动器改了玩家的游戏内设置（分辨率 / 按键会被盖掉）'
}
$left = @(Get-Process -Name 'CoD2SP_s', 'CoD2MP_s' -ErrorAction SilentlyContinue)
Check ($left.Count -eq 0) '没有游戏进程残留' (($left | ForEach-Object { $_.ProcessName + '#' + $_.Id }) -join ', ')
$left2 = @(Get-Process -Name 'game_starter' -ErrorAction SilentlyContinue)
Check ($left2.Count -eq 0) '没有 game_starter 进程残留' (($left2 | ForEach-Object { 'pid ' + $_.Id }) -join ', ')

Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
Write-Output '===================='
if ($fail -eq 0) { Write-Output '收尾自检：全部 PASS（全程没有启动游戏）'; exit 0 }
Write-Output ('收尾自检：' + $fail + ' 项 FAIL')
exit $fail
