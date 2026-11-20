# 纯读扫描：dump 游戏进程全部私有可执行内存区（运行时解密代码候选）＋熵/模块清单。不写游戏内存。
param([string]$OutDir = 'D:\Temp\code-regions', [int]$TargetPid = 0)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
public static class CodeScan
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct MBI
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    static double Entropy(byte[] b, int n)
    {
        int[] freq = new int[256];
        for (int i = 0; i < n; i++) freq[b[i]]++;
        double e = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = (double)freq[i] / n;
            e -= p * Math.Log(p, 2);
        }
        return e;
    }
    public static int Run(int pid, string outDir)
    {
        Directory.CreateDirectory(outDir);
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess 失败"); return 1; }
        string manifest = Path.Combine(outDir, "manifest.txt");
        long addr = 0;
        int dumped = 0;
        while (true)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            bool exec = (mbi.Protect == 0x10 || mbi.Protect == 0x20 || mbi.Protect == 0x40 || mbi.Protect == 0x80);
            if (mbi.State == 0x1000 && exec)
            {
                string kind = mbi.Type == 0x20000 ? "PRIVATE" : (mbi.Type == 0x1000000 ? "IMAGE" : "MAPPED");
                string line = kind + " base=" + addr.ToString("X8") + " size=" + size.ToString("X8") + " protect=" + mbi.Protect.ToString("X2");
                if (mbi.Type == 0x20000 && size <= (1 << 26))
                {
                    byte[] buf = new byte[size];
                    int got;
                    if (ReadProcessMemory(hp, (uint)addr, buf, (int)size, out got) && got == size)
                    {
                        string f = Path.Combine(outDir, addr.ToString("X8") + ".bin");
                        File.WriteAllBytes(f, buf);
                        double e = Entropy(buf, got);
                        string head = BitConverter.ToString(buf, 0, Math.Min(32, got)).Replace("-", " ");
                        line += " entropy=" + e.ToString("F3") + " head=" + head + " DUMPED";
                        dumped++;
                    }
                    else line += " READ_FAIL";
                }
                File.AppendAllText(manifest, line + "\r\n");
                Console.WriteLine(line);
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        CloseHandle(hp);
        Console.WriteLine("dumped=" + dumped);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if ($TargetPid -eq 0) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { Write-Output '没找到 game 进程'; exit 1 }
    $TargetPid = $p.Id
}
[CodeScan]::Run($TargetPid, $OutDir) | Out-Null
Get-Process -Id $TargetPid | Select-Object -ExpandProperty Modules |
    Where-Object { $_.FileName -notmatch 'Windows\\(System32|SysWOW64|WinSxS)' } |
    ForEach-Object { 'MODULE base=' + $_.BaseAddress.ToInt64().ToString('X8') + ' size=' + $_.ModuleMemorySize.ToString('X8') + ' ' + $_.FileName } |
    Tee-Object -FilePath (Join-Path $OutDir 'modules.txt')