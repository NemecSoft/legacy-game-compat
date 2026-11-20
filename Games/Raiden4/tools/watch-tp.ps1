# 常驻纯读监视：TP.dll 范围内可执行区 head 熵从密文跌到明文瞬间 dump 全区（战斗态按需解密捕获）。
param([string]$OutDir = 'D:\Temp\code-tp-live', [string]$Log = 'D:\Temp\tp-watch.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
public static class TpWatch
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct MBI
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    static void Log(string path, string line)
    {
        string s = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
        File.AppendAllText(path, s + "\r\n");
        Console.WriteLine(s);
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
    public static int Run(int pid, long from, long to, string outDir, string logPath)
    {
        Directory.CreateDirectory(outDir);
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Log(logPath, "OpenProcess 失败"); return 1; }
        Log(logPath, "tp-watch start pid=" + pid + " range=" + from.ToString("X8") + "-" + to.ToString("X8"));
        Dictionary<long, double> prev = new Dictionary<long, double>();
        int dumps = 0;
        byte[] head = new byte[0x1000];
        while (true)
        {
            if (WaitForSingleObject(hp, 0) == 0) { Log(logPath, "game 退出，tp-watch 本轮结束"); break; }
            long addr = from;
            while (addr < to)
            {
                MBI mbi;
                int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
                if (r == 0) break;
                long size = mbi.RegionSize.ToInt64();
                if (size <= 0) break;
                bool exec = (mbi.Protect == 0x10 || mbi.Protect == 0x20 || mbi.Protect == 0x40 || mbi.Protect == 0x80);
                if (mbi.State == 0x1000 && exec)
                {
                    int got;
                    double e = -1;
                    if (ReadProcessMemory(hp, (uint)addr, head, 0x1000, out got) && got == 0x1000) e = Entropy(head, got);
                    double pe;
                    bool had = prev.TryGetValue(addr, out pe);
                    if (e >= 0)
                    {
                        if (had && pe > 7.5 && e < 7.4 && dumps < 24 && size <= (1 << 27))
                        {
                            byte[] buf = new byte[size];
                            int g2;
                            if (ReadProcessMemory(hp, (uint)addr, buf, (int)size, out g2) && g2 == size)
                            {
                                File.WriteAllBytes(Path.Combine(outDir, addr.ToString("X8") + ".bin"), buf);
                                dumps++;
                                Log(logPath, "解密捕获 base=" + addr.ToString("X8") + " size=" + size.ToString("X8") + " entropy " + pe.ToString("F3") + " -> " + e.ToString("F3") + " 全区域熵=" + Entropy(buf, g2).ToString("F3"));
                            }
                        }
                        prev[addr] = e;
                    }
                }
                addr += size;
            }
            Thread.Sleep(3000);
        }
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
while ($true) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) {
        $tp = $null
        for ($i = 0; $i -lt 40; $i++) {
            $p.Refresh()
            $tp = $p.Modules | Where-Object { $_.FileName -match 'TeknoParrot\.dll$' } | Select-Object -First 1
            if ($tp) { break }
            Start-Sleep -Seconds 2
        }
        if ($tp) {
            [TpWatch]::Run($p.Id, $tp.BaseAddress.ToInt64(), $tp.BaseAddress.ToInt64() + $tp.ModuleMemorySize, $OutDir, $Log) | Out-Null
        }
    }
    Start-Sleep -Seconds 2
}