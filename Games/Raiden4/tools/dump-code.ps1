# 纯读 dump：把 TeknoParrot.dll 映像的全部可执行内存区域读盘（运行时解包后的明文代码）。
param([string]$OutDir = 'D:\Temp\raiden-mem')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
public static class CodeDump
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct MBI
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    public static List<string> Run(int pid, long tpBase, string outDir)
    {
        List<string> log = new List<string>();
        IntPtr hp = OpenProcess(0x0410, false, pid);
        if (hp == IntPtr.Zero) { log.Add("OpenProcess 失败"); return log; }
        Directory.CreateDirectory(outDir);
        long addr = 0;
        while (true)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            long b = mbi.BaseAddress.ToInt64();
            uint pr = mbi.Protect;
            bool exec = (pr == 0x10 || pr == 0x20 || pr == 0x40 || pr == 0x80);
            if (mbi.State == 0x1000 && exec && mbi.AllocationBase.ToInt64() == tpBase && size <= (1 << 27))
            {
                string path = Path.Combine(outDir, b.ToString("X8") + ".bin");
                using (FileStream fs = File.Create(path))
                {
                    long done = 0;
                    byte[] buf = new byte[1 << 20];
                    while (done < size)
                    {
                        int chunk = (int)Math.Min(buf.Length, size - done);
                        int got;
                        if (!ReadProcessMemory(hp, (uint)(b + done), buf, chunk, out got) || got <= 0) break;
                        fs.Write(buf, 0, got);
                        done += got;
                    }
                    log.Add("dump base=" + b.ToString("X8") + " size=" + size.ToString("X8") + " got=" + done.ToString("X8") + " -> " + path);
                }
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        CloseHandle(hp);
        return log;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'dump: 没有 game 进程'; exit 1 }
$tp = $p.Modules | Where-Object { $_.ModuleName -eq 'TeknoParrot.dll' } | Select-Object -First 1
if (-not $tp) { Write-Output 'dump: 没找到 TeknoParrot.dll 模块'; exit 1 }
Write-Output ("tp base=" + $tp.BaseAddress.ToInt64().ToString("X8"))
foreach ($line in [CodeDump]::Run($p.Id, $tp.BaseAddress.ToInt64(), $OutDir)) { Write-Output $line }