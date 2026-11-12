# 退出码回传回归：不起界面、不启动游戏，只验证 --selftest 把子程序退出码原样回传。
param([string]$Exe = 'D:\YunGame\CoD\CoD4.Remastered\game_starter.exe')

$ErrorActionPreference = 'Stop'
$fail = 0
foreach ($code in @(42, 0)) {
  $p = Start-Process -FilePath $Exe -ArgumentList @('--selftest', 'powershell.exe', '-NoProfile', '-Command', "exit $code") -Wait -PassThru
  if ($p.ExitCode -ne $code) {
    Write-Host "FAIL 期望 $code，实际 $($p.ExitCode)"
    $fail++
  } else {
    Write-Host "PASS 子程序退出码 $code 原样回传"
  }
}
Write-Host ("结果：" + $(if ($fail -eq 0) { '全部通过' } else { "$fail 项失败" }))
exit $fail
