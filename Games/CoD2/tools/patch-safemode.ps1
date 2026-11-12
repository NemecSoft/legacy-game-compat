param([string]$GameDir = 'D:\YunGame\CoD\CoD2', [switch]$Restore)

# 使命召唤2：跳过「上次没正常退出，是否以安全模式执行？」那个繁体 Big5 询问框。
# 网上公开方案只有「启动前删 __CoD2SP_s 标记」（启动器已做），但玩家直接双击游戏 exe 时照样弹；
# 标记检查函数没有 cvar / 命令行开关（扫过 exe 字符串），所以做最小字节补丁：
#   FUN_00450700（VA 0x450700 / 文件偏移 0x50700）= 读标记 → 比 PID → MessageBoxA → 写标记。
#   函数头 6 字节 A0 F0 6F 82 00 83 改成 B8 01 00 00 00 C3（MOV EAX,1; RET）= 永远「继续启动」：
#   不弹框、不置安全模式标志（不降 640×480）、不再写标记文件；返回值 1 与原成功路径一致。
# 证据：Games\CoD2\evidence\safemode-patch.txt（反汇编出处 + 补丁前后哈希）。
# 回滚：-Restore 把 _orig\ 里的原文件盖回去（启动器 /restore-exe 同逻辑）。

$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDir 'CoD2SP_s.exe'
$orig = Join-Path $GameDir '_orig\CoD2SP_s.exe'
$repoBak = Join-Path $PSScriptRoot '..\backup\CoD2SP_s.exe.orig'
$off = 0x50700
$wantOrig = [byte[]](0xA0, 0xF0, 0x6F, 0x82, 0x00, 0x83)
$wantPatch = [byte[]](0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3)
$origSha = '2be375ec149c54470a6d2ce3b04b5edf4dd3e7a679ea5db58420a0841c1fa386'

function BytesAt([string]$path, [int]$o, [int]$n) {
  $fs = [IO.File]::OpenRead($path); $fs.Position = $o
  $b = New-Object byte[] $n; [void]$fs.Read($b, 0, $n); $fs.Close(); return $b
}
function Same([byte[]]$a, [byte[]]$b) { return (@(Compare-Object $a $b -SyncWindow 0).Count -eq 0) }

if ($Restore) {
  if (-not (Test-Path $orig)) { Write-Output 'FAIL 没有 _orig\CoD2SP_s.exe，无从回滚'; exit 1 }
  if ((Get-FileHash $orig).Hash.ToLowerInvariant() -ne $origSha) { Write-Output 'FAIL _orig 哈希不对，不敢盖'; exit 1 }
  Copy-Item $orig $exe -Force
  Write-Output ('RESTORED 已回滚原版，sha256=' + (Get-FileHash $exe).Hash.ToLowerInvariant())
  exit 0
}

$cur = BytesAt $exe $off 6
if (Same $cur $wantPatch) { Write-Output 'ALREADY 补丁已在位，不动'; exit 0 }
if (-not (Same $cur $wantOrig)) {
  Write-Output ('FAIL 偏移 0x50700 处字节不是原版也不是补丁版（' + (($cur | ForEach-Object { $_.ToString('X2') }) -join ' ') + '）， exe 版本不对，停手')
  exit 1
}
if ((Get-FileHash $exe).Hash.ToLowerInvariant() -ne $origSha) { Write-Output 'FAIL 整文件哈希与取证时的原版不一致，停手'; exit 1 }
New-Item -ItemType Directory -Force -Path (Split-Path $orig -Parent) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $repoBak -Parent) | Out-Null
Copy-Item $exe $orig -Force
Copy-Item $exe $repoBak -Force
$fs = [IO.File]::Open($exe, 'Open', 'ReadWrite'); $fs.Position = $off
$fs.Write($wantPatch, 0, 6); $fs.Close()
$after = BytesAt $exe $off 6
if (-not (Same $after $wantPatch)) { Write-Output 'FAIL 写后回读不一致'; exit 1 }
Write-Output ('PATCHED 原版 sha256=' + $origSha)
Write-Output ('PATCHED 补丁后 sha256=' + (Get-FileHash $exe).Hash.ToLowerInvariant())
Write-Output ('BACKUP ' + $orig)
Write-Output ('BACKUP ' + $repoBak)
exit 0