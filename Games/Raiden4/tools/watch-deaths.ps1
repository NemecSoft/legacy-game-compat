# 死亡流程纯读记录器：签名扫玩家对象 -> 环形采样各线程 EIP/ESP -> 命数变化瞬间 dump 死亡调用链。
# 不写内存、不挂调试器、不发键、不抢前台。game 进程不在时自动等待，进程退出后等下一轮。
param([string]$Out = 'D:\Temp\death-watch.log', [switch]$SelfTest, [switch]$Probe)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class DeathWatch
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
    public struct Frame
    {
        public long tick; public int[] lives; public int[] tids; public uint[] eips; public uint[] esps;
    }
    static void Log(string path, string line)
    {
        string s = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
        File.AppendAllText(path, s + "\r\n");
        Console.WriteLine(s);
    }
    // 玩家对象签名：+0x1C=self +0x20=0x3FF +0x24=self+0x40 +0x2C=14 +0x30=命数(0..9) +0x34=0
    public static List<long> FindObj(IntPtr hp) { return FindCore(hp, false, null); }
    public static List<long[]> FindLoose(IntPtr hp)
    {
        List<long[]> fs = new List<long[]>();
        FindCore(hp, true, fs);
        return fs;
    }
    static List<long> FindCore(IntPtr hp, bool loose, List<long[]> fieldsOut)
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
                            if (loose)
                            {
                                if (lv < 0 || lv > 99) continue;
                                if (fieldsOut != null && fieldsOut.Count < 12)
                                    fieldsOut.Add(new long[] { baseA,
                                        BitConverter.ToUInt32(buf, i), BitConverter.ToUInt32(buf, i + 4),
                                        BitConverter.ToUInt32(buf, i + 0x20), BitConverter.ToUInt32(buf, i + 0x24),
                                        BitConverter.ToUInt32(buf, i + 0x2C), lv,
                                        BitConverter.ToUInt32(buf, i + 0x34), BitConverter.ToUInt32(buf, i + 0x38) });
                                continue;
                            }
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
    public static Frame Sample(IntPtr hp, int pid, long[] objs)
    {
        Frame f = new Frame();
        f.tick = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        f.lives = new int[objs.Length];
        for (int i = 0; i < objs.Length; i++) ReadLife(hp, objs[i], out f.lives[i]);
        List<int> tids = new List<int>(); List<uint> eips = new List<uint>(); List<uint> esps = new List<uint>();
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
                        tids.Add(tid);
                        eips.Add(BitConverter.ToUInt32(ctx, 184));
                        esps.Add(BitConverter.ToUInt32(ctx, 196));
                    }
                    CloseHandle(ht);
                } while (Thread32Next(snap, te));
            }
            CloseHandle(snap);
        }
        f.tids = tids.ToArray(); f.eips = eips.ToArray(); f.esps = esps.ToArray();
        return f;
    }
    static void Dump(string path, IntPtr hp, int pid, long[] objs, int[] oldL, int[] newL, List<Frame> ring)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("==== 命数变化 " + DateTime.Now.ToString("HH:mm:ss.fff") + " ====");
        for (int i = 0; i < objs.Length; i++)
            sb.AppendLine("obj=" + objs[i].ToString("X8") + " life " + (i < oldL.Length ? oldL[i].ToString() : "?") + " -> " + newL[i]);
        byte[] db40 = new byte[0x40]; int dg;
        foreach (long o in objs)
            if (ReadProcessMemory(hp, (uint)o, db40, 0x40, out dg))
                sb.AppendLine("  fields@" + o.ToString("X8") + ": " + BitConverter.ToString(db40, 0, dg).Replace("-", " "));
        sb.AppendLine("-- 环形缓冲（变化前约600ms，每帧各线程 EIP/ESP）--");
        foreach (Frame f in ring)
        {
            StringBuilder lb = new StringBuilder();
            for (int i = 0; i < f.lives.Length; i++) lb.Append(f.lives[i]).Append(',');
            sb.AppendLine("t=" + f.tick + " lives=" + lb);
            for (int i = 0; i < f.tids.Length; i++)
                sb.AppendLine("  tid=" + f.tids[i] + " eip=" + f.eips[i].ToString("X8") + " esp=" + f.esps[i].ToString("X8"));
        }
        sb.AppendLine("-- 变化瞬间各线程栈内 .text 返回地址候选 --");
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
                        sb.AppendLine("tid=" + tid + " eip=" + eip.ToString("X8") + " esp=" + esp.ToString("X8"));
                        byte[] stack = new byte[1024]; int got;
                        if (ReadProcessMemory(hp, esp, stack, 1024, out got))
                        {
                            for (int i = 0; i + 4 <= got; i += 4)
                            {
                                uint v = BitConverter.ToUInt32(stack, i);
                                if ((v >= 0x401000 && v < 0x4f0000) || (v >= 0x60000000 && v < 0x70000000))
                                    sb.AppendLine("  +" + i.ToString("X3") + " ret?=" + v.ToString("X8"));
                            }
                        }
                    }
                    CloseHandle(ht);
                } while (Thread32Next(snap, te));
            }
            CloseHandle(snap);
        }
        File.AppendAllText(path, sb.ToString());
        Console.WriteLine(sb.ToString());
    }
    public static int Run(int pid, string outPath)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Log(outPath, "OpenProcess 失败 pid=" + pid); return 1; }
        Log(outPath, "watch start pid=" + pid);
        long[] objs = new long[0];
        int[] prev = new int[0];
        int noHit = 0;
        int frameCount = 0;
        List<Frame> ring = new List<Frame>();
        while (true)
        {
            if (WaitForSingleObject(hp, 0) == 0)
            {
                if (ring.Count > 0 && objs.Length > 0)
                {
                    int[] dl = new int[objs.Length];
                    for (int i = 0; i < objs.Length; i++) dl[i] = -1;
                    Log(outPath, "进程退出，补 dump 退出前环形缓冲:");
                    Dump(outPath, hp, pid, objs, prev, dl, ring);
                }
                Log(outPath, "game 进程退出，watch 结束"); break;
            }
            if (objs.Length == 0)
            {
                List<long> found = FindObj(hp);
                if (found.Count == 0)
                {
                    List<long[]> ls0 = FindLoose(hp);
                    if (ls0.Count > 0)
                    {
                        found = new List<long>();
                        foreach (long[] ff0 in ls0) found.Add(ff0[0]);
                        Log(outPath, "严格签名无命中，改用宽松自引用候选监控 " + found.Count + " 个对象");
                    }
                }
                if (found.Count == 0)
                {
                    noHit++;
                    if (noHit % 30 == 0)
                    {
                        Log(outPath, "尚无严格签名命中，已扫 " + noHit + " 轮；宽松自引用候选:");
                        List<long[]> ls = FindLoose(hp);
                        foreach (long[] ff in ls)
                            Log(outPath, "  obj=" + ff[0].ToString("X8") + " f00=" + ff[1].ToString("X8") + " f04=" + ff[2].ToString("X8") + " f20=" + ff[3].ToString("X8") + " f24=" + ff[4].ToString("X8") + " f2C=" + ff[5].ToString("X8") + " f30=" + ff[6] + " f34=" + ff[7].ToString("X8") + " f38=" + ff[8].ToString("X8"));
                        if (ls.Count == 0) Log(outPath, "  （无任何自引用候选）");
                    }
                    Thread.Sleep(1000); continue;
                }
                objs = found.ToArray();
                prev = new int[objs.Length];
                for (int i = 0; i < objs.Length; i++) ReadLife(hp, objs[i], out prev[i]);
                StringBuilder ob = new StringBuilder();
                foreach (long o in objs) ob.Append(o.ToString("X8")).Append(' ');
                Log(outPath, "签名命中玩家对象: " + ob + " lives=" + string.Join(",", Array.ConvertAll(prev, delegate(int x) { return x.ToString(); })));
                byte[] ob40 = new byte[0x40]; int og;
                foreach (long o in objs)
                    if (ReadProcessMemory(hp, (uint)o, ob40, 0x40, out og))
                        Log(outPath, "  fields@" + o.ToString("X8") + ": " + BitConverter.ToString(ob40, 0, og).Replace("-", " "));
                ring.Clear();
            }
            frameCount++;
            if (frameCount % 200 == 0)
            {
                List<long> re2 = FindObj(hp);
                List<long> merged = new List<long>(objs);
                List<int> mp = new List<int>(prev);
                foreach (long o2 in re2)
                {
                    bool exist = false;
                    foreach (long o3 in objs) if (o3 == o2) exist = true;
                    if (!exist)
                    {
                        int lv2; ReadLife(hp, o2, out lv2);
                        merged.Add(o2); mp.Add(lv2);
                        Log(outPath, "新对象入监控: " + o2.ToString("X8") + " life=" + lv2);
                    }
                }
                objs = merged.ToArray(); prev = mp.ToArray();
            }
            Frame f = Sample(hp, pid, objs);
            bool bad = false, changed = false;
            for (int i = 0; i < objs.Length; i++)
            {
                if (f.lives[i] < 0) { bad = true; break; }
                if (f.lives[i] != prev[i]) changed = true;
            }
            if (bad)
            {
                List<long> re = FindObj(hp);
                if (ring.Count > 0 && prev.Length > 0)
                {
                    long[] nobjs = re.Count > 0 ? re.ToArray() : objs;
                    int[] nl = new int[nobjs.Length];
                    for (int i = 0; i < nobjs.Length; i++) ReadLife(hp, nobjs[i], out nl[i]);
                    Log(outPath, re.Count > 0 ? "对象释放后重建，补记变化:" : "对象全释放(场景清空)，补 dump 死前环形缓冲:");
                    Dump(outPath, hp, pid, nobjs, prev, nl, ring);
                    if (re.Count > 0) { objs = nobjs; prev = nl; ring.Clear(); continue; }
                }
                objs = new long[0]; ring.Clear(); continue;
            }
            ring.Add(f);
            if (ring.Count > 40) ring.RemoveAt(0);
            if (changed)
            {
                bool isPlayer = false; bool decr = false;
                for (int i = 0; i < objs.Length; i++)
                    if (f.lives[i] != prev[i])
                    {
                        if (IsPlayer(hp, objs[i])) isPlayer = true;
                        if (f.lives[i] < prev[i]) decr = true;
                    }
                if (isPlayer || decr)
                {
                    Dump(outPath, hp, pid, objs, prev, f.lives, ring);
                }
                else
                {
                    StringBuilder nb = new StringBuilder();
                    for (int i = 0; i < objs.Length; i++)
                        if (f.lives[i] != prev[i]) nb.Append(objs[i].ToString("X8")).Append(':').Append(prev[i]).Append("->").Append(f.lives[i]).Append(' ');
                    Log(outPath, "非玩家特征对象变化(不dump): " + nb);
                }
                prev = f.lives;
            }
            Thread.Sleep(15);
        }
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if ($SelfTest) {
    $p = $null
    for ($i = 0; $i -lt 45; $i++) {
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { break }
        Start-Sleep -Seconds 2
    }
    if (-not $p) { Write-Output 'selftest: 没等到 game 进程'; exit 1 }
    $h = [DeathWatch]::OpenProcess(0x00100410, $false, $p.Id)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $hits = [DeathWatch]::FindObj($h)
    $sw.Stop()
    Write-Output ("selftest: pid=" + $p.Id + " scan_ms=" + $sw.ElapsedMilliseconds + " hits=" + $hits.Count)
    foreach ($x in $hits) { Write-Output ("  obj=" + $x.ToString('X8')) }
    [DeathWatch]::CloseHandle($h) | Out-Null
    exit 0
}
if ($Probe) {
    $p = $null
    for ($i = 0; $i -lt 45; $i++) {
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { break }
        Start-Sleep -Seconds 2
    }
    if (-not $p) { Write-Output 'probe: 没等到 game 进程'; exit 1 }
    $h = [DeathWatch]::OpenProcess(0x00100410, $false, $p.Id)
    $fr = [DeathWatch]::Sample($h, $p.Id, @())
    Write-Output ("probe: pid=" + $p.Id + " threads=" + $fr.tids.Length)
    for ($i = 0; $i -lt $fr.tids.Length; $i++) {
        Write-Output ("  tid=" + $fr.tids[$i] + " eip=" + $fr.eips[$i].ToString('X8') + " esp=" + $fr.esps[$i].ToString('X8'))
    }
    [DeathWatch]::CloseHandle($h) | Out-Null
    exit 0
}
while ($true) {
    $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { [DeathWatch]::Run($p.Id, $Out) | Out-Null }
    Start-Sleep -Milliseconds 2000
}