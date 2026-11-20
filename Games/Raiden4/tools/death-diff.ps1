# 死亡调用链差分记录器：1ms 轮询命数，扣减瞬间全栈扫 ret，与平时 3 秒频率基线差分，输出稀有 ret＝死亡专属调用链。
# 纯读：不写内存、不挂调试器、不发键、不抢前台。
param([string]$Out = 'D:\Temp\death-diff.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class DeathDiff
{
    [DllImport("kernel32.dll")] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll")] public static extern bool Thread32First(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern bool Thread32Next(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenThread(uint acc, bool inh, int tid);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool Wow64GetThreadContext(IntPtr h, byte[] ctx);
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
    static bool IsRet(uint v)
    {
        return (v >= 0x401000 && v < 0x4F0000) || (v >= 0x60CB1000 && v < 0x61318000) || (v >= 0x61691000 && v < 0x616C5000);
    }
    public static List<long> FindObj(IntPtr hp)
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
    static bool ReadLife(IntPtr hp, long obj, out int life)
    {
        byte[] b = new byte[4]; int got;
        life = -1;
        if (!ReadProcessMemory(hp, (uint)(obj + 0x30), b, 4, out got) || got != 4) return false;
        life = BitConverter.ToInt32(b, 0);
        return true;
    }
    struct ThreadStack { public int tid; public uint esp; public List<KeyValuePair<uint, int>> rets; }
    static List<ThreadStack> ScanStacks(IntPtr hp, int pid, bool mainOnly)
    {
        List<ThreadStack> outL = new List<ThreadStack>();
        IntPtr snap = CreateToolhelp32Snapshot(4, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return outL;
        byte[] te = new byte[28];
        BitConverter.GetBytes((uint)28).CopyTo(te, 0);
        List<int> tids = new List<int>(); List<uint> esps = new List<uint>();
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
                    tids.Add(tid);
                    esps.Add(BitConverter.ToUInt32(ctx, 196));
                }
                CloseHandle(ht);
            } while (Thread32Next(snap, te));
        }
        CloseHandle(snap);
        uint minEsp = 0xFFFFFFFF; int mainIdx = -1;
        for (int i = 0; i < esps.Count; i++) if (esps[i] < minEsp) { minEsp = esps[i]; mainIdx = i; }
        for (int i = 0; i < tids.Count; i++)
        {
            if (mainOnly && i != mainIdx) continue;
            uint esp = esps[i];
            if (esp < 0x10000 || esp > 0x7FFF0000) continue;
            MBI mbi;
            if (VirtualQueryEx(hp, (IntPtr)esp, out mbi, Marshal.SizeOf(typeof(MBI))) == 0) continue;
            long end = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
            int len = (int)Math.Min(end - esp, 0x20000);
            if (len <= 0) continue;
            byte[] stack = new byte[len]; int got;
            if (!ReadProcessMemory(hp, esp, stack, len, out got)) continue;
            ThreadStack ts = new ThreadStack();
            ts.tid = tids[i]; ts.esp = esp; ts.rets = new List<KeyValuePair<uint, int>>();
            for (int o = 0; o + 4 <= got; o += 4)
            {
                uint v = BitConverter.ToUInt32(stack, o);
                if (IsRet(v)) ts.rets.Add(new KeyValuePair<uint, int>(v, o));
            }
            outL.Add(ts);
        }
        return outL;
    }
    public static int Run(int pid, string outPath)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Log(outPath, "OpenProcess 失败 pid=" + pid); return 1; }
        Log(outPath, "death-diff start pid=" + pid);
        long[] objs = new long[0];
        int[] prev = new int[0];
        List<Dictionary<uint, int>> ring = new List<Dictionary<uint, int>>();
        int frame = 0;
        while (true)
        {
            if (WaitForSingleObject(hp, 0) == 0) { Log(outPath, "game 进程退出，death-diff 结束"); break; }
            if (objs.Length == 0)
            {
                List<long> found = FindObj(hp);
                if (found.Count == 0) { Thread.Sleep(500); continue; }
                objs = found.ToArray();
                prev = new int[objs.Length];
                for (int i = 0; i < objs.Length; i++) ReadLife(hp, objs[i], out prev[i]);
                StringBuilder ob = new StringBuilder();
                for (int i = 0; i < objs.Length; i++) ob.Append(objs[i].ToString("X8")).Append(':').Append(prev[i]).Append(' ');
                Log(outPath, "监控对象: " + ob);
                ring.Clear();
            }
            frame++;
            int[] cur = new int[objs.Length];
            bool bad = false, decr = false;
            for (int i = 0; i < objs.Length; i++)
            {
                if (!ReadLife(hp, objs[i], out cur[i])) { bad = true; break; }
                if (cur[i] < prev[i]) decr = true;
            }
            if (bad) { objs = new long[0]; ring.Clear(); continue; }
            if (decr)
            {
                List<ThreadStack> now = ScanStacks(hp, pid, false);
                Dictionary<uint, int> baseCnt = new Dictionary<uint, int>();
                foreach (Dictionary<uint, int> d in ring)
                    foreach (KeyValuePair<uint, int> kv in d)
                        baseCnt[kv.Key] = (baseCnt.ContainsKey(kv.Key) ? baseCnt[kv.Key] : 0) + kv.Value;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("==== 命数扣减 " + DateTime.Now.ToString("HH:mm:ss.fff") + " ====");
                for (int i = 0; i < objs.Length; i++)
                    if (cur[i] != prev[i]) sb.AppendLine("obj=" + objs[i].ToString("X8") + " life " + prev[i] + " -> " + cur[i]);
                byte[] f40 = new byte[0x40]; int fg;
                for (int i = 0; i < objs.Length; i++)
                    if (cur[i] != prev[i] && ReadProcessMemory(hp, (uint)objs[i], f40, 0x40, out fg))
                        sb.AppendLine("  fields: " + BitConverter.ToString(f40, 0, fg).Replace("-", " "));
                foreach (ThreadStack ts in now)
                {
                    sb.AppendLine("-- tid=" + ts.tid + " esp=" + ts.esp.ToString("X8") + " 栈ret数=" + ts.rets.Count);
                    List<KeyValuePair<uint, int>> rare = new List<KeyValuePair<uint, int>>();
                    foreach (KeyValuePair<uint, int> kv in ts.rets)
                    {
                        int bc = baseCnt.ContainsKey(kv.Key) ? baseCnt[kv.Key] : 0;
                        if (bc < 3) rare.Add(kv);
                    }
                    sb.AppendLine("  稀有ret(基线<3): ");
                    foreach (KeyValuePair<uint, int> kv in rare)
                    {
                        int bc = baseCnt.ContainsKey(kv.Key) ? baseCnt[kv.Key] : 0;
                        sb.AppendLine("    +" + kv.Value.ToString("X4") + " ret=" + kv.Key.ToString("X8") + " base=" + bc);
                    }
                    if (ts.rets.Count > 0 && ts.esp == minEspOf(now))
                    {
                        sb.AppendLine("  主线程全栈ret序: ");
                        StringBuilder seq = new StringBuilder();
                        foreach (KeyValuePair<uint, int> kv in ts.rets) seq.Append(kv.Key.ToString("X8")).Append(' ');
                        sb.AppendLine("    " + seq);
                    }
                }
                File.AppendAllText(outPath, sb.ToString());
                Console.WriteLine(sb.ToString());
                prev = cur;
                ring.Clear();
                continue;
            }
            prev = cur;
            if (frame % 40 == 0)
            {
                List<ThreadStack> ms = ScanStacks(hp, pid, true);
                Dictionary<uint, int> d = new Dictionary<uint, int>();
                foreach (ThreadStack ts in ms)
                    foreach (KeyValuePair<uint, int> kv in ts.rets)
                        d[kv.Key] = (d.ContainsKey(kv.Key) ? d[kv.Key] : 0) + 1;
                ring.Add(d);
                if (ring.Count > 75) ring.RemoveAt(0);
            }
            Thread.Sleep(1);
        }
        CloseHandle(hp);
        return 0;
    }
    static uint minEspOf(List<ThreadStack> l)
    {
        uint m = 0xFFFFFFFF;
        foreach (ThreadStack t in l) if (t.esp < m) m = t.esp;
        return m;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
while ($true) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { [DeathDiff]::Run($p.Id, $Out) | Out-Null }
    Start-Sleep -Milliseconds 2000
}