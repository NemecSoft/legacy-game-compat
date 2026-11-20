# 看候选地址邻域 dword，找玩家结构体特征（命数旁伴炸弹数等）
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class MemPeek {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr ba, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static int Peek(int pid, long addr) {
    IntPtr h = OpenProcess(0x0410, false, pid);
    byte[] b = new byte[4]; int got;
    bool ok = ReadProcessMemory(h, (IntPtr)addr, b, 4, out got);
    CloseHandle(h);
    if (!ok || got != 4) return -999999;
    return BitConverter.ToInt32(b, 0);
  }
}
'@
$proc = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output '没有 game 进程'; exit 1 }
foreach ($line in (Get-Content 'D:\Temp\mem-c.txt')) {
  $x = [Convert]::ToInt64($line, 16)
  $v = [MemPeek]::Peek($proc.Id, $x)
  if ($v -ne 2) { continue }
  $sb = New-Object Text.StringBuilder
  for ($off = -32; $off -le 32; $off += 4) {
    $d = [MemPeek]::Peek($proc.Id, $x + $off)
    $mark = if ($off -eq 0) { '[' + $d + ']' } else { [string]$d }
    $null = $sb.Append($mark + ' ')
  }
  Write-Output ('0x' + $x.ToString('X') + ': ' + $sb.ToString())
}