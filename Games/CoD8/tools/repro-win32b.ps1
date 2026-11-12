# 试验二：Win32 CreateProcess 直连 iw5sp.exe，这次保留进程句柄，退出后抓退出码
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$game = 'D:\YunGame\CoD\CoD8'
$ev   = 'D:\AI\legacy-game-compat\Games\CoD8\evidence'

Add-Type -Namespace Win32 -Name Proc2 -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
public static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
[DllImport("kernel32.dll", SetLastError=true)]
public static extern uint WaitForSingleObject(IntPtr h, uint ms);
[DllImport("kernel32.dll", SetLastError=true)]
public static extern bool GetExitCodeProcess(IntPtr h, out uint code);
[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
public struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput, hStdOutput, hStdError; }
[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId; }
'@

$exe = Join-Path $game 'iw5sp.exe'
$si = New-Object Win32.Proc2+STARTUPINFO
$si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si)
$pi = New-Object Win32.Proc2+PROCESS_INFORMATION
$ok = [Win32.Proc2]::CreateProcess($exe, "`"$exe`"", [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x00000010, [IntPtr]::Zero, $game, [ref]$si, [ref]$pi)
if (-not $ok) { Write-Output ("CreateProcess 失败 err=" + [Runtime.InteropServices.Marshal]::GetLastWin32Error()); exit 1 }
Write-Output ("CreateProcess 成功 pid=" + $pi.dwProcessId + " 工作目录=$game")

for ($i = 1; $i -le 6; $i++) {
  Start-Sleep -Seconds 10
  $gp = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gp) {
    & 'D:\AI\legacy-game-compat\scripts\capture-window.ps1' -Process iw5sp -Out (Join-Path $ev ("repro-win32b-t$($i*10)s.png")) -WaitSeconds 2 2>$null | Out-Null
    Write-Output ("t+$($i*10)s 活着 pid=" + $gp.Id + " 主窗=" + ($gp.MainWindowHandle -ne 0) + " 响应=" + $gp.Responding)
  } else {
    Write-Output ("t+$($i*10)s 游戏进程没了，取退出码…")
    break
  }
}
# 最多再等 20 秒拿退出码
$w = [Win32.Proc2]::WaitForSingleObject($pi.hProcess, 20000)
$code = 0
[void][Win32.Proc2]::GetExitCodeProcess($pi.hProcess, [ref]$code)
Write-Output ("退出码=" + $code + " (0x" + $code.ToString('X') + ")")
