# 签名找真玩家命数地址 L：[L]==life(1..3) 且 [L+4]==0x85 且 [L+0x38] in {0x80,0xC0}。
# 真玩家 struct base = L-0x40（+0x44=0x85、+0x70=float、+0x78=0x80/0xC0 特征）。
param([int]$Life = 3, [string]$Out = 'D:\Temp\find-player.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class FindPlayer
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
    public static int Run(int pid, int life, string outPath)
    {
        IntPtr hp = OpenProcess(0x0010, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess fail"); return 1; }
        List<string> hits = new List<string>();
        long addr = 0;
        byte[] buf = new byte[1 << 20];
        while (true)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            bool writable = (mbi.Protect == 0x04 || mbi.Protect == 0x02 || mbi.Protect == 0x40 || mbi.Protect == 0x20);
            if (mbi.State == 0x1000 && writable && size > 0 && size <= (1 << 28))
            {
                long done = 0;
                while (done < size)
                {
                    int chunk = (int)Math.Min(buf.Length, size - done);
                    int got;
                    if (ReadProcessMemory(hp, (uint)(addr + done), buf, chunk, out got) && got > 0)
                    {
                        for (int i = 0; i + 0x40 <= got; i += 4)
                        {
                            if (BitConverter.ToInt32(buf, i) != life) continue;
                            uint f = BitConverter.ToUInt32(buf, i + 0x38);
                            if (f != 0x80 && f != 0xC0) continue;
                            hits.Add((addr + done + i).ToString("X8"));
                        }
                    }
                    if (chunk <= 0x40) break;
                    done += chunk - 0x40;
                }
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        CloseHandle(hp);
        string line = "life=" + life + " hits=" + hits.Count + ": " + string.Join(",", hits);
        File.AppendAllText(outPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + "\r\n");
        Console.WriteLine(line);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
[FindPlayer]::Run($p.Id, $Life, $Out) | Out-Null