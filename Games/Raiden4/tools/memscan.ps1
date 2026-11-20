# 内存 trainer 定位工具：扫 game 进程可写内存区的 dword 值，两次快照交集钉地址。
# 用法：
#   scan.ps1 -Mode scan  -Value 3 -Out a.txt      扫值快照
#   scan.ps1 -Mode intersect -A a.txt -B b.txt -Out c.txt   交集（B 里值已变）
#   scan.ps1 -Mode peek  -Addrs c.txt             读候选当前值
#   scan.ps1 -Mode write -Addr 0x... -Value 9     写值验证
param(
  [Parameter(Mandatory = $true)][ValidateSet('scan','intersect','peek','write')][string]$Mode,
  [int]$Value = 3,
  [string]$Out = '',
  [string]$A = '',
  [string]$B = '',
  [string]$Addrs = '',
  [string]$Addr = ''
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
public class MemScan {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr ba, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, IntPtr ba, byte[] buf, int size, out int written);
  [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct MBI {
    public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
    public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
  }
  public static List<long> Scan(int pid, int value) {
    IntPtr h = OpenProcess(0x0410, false, pid); // QUERY_INFORMATION | VM_READ
    if (h == IntPtr.Zero) throw new Exception("OpenProcess 失败 pid=" + pid);
    List<long> hits = new List<long>();
    long addr = 0;
    byte[] buf = new byte[1 << 20];
    while (true) {
      MBI mbi;
      int r = VirtualQueryEx(h, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
      if (r == 0) break;
      long size = mbi.RegionSize.ToInt64();
      bool writable = (mbi.Protect == 0x04 || mbi.Protect == 0x02 || mbi.Protect == 0x40 || mbi.Protect == 0x20);
      if (mbi.State == 0x1000 && writable && size > 0 && size <= (1 << 28)) {
        long done = 0;
        while (done < size) {
          int chunk = (int)Math.Min(buf.Length, size - done);
          int got;
          if (ReadProcessMemory(h, (IntPtr)(addr + done), buf, chunk, out got) && got > 0) {
            for (int i = 0; i + 4 <= got; i += 4) {
              if (BitConverter.ToInt32(buf, i) == value) hits.Add(addr + done + i);
            }
          }
          done += chunk;
        }
      }
      addr += size;
      if (addr <= 0 || addr > 0x7FFF0000L) break;
    }
    CloseHandle(h);
    return hits;
  }
  public static int Peek(int pid, long addr) {
    IntPtr h = OpenProcess(0x0410, false, pid);
    byte[] b = new byte[4]; int got;
    bool ok = ReadProcessMemory(h, (IntPtr)addr, b, 4, out got);
    CloseHandle(h);
    if (!ok || got != 4) throw new Exception("读失败 " + addr.ToString("X"));
    return BitConverter.ToInt32(b, 0);
  }
  public static void Write(int pid, long addr, int value) {
    IntPtr h = OpenProcess(0x0428, false, pid); // + VM_WRITE | VM_OPERATION
    byte[] b = BitConverter.GetBytes(value); int put;
    bool ok = WriteProcessMemory(h, (IntPtr)addr, b, 4, out put);
    CloseHandle(h);
    if (!ok || put != 4) throw new Exception("写失败 " + addr.ToString("X"));
  }
}
'@
$proc = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output '没有 game 进程在跑'; exit 1 }
$pid_ = $proc.Id
$base = $proc.MainModule.BaseAddress.ToInt64()
Write-Output ("game pid=" + $pid_ + " base=0x" + $base.ToString('X'))

function Read-List([string]$f) {
  if (-not (Test-Path $f)) { return @() }
  Get-Content $f | Where-Object { $_ -match '^0x' } | ForEach-Object { [Convert]::ToInt64($_, 16) }
}

switch ($Mode) {
  'scan' {
    $hits = [MemScan]::Scan($pid_, $Value)
    Write-Output ("值=" + $Value + " 命中 " + $hits.Count + " 处")
    if ($Out) { $hits | ForEach-Object { '0x' + $_.ToString('X') } | Set-Content $Out -Encoding ASCII }
    else { $hits | Select-Object -First 20 | ForEach-Object { Write-Output ('0x' + $_.ToString('X')) } }
  }
  'intersect' {
    $la = Read-List $A; $lb = Read-List $B
    $set = New-Object 'System.Collections.Generic.HashSet[long]'
    foreach ($x in $lb) { $null = $set.Add($x) }
    $both = @($la | Where-Object { $set.Contains($_) })
    Write-Output ("A=" + $la.Count + " B=" + $lb.Count + " 交集=" + $both.Count)
    foreach ($x in $both) {
      $v = [MemScan]::Peek($pid_, $x)
      Write-Output ('0x' + $x.ToString('X') + ' rva=0x' + ($x - $base).ToString('X') + ' 当前=' + $v)
    }
    if ($Out) { $both | ForEach-Object { '0x' + $_.ToString('X') } | Set-Content $Out -Encoding ASCII }
  }
  'peek' {
    foreach ($x in (Read-List $Addrs)) {
      $v = [MemScan]::Peek($pid_, $x)
      Write-Output ('0x' + $x.ToString('X') + ' rva=0x' + ($x - $base).ToString('X') + ' 当前=' + $v)
    }
  }
  'write' {
    $x = [Convert]::ToInt64($Addr, 16)
    [MemScan]::Write($pid_, $x, $Value)
    Write-Output ('已写 0x' + $x.ToString('X') + ' = ' + $Value + '，回读=' + [MemScan]::Peek($pid_, $x))
  }
}