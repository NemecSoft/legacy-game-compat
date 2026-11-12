# CoD5 启动器 --go 实测：前台归属（零干预验证）+ 存档/画面/重定向/AppData
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$g = 'D:\YunGame\CoD\CoD5'
$ev = 'D:\AI\legacy-game-compat\Games\CoD5\evidence'
$log = "$g\运行日志.txt"
Remove-Item $log -Force -ErrorAction SilentlyContinue
$appCfg = 'C:\Users\Administrator\AppData\Local\Activision\CoDWaW\players\profiles\$$$\config.cfg'
$appBefore = (Get-Item -LiteralPath $appCfg).LastWriteTime

Add-Type -Namespace W -Name U -MemberDefinition '
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
'
function FgPid {
  $h = [W.U]::GetForegroundWindow()
  $p = 0u
  [void][W.U]::GetWindowThreadProcessId($h, [ref]$p)
  return $p
}
function NameOfPid([uint32]$procId) {
  try { return (Get-Process -Id $procId -ErrorAction Stop).ProcessName } catch { return "(pid=$procId)" }
}

$p = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Write-Output ("game_starter pid=" + $p.Id)
for ($i = 1; $i -le 8; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process CoDWaW -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    $fg = FgPid
    $fgName = NameOfPid $fg
    $mini = ($gp.MainWindowHandle -ne 0) -and [W.U]::IsIconic($gp.MainWindowHandle)
    $inFront = ($fg -eq $gp.Id)
    Write-Output ("t+$($i*10)s 存活=" + $gp.Responding + " 主窗=" + ($gp.MainWindowHandle -ne 0) + " 最小化=$mini " + $(if($inFront){"<== 游戏在最前"}else{"前台=$fgName"}) + "（游戏pid=$($gp.Id)）")
    if ($i -eq 4 -or $i -eq 8) {
      & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process CoDWaW -Out (Join-Path $ev ("zero-t$($i*10)s.png")) -WaitSeconds 2 -KeepOpen 2>$null | Out-Null
    }
  } else { Write-Output ("t+$($i*10)s 游戏进程没了"); break }
}
$gp = Get-Process CoDWaW -ErrorAction SilentlyContinue
if ($gp) {
  Write-Output ("80 秒存活，测试结束，收尾杀掉测试进程")
  Get-Process CoDWaW, game_starter -ErrorAction SilentlyContinue | Stop-Process -Force
}
$appAfter = (Get-Item -LiteralPath $appCfg).LastWriteTime
Write-Output ("AppData config 最后写入：测试前 $appBefore → 测试后 $appAfter（相同 = 零写入）")
Write-Output '----- 运行日志 -----'
Get-Content $log -Encoding UTF8 -ErrorAction SilentlyContinue
