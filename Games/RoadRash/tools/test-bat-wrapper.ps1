# 验证无控制台薄包装 game_starter.bat：
#   ① 跑起来后，包装用的 cmd 控制台窗口不可见
#   ② bat 的退出码 == 游戏的退出码（假游戏约定 7）
param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [int]$FakeExit = 7,
  [int]$FakeSeconds = 8
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class Win {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
  public delegate bool EnumProc(IntPtr h, IntPtr lp);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public static List<string> Titles(uint pid) {
    List<string> r = new List<string>();
    EnumWindows(delegate(IntPtr h, IntPtr lp) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        int len = GetWindowTextLength(h);
        if (len > 0) { StringBuilder sb = new StringBuilder(len + 1); GetWindowText(h, sb, sb.Capacity); r.Add(sb.ToString()); }
      }
      return true;
    }, IntPtr.Zero);
    return r;
  }
}
"@

$ini = Join-Path $GameDir 'launcher.ini'
$bak = "$ini.wrapper-bak"
Copy-Item $ini $bak -Force
$f = @"
[Launcher]
Title=无控制台包装测试
GameExe=C:\Windows\System32\cmd.exe
GameArgs=/c ping -n $FakeSeconds 127.0.0.1 >nul & exit $FakeExit
LayerDll=ddraw.dll
LayerIni=ddraw.ini
LayerBackupDir=_cnc-ddraw
KeymapHelper=_tools\keymap.exe
"@
[IO.File]::WriteAllText($ini, $f, (New-Object System.Text.UTF8Encoding($false)))

$fail = 0
try {
  $p = Start-Process cmd.exe -ArgumentList '/c', (Join-Path $GameDir 'game_starter.bat') -PassThru -WorkingDirectory $GameDir
  Start-Sleep -Seconds 4

  $go = Get-Process game_starter -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $go) { Write-Output 'FAIL game_starter.exe 没起来'; $fail = 1 }
  else {
    $cmdPids = @($p.Id)
    $kids = Get-CimInstance Win32_Process -Filter "ParentProcessId=$($p.Id)" | Where-Object { $_.Name -eq 'cmd.exe' }
    foreach ($k in $kids) { $cmdPids += [int]$k.ProcessId }
    $visible = @()
    foreach ($cp in $cmdPids) {
      foreach ($t in [Win]::Titles([uint32]$cp)) { $visible += "$cp|$t" }
    }
    if ($visible.Count -eq 0) { Write-Output 'OK   包装用的 cmd 控制台窗口不可见（无黑框）' }
    else { Write-Output ('FAIL 还看得见窗口：' + ($visible -join ' / ')); $fail = 1 }
  }

  $p.WaitForExit()
  if ([int]$p.ExitCode -eq $FakeExit) { Write-Output ("OK   bat 退出码 = " + $p.ExitCode + "（== 游戏退出码）") }
  else { Write-Output ("FAIL bat 退出码 = " + $p.ExitCode + "，期望 " + $FakeExit); $fail = 1 }
}
finally {
  Move-Item $bak $ini -Force
  Write-Output ('ini 已还原：' + ((Get-Content $ini | Select-String '^GameExe').Line))
}
if ($fail -eq 0) { Write-Output 'PASS 无控制台薄包装验证通过' } else { Write-Output "FAIL 共 $fail 项" }
exit $fail
