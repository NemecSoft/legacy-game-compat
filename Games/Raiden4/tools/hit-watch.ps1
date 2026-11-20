# 连续采样玩家对象池＋活跃内存块；命数扣减（挨打）瞬间 dump 全线程 EIP/代码页/栈。纯读。
param([string]$Out = 'D:\Temp\hit-sample.bin', [string]$Log = 'D:\Temp\hit-watch.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class HitWatch
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll")] public static extern bool Thread32First(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern bool Thread32Next(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenThread(uint acc, bool inh, int tid);
    [DllImport("kernel32.dll")] public static extern bool Wow64GetThreadContext(IntPtr h, byte[] ctx);
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
    static List<long> FindObj(IntPtr hp)
    {
        List<long> hits = new List<long>();
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
                        for (int i = 0; i + 0x3C <= got; i += 4)
                        {
                            long baseA = addr + done + i;
                            if (BitConverter.ToUInt32(buf, i + 0x08) != 0xFFEEFFEE) continue;
                            if (BitConverter.ToUInt32(buf, i + 0x1C) != (uint)baseA) continue;
                            int lv = BitConverter.ToInt32(buf, i + 0x30);
                            if (lv < 0 || lv > 9) continue;
                            hits.Add(baseA);
                        }
                    }
                    if (chunk <= 0x40) break;
                    done += chunk - 0x40;
                }
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        return hits;
    }
    static uint ReadDword(IntPtr hp, long addr)
    {
        byte[] b = new byte[4]; int got;
        if (!ReadProcessMemory(hp, (uint)addr, b, 4, out got) || got != 4) return 0xFFFFFFFF;
        return BitConverter.ToUInt32(b, 0);
    }
    static bool IsPlayer(IntPtr hp, long obj)
    {
        return ReadDword(hp, obj + 0x20) == 0x3FF || ReadDword(hp, obj + 0x24) == (uint)(obj + 0x40);
    }
    static bool ReadLife(IntPtr hp, long obj, out int life)
    {
        byte[] b = new byte[4]; int got;
        life = -1;
        if (!ReadProcessMemory(hp, (uint)(obj + 0x30), b, 4, out got) || got != 4) return false;
        life = BitConverter.ToInt32(b, 0);
        return true;
    }
    static List<long> ActiveScan(IntPtr hp)
    {
        List<long> blocks = new List<long>();
        List<int> scores = new List<int>();
        List<long> logic = new List<long>();
        long addr = 0;
        while (true)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            bool writable = (mbi.Protect == 0x04 || mbi.Protect == 0x02 || mbi.Protect == 0x40 || mbi.Protect == 0x20);
            if (mbi.State == 0x1000 && writable && size > 0x1000 && size <= (1 << 24))
            {
                byte[] a2 = new byte[size];
                byte[] b2 = new byte[size];
                int ga, gb;
                bool okA = ReadProcessMemory(hp, (uint)addr, a2, (int)size, out ga) && ga == size;
                Thread.Sleep(50);
                bool okB = ReadProcessMemory(hp, (uint)addr, b2, (int)size, out gb) && gb == size;
                if (okA && okB)
                {
                    for (int blk = 0; blk + 0x1000 <= size; blk += 0x1000)
                    {
                        int chg = 0;
                        for (int i = blk; i < blk + 0x1000; i += 4)
                            if (BitConverter.ToUInt32(a2, i) != BitConverter.ToUInt32(b2, i)) chg++;
                        if (chg > 60)
                        {
                            blocks.Add(addr + blk);
                            scores.Add(chg);
                        }
                        else if (chg >= 5)
                        {
                            logic.Add(addr + blk);
                        }
                    }
                }
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        List<long> top = new List<long>();
        for (int n = 0; n < 5 && blocks.Count > 0; n++)
        {
            int best = 0;
            for (int i = 1; i < scores.Count; i++) if (scores[i] > scores[best]) best = i;
            top.Add(blocks[best]);
            blocks.RemoveAt(best); scores.RemoveAt(best);
        }
        foreach (long o in logic)
        {
            if (top.Count >= 30) break;
            if (!top.Contains(o)) top.Add(o);
        }
        return top;
    }
    static void DumpRing(IntPtr hp, int pid, long obj, int oldL, int newL, string logPath)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("==== 受击 " + DateTime.Now.ToString("HH:mm:ss.fff") + " obj=" + obj.ToString("X8") + " life " + oldL + " -> " + newL + " ====");
        IntPtr snap = CreateToolhelp32Snapshot(4, 0);
        if (snap != IntPtr.Zero && snap != new IntPtr(-1))
        {
            byte[] te = new byte[28];
            BitConverter.GetBytes((uint)28).CopyTo(te, 0);
            if (Thread32First(snap, te))
            {
                do
                {
                    if (BitConverter.ToInt32(te, 12) != pid) continue;
                    int tid = BitConverter.ToInt32(te, 8);
                    IntPtr ht = OpenThread(0x0048, false, tid);
                    if (ht == IntPtr.Zero) continue;
                    byte[] ctx = new byte[1024];
                    BitConverter.GetBytes((uint)0x10007).CopyTo(ctx, 0);
                    if (Wow64GetThreadContext(ht, ctx))
                    {
                        uint eip = BitConverter.ToUInt32(ctx, 184);
                        uint esp = BitConverter.ToUInt32(ctx, 196);
                        sb.AppendLine("  tid=" + tid + " eip=" + eip.ToString("X8") + " esp=" + esp.ToString("X8"));
                        byte[] code = new byte[32]; int got;
                        if (eip > 0x10000 && ReadProcessMemory(hp, eip, code, 32, out got) && got == 32)
                            sb.AppendLine("    code@eip: " + BitConverter.ToString(code).Replace("-", " "));
                        byte[] stk = new byte[64];
                        if (esp > 0x10000 && ReadProcessMemory(hp, esp, stk, 64, out got) && got == 64)
                            sb.AppendLine("    stack@esp: " + BitConverter.ToString(stk).Replace("-", " "));
                    }
                    CloseHandle(ht);
                } while (Thread32Next(snap, te));
            }
            CloseHandle(snap);
        }
        File.AppendAllText(logPath, sb.ToString());
        Console.WriteLine(sb.ToString());
    }
    public static int Run(int pid, string outPath, string logPath)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Log(logPath, "OpenProcess 失败"); return 1; }
        Log(logPath, "hit-watch start pid=" + pid);
        long[] objs = new long[0];
        int[] prev = new int[0];
        long[] active = new long[0];
        int rounds = 0;
        FileStream fs = new FileStream(outPath, FileMode.Append, FileAccess.Write);
        BinaryWriter bw = new BinaryWriter(fs);
        byte[] frame = new byte[0x400];
        while (true)
        {
            if (WaitForSingleObject(hp, 0) == 0) { Log(logPath, "game 退出，hit-watch 结束"); break; }
            rounds++;
            if (objs.Length == 0 || rounds % 100 == 0)
            {
                List<long> found = FindObj(hp);
                List<long> players = new List<long>();
                foreach (long o in found) if (IsPlayer(hp, o)) players.Add(o);
                objs = players.ToArray();
                prev = new int[objs.Length];
                for (int i = 0; i < objs.Length; i++) ReadLife(hp, objs[i], out prev[i]);
                Log(logPath, "玩家对象 " + objs.Length + " 个");
            }
            if (rounds % 200 == 0)
            {
                active = ActiveScan(hp).ToArray();
                string s = "";
                foreach (long o in active) s += o.ToString("X8") + " ";
                Log(logPath, "活跃块 " + active.Length + " 个: " + s);
            }
            if (rounds % 7 == 0)
            {
                long tick = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                foreach (long o in objs)
                {
                    int got;
                    if (ReadProcessMemory(hp, (uint)o, frame, 0x400, out got) && got == 0x400)
                    {
                        bw.Write(tick);
                        bw.Write((uint)o);
                        bw.Write(frame);
                    }
                }
                foreach (long o in active)
                {
                    int got;
                    if (ReadProcessMemory(hp, (uint)o, frame, 0x400, out got) && got == 0x400)
                    {
                        bw.Write(tick);
                        bw.Write((uint)o);
                        bw.Write(frame);
                    }
                }
                bw.Flush();
            }
            for (int i = 0; i < objs.Length; i++)
            {
                int lv;
                if (!ReadLife(hp, objs[i], out lv)) { objs = new long[0]; break; }
                if (lv != prev[i])
                {
                    Log(logPath, "命数变化 obj=" + objs[i].ToString("X8") + " life " + prev[i] + " -> " + lv);
                    if (lv < prev[i]) DumpRing(hp, pid, objs[i], prev[i], lv, logPath);
                    prev[i] = lv;
                }
            }
            Thread.Sleep(15);
        }
        bw.Close(); fs.Close();
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
while ($true) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { break }
    Start-Sleep -Seconds 2
}
[HitWatch]::Run($p.Id, $Out, $Log)