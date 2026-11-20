# 真无敌猎手：签名找命数 L -> 轮询等死亡(值变) -> 重生圈活跃期连 dump base 两次 -> 列出变化字段(候选无敌计时器)。
# 配合外部 forcing 死亡（send-start 11 连发）。纯读+日志，不写游戏。
param([int]$Life = 3, [int]$WaitSec = 60, [string]$Out = 'D:\Temp\god-hunt.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class GodHunt
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
    static void Log(string p, string s) { string l = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s; File.AppendAllText(p, l + "\r\n"); Console.WriteLine(l); }
    static uint Rd(IntPtr h, uint a) { byte[] b = new byte[4]; int g; if (!ReadProcessMemory(h, a, b, 4, out g) || g != 4) return 0xFFFFFFFF; return BitConverter.ToUInt32(b, 0); }
    static List<uint> Find(IntPtr h, int life)
    {
        List<uint> hits = new List<uint>();
        long addr = 0; byte[] buf = new byte[1 << 20];
        while (true)
        {
            MBI mbi; int r = VirtualQueryEx(h, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64(); if (size <= 0) break;
            bool w = (mbi.Protect == 0x04 || mbi.Protect == 0x02 || mbi.Protect == 0x40 || mbi.Protect == 0x20);
            if (mbi.State == 0x1000 && w && size <= (1 << 28))
            {
                long done = 0;
                while (done < size)
                {
                    int chunk = (int)Math.Min(buf.Length, size - done); int got;
                    if (ReadProcessMemory(h, (uint)(addr + done), buf, chunk, out got) && got > 0)
                        for (int i = 0; i + 0x40 <= got; i += 4)
                        {
                            if (BitConverter.ToInt32(buf, i) != life) continue;
                            if (BitConverter.ToUInt32(buf, i + 4) != 0x85) continue;
                            uint f = BitConverter.ToUInt32(buf, i + 0x38);
                            if (f != 0x80 && f != 0xC0) continue;
                            hits.Add((uint)(addr + done + i));
                        }
                    if (chunk <= 0x40) break;
                    done += chunk - 0x40;
                }
            }
            addr += size; if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        return hits;
    }
    public static int Run(int pid, int life, int waitSec, string outPath)
    {
        IntPtr h = OpenProcess(0x0010, false, pid);
        if (h == IntPtr.Zero) { Log(outPath, "OpenProcess fail"); return 1; }
        List<uint> hits = Find(h, life);
        Log(outPath, "L hits=" + hits.Count + ": " + string.Join(",", hits.ConvertAll(x => x.ToString("X8"))));
        if (hits.Count == 0) { CloseHandle(h); return 1; }
        int waited = 0;
        while (waited < waitSec * 1000)
        {
            uint L = 0; uint v = 0; bool changed = false;
            foreach (uint cand in hits) { uint cv = Rd(h, cand); if (cv != (uint)life) { L = cand; v = cv; changed = true; break; } }
            if (changed)
            {
                uint baseA = L - 0x40;
                Log(outPath, "death detected L=" + L.ToString("X8") + " " + life + "->" + v + " base=" + baseA.ToString("X8"));
                byte[] a = new byte[0x100]; byte[] b = new byte[0x100]; int g;
                ReadProcessMemory(h, baseA, a, 0x100, out g);
                Thread.Sleep(2000);
                ReadProcessMemory(h, baseA, b, 0x100, out g);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 0x100; i += 4)
                {
                    uint va = BitConverter.ToUInt32(a, i), vb = BitConverter.ToUInt32(b, i);
                    if (va != vb) sb.Append("+" + i.ToString("X3") + ": " + va.ToString("X8") + " -> " + vb.ToString("X8") + "\r\n");
                }
                Log(outPath, "changed fields:\r\n" + sb);
                File.WriteAllBytes("D:\\Temp\\hunt-t0.bin", a);
                File.WriteAllBytes("D:\\Temp\\hunt-t1.bin", b);
                CloseHandle(h);
                return 0;
            }
            Thread.Sleep(100); waited += 100;
        }
        Log(outPath, "no death within " + waitSec + "s");
        CloseHandle(h);
        return 1;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
[GodHunt]::Run($p.Id, $Life, $WaitSec, $Out) | Out-Null