# 雷电4 收尾自检：作弊写入回归 + 环境读数 + 日志自证（全程不启动游戏）
$ErrorActionPreference = 'Stop'
$Game = 'D:\YunGame\G\Raiden4'
$exe = Join-Path $Game 'game_starter.exe'
$ini = Join-Path $Game 'JJ11.ini'
$bak = Join-Path $Game 'JJ11.ini.sjis-bak'
$fail = 0

function Run-Switch([string]$Arg) {
  $p = Start-Process -FilePath $exe -ArgumentList $Arg -WorkingDirectory $Game -PassThru -Wait
  return $p.ExitCode
}
function Check([string]$Name, [bool]$Ok) {
  if ($Ok) { Write-Output ("合格 " + $Name) } else { Write-Output ("不合格 " + $Name); $script:fail = 1 }
}
function Split-Lines([byte[]]$Bytes) {
  $lines = New-Object System.Collections.Generic.List[byte[]]
  $cur = New-Object System.Collections.Generic.List[byte]
  foreach ($b in $Bytes) {
    if ($b -eq 10) { $lines.Add($cur.ToArray()); $cur.Clear() } else { $cur.Add($b) }
  }
  if ($cur.Count -gt 0) { $lines.Add($cur.ToArray()) }
  return ,$lines
}
function Same-Bytes([byte[]]$A, [byte[]]$B) {
  if ($A.Length -ne $B.Length) { return $false }
  for ($i = 0; $i -lt $A.Length; $i++) { if ($A[$i] -ne $B[$i]) { return $false } }
  return $true
}

# 0. 留底当前 ini（测试完原样还原，不动玩家配置）
$orig = [IO.File]::ReadAllBytes($ini)
$origLines = Split-Lines $orig
Check '原版备份存在' (Test-Path $bak)

# 1. 写 TRUE -> 读回 TRUE
Check 'jjset no_death=TRUE 退出码0' ((Run-Switch '--jjset=no_death=TRUE') -eq 0)
Check 'jjget no_death 退出码0' ((Run-Switch '--jjget=no_death') -eq 0)
$read = (Get-Content (Join-Path $Game 'jjread.txt') -Encoding UTF8 -Raw).Trim()
Check '读回 no_death=TRUE' ($read -eq 'no_death=TRUE')

# 2. SJIS 注释保留：原文件每一行逐字节不变；差异只允许是「末尾追加一条 no_death 生效行」
#    或「就地替换一条 no_death 生效行」（原版里作弊键全是注释行，首次写入走追加）
$nowLines = Split-Lines ([IO.File]::ReadAllBytes($ini))
$diffOk = $true
$extra = $nowLines.Count - $origLines.Count
if ($extra -lt 0 -or $extra -gt 1) { $diffOk = $false }
else {
  for ($i = 0; $i -lt $origLines.Count; $i++) {
    if (-not (Same-Bytes $origLines[$i] $nowLines[$i])) {
      $a = [Text.Encoding]::GetEncoding(932).GetString($origLines[$i])
      $b = [Text.Encoding]::GetEncoding(932).GetString($nowLines[$i])
      if (-not (($a -match '^\s*no_death\s*=') -and ($b -match '^\s*no_death\s*='))) { $diffOk = $false }
    }
  }
  if ($extra -eq 1) {
    $last = [Text.Encoding]::GetEncoding(932).GetString($nowLines[$nowLines.Count - 1])
    if ($last -notmatch '^\s*no_death\s*=') { $diffOk = $false }
  }
}
Check '原行逐字节保留，差异仅一条 no_death 生效行' $diffOk

# 3. 写 FALSE -> 读回 FALSE
Check 'jjset no_death=FALSE 退出码0' ((Run-Switch '--jjset=no_death=FALSE') -eq 0)
Check 'jjget no_death 退出码0' ((Run-Switch '--jjget=no_death') -eq 0)
$read = (Get-Content (Join-Path $Game 'jjread.txt') -Encoding UTF8 -Raw).Trim()
Check '读回 no_death=FALSE' ($read -eq 'no_death=FALSE')

# 4. 原样还原 ini
[IO.File]::WriteAllBytes($ini, $orig)
Check 'ini 已原样还原' (Same-Bytes ([IO.File]::ReadAllBytes($ini)) $orig)

# 5. 环境读数（含 _tp 组件齐全断言）
Check 'readings 退出码0' ((Run-Switch '--readings') -eq 0)
$rd = Get-Content (Join-Path $Game 'readings.txt') -Encoding UTF8
Check 'readings 断言=通过' (($rd -join "`n") -match '断言=通过')

# 6. 日志自证行
$log = Get-Content (Join-Path $Game '运行日志.txt') -Encoding UTF8 -Raw
Check '日志有 JJ11 写入记录' ($log -match 'JJ11\.ini 写入：no_death = TRUE')
Check '日志无 shell 参数转换警告' ($log -notmatch '参数疑似被 shell 路径转换')

if ($fail -eq 0) { Write-Output '收尾自检全部通过'; exit 0 } else { Write-Output '收尾自检有不合格项'; exit 1 }