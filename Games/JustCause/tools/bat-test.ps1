param([string]$GameDir='D:\YunGame\Z\JustCause1')
$ErrorActionPreference='Stop'
Add-Type @"
using System;using System.Runtime.InteropServices;
public class B {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@
$fail=0
function Check([string]$n,[bool]$ok,[string]$d=''){
  if($ok){Write-Output ("PASS  $n  $d")}else{$script:fail++;Write-Output ("FAIL  $n  $d")}
}
$dir=$GameDir
$rcFile='D:\AI\legacy-game-compat\Games\JustCause\tools\bat-rc.txt'
$wrapper='D:\AI\legacy-game-compat\Games\JustCause\tools\bat-test-run.bat'
Remove-Item $rcFile -EA SilentlyContinue
@"
@echo off
(echo 2) | "$dir\game_starter.bat"
echo %ERRORLEVEL% > "$rcFile"
"@ | Out-File -FilePath $wrapper -Encoding ascii
$p=Start-Process -FilePath 'cmd.exe' -ArgumentList '/c',"`"$wrapper`"" -WorkingDirectory $dir -PassThru
Start-Sleep -Seconds 16
$conf=Get-Content (Join-Path $dir 'dxvk.conf') -Raw
Check '菜单选 2 后 dxvk.conf 写入 HUD' ($conf -match 'dxvk\.hud\s*=\s*fps') ($conf.Trim() -replace "`r?`n",' | ')
$game=Get-Process JustCause -EA SilentlyContinue | Select-Object -First 1
Check '游戏已启动' ([bool]$game) ("pid=" + $(if($game){$game.Id}else{0}))
if($game){
  $wmi=Get-CimInstance Win32_Process -Filter ("ProcessId=" + $game.Id)
  $parent=Get-CimInstance Win32_Process -Filter ("ProcessId=" + $wmi.ParentProcessId) -EA SilentlyContinue
  Check '游戏的父进程是运行 bat 的 cmd' ($parent -and $parent.Name -eq 'cmd.exe') ("parent=" + $wmi.ParentProcessId + " (" + $parent.Name + "); launcher cmd=" + $p.Id)
  [B]::PostMessage($game.MainWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero) | Out-Null
  $p.WaitForExit(60000) | Out-Null
  Check '游戏退出后 bat 结束' $p.HasExited
  Start-Sleep -Seconds 1
  $rc=(Get-Content $rcFile -Raw).Trim()
  Check 'bat 回传游戏退出码 0' ($rc -eq '0') ("rc=" + $rc)
  $conf2=Get-Content (Join-Path $dir 'dxvk.conf') -Raw
  Check '退出后 dxvk.conf 恢复默认（无 HUD）' (-not ($conf2 -match 'dxvk\.hud')) ($conf2.Trim() -replace "`r?`n",' | ')
}
Get-Process JustCause -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Remove-Item $wrapper -Force -EA SilentlyContinue
Write-Output ("FAILURES=" + $fail)
exit $fail
