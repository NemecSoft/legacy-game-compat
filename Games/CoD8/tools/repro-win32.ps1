# 试验：用最原始的 Win32 CreateProcess 直连 iw5sp.exe（不走 cmd start /D），
# 复刻当时黑屏的那条启动路，每 10 秒采样进程状态 + 截图。
# 只观察不杀进程，60 秒后仍活着就留给用户亲眼看。
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

Add-Type -Namespace Win32 -Name Proc -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
public static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
public struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput, hStdOutput, hStdError; }
[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId; }
'@

$exe = Join-Path $game 'iw5sp.exe'
$si = New-Object Win32.Proc+STARTUPINFO
$si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si)
$pi = New-Object Win32.Proc+PROCESS_INFORMATION
$ok = [Win32.Proc]::CreateProcess($exe, "`"$exe`"", [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x00000010, [IntPtr]::Zero, $game, [ref]$si, [ref]$pi)
if (-not $ok) {
  Write-Output ("CreateProcess 失败 err=" + [Runtime.InteropServices.Marshal]::GetLastWin32Error())
  exit 1
}
Write-Output ("CreateProcess 成功 pid=" + $pi.dwProcessId + " 工作目录=$game")

for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-win32-t$($i*10)s.png")) -WaitSeconds 2 -KeepOpen 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 pid=" + $gp.Id + " 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding + " 标题=" + $gp.MainWindowTitle)
  } else {
    Write-Output "t+$($i*10)s 游戏进程没了"
    break
  }
}
$gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gp) { Write-Output '60 秒采样结束，进程仍在跑，留给你亲眼确认画面。' }
