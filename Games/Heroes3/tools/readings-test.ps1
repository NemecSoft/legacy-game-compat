param([string]$GameDir = 'D:\YunGame\E\Heroes3')
# 现场读数回归：跑 exe 的 /readings（不启动游戏），断言刷新率/磁盘/分辨率这些「会被当除数」的值都合理。
# 这条是为了兜住「GetDeviceCaps 常量抄错、读完还当正常」这类低级错误：读错就必须红。
$ErrorActionPreference = 'Stop'
$log = Join-Path $GameDir '运行日志.txt'
if (Test-Path $log) { Remove-Item $log -Force }
$out = & cmd.exe /c "`"$(Join-Path $GameDir 'game_starter.exe')`" /readings" 2>&1
$code = $LASTEXITCODE
Start-Sleep -Milliseconds 300
$txt = if (Test-Path $log) { [IO.File]::ReadAllText($log, [Text.Encoding]::UTF8) } else { '' }

$fail = 0
function Check($ok, $name, $detail) {
  if ($ok) { Write-Output ("PASS " + $name) }
  else { $script:fail++; Write-Output ("FAIL " + $name + "  " + $detail) }
}

Check ($code -eq 0) '/readings 退出码 0' ("实际 " + $code)
Check ($txt -match '现场快照') '日志里有现场快照' '没有现场快照行'
Check ($txt -match '读数校验：全部合理') '读数校验通过' '日志里不是「全部合理」'

if ($txt -match '刷新率=(-?\d+)') {
  $r = [int]$Matches[1]
  Check ($r -ge 1 -and $r -le 1000) ('刷新率合理（' + $r + '）') '刷新率不在 1..1000，说明又读错字段了'
} else { Check $false '读到刷新率' '日志里没有刷新率' }

if ($txt -match '每扇区字节=(\d+) 每簇扇区=(\d+)') {
  $bps = [int]$Matches[1]; $spc = [int]$Matches[2]
  Check (($bps -in 512, 1024, 2048, 4096) -and ($spc -ge 1 -and $spc -le 128)) `
    ("磁盘扇区参数合理（" + $bps + "/" + $spc + "）") '每扇区字节或每簇扇区不是常见值'
} else { Check $false '读到磁盘参数' '日志里没有扇区参数' }

if ($txt -match '簇 总(\d+)/可用(\d+)') {
  $total = [long]$Matches[1]; $free = [long]$Matches[2]
  Check ($free -le $total) ('可用簇 ≤ 总簇（' + $free + '/' + $total + '）') '可用簇大于总簇：网络盘返回了不自洽的值'
} else { Check $false '读到簇数' '日志里没有簇总数' }

# 盘类型与 DPI：这两个是「客户机与开发机最容易不一样」的地方，必须打在日志里且不能是未知/0
if ($txt -match '游戏盘\s+\S+\s*（(.+?)）') {
  $dt = $Matches[1]
  Check ($dt -in '本地硬盘', '网络盘', '光盘', '可移动盘', '内存盘') ('游戏盘类型认出来了（' + $dt + '）') `
    "盘类型是「$dt」：GetDriveType 常量又抄错了"
} else { Check $false '读到游戏盘类型' '现场快照里没有盘类型' }

if ($txt -match 'DPI=(\d+)/(\d+)') {
  $dx = [int]$Matches[1]; $dy = [int]$Matches[2]
  Check ($dx -ge 96 -and $dx -le 480 -and $dy -ge 96 -and $dy -le 480) ('DPI 合理（' + $dx + '/' + $dy + '）') `
    'DPI 读出 0 或离谱值：老游戏拿它当除数就是 0xC0000094'
} else { Check $false '读到 DPI' '现场快照里没有 DPI' }

Write-Output ('FAILURES=' + $fail)
exit $fail
