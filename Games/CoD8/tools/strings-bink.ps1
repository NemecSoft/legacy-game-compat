# 解剖 hook 版 binkw32.dll：抓 ASCII/Unicode 字符串里的线索
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$bytes = [IO.File]::ReadAllBytes('D:\YunGame\CoD\CoD8\binkw32.dll')
Write-Output ("文件大小: " + $bytes.Length)

# ASCII 串（长度>=6）
$sb = New-Object Text.StringBuilder
$ascii = New-Object System.Collections.Generic.List[string]
foreach ($b in $bytes) {
  if ($b -ge 0x20 -and $b -le 0x7E) { [void]$sb.Append([char]$b) }
  else { if ($sb.Length -ge 6) { $ascii.Add($sb.ToString()) }; [void]$sb.Clear() }
}
Write-Output ("ASCII 串总数: " + $ascii.Count)
Write-Output '===== 网络相关（http/https/www/域名后缀）====='
$ascii | Where-Object { $_ -match 'http|www\.|\.com|\.net|\.cn|\.org|api|login|auth|key|licen|token|verify|check' } | Select-Object -First 40 | ForEach-Object { Write-Output $_ }
Write-Output '===== 文件/系统相关 ====='
$ascii | Where-Object { $_ -match '\.dll|\.cfg|\.txt|\.ini|\.dat|\.iw5|\\|/|HKLM|HKCU|SOFTWARE|mutex|Mutex' } | Select-Object -First 40 | ForEach-Object { Write-Output $_ }
Write-Output '===== 全部串抽样（前 60 条 + 后 40 条）====='
$ascii | Select-Object -First 60 | ForEach-Object { Write-Output $_ }
Write-Output '...'
$ascii | Select-Object -Last 40 | ForEach-Object { Write-Output $_ }
