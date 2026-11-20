# 命数差分扫描（CE 式纯读）：战斗中全内存扫 dword==3 存候选；按死亡时间窗逐轮收窄 2/1/0；终轮 dump 候选邻域。
# -Auto 假设启动时已在战斗：T0 扫3 -> +200s 扫2 -> +300s 扫1 -> +400s 扫0。每轮空集则保留上轮并告警。
param([switch]$Auto, [int]$Phase = 0, [int]$Value = 3, [string]$Cand = 'D:\Temp\life-cands.bin', [int]$WaitSec = 0)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
public static class LifeScan
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
    static void Log(string s) { string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + s; File.AppendAllText("D:\\Temp\\scan-life.log", line + "\r\n"); Console.WriteLine(line); }
    public static List<uint> ScanAll(IntPtr hp, int val)
    {
        List<uint> hits = new List<uint>();
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
            if (mbi.State == 0x1000 && writable && size <= (1 << 28))
            {
                long done = 0;
                while (done < size)
                {
                    int chunk = (int)Math.Min(buf.Length, size - done);
                    int got;
                    if (ReadProcessMemory(hp, (uint)(addr + done), buf, chunk, out got) && got > 0)
                        for (int i = 0; i + 4 <= got; i += 4)
                            if (BitConverter.ToInt32(buf, i) == val) hits.Add((uint)(addr + done + i));
                    done += chunk;
                }
            }
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        return hits;
    }
    public static List<uint> Narrow(IntPtr hp, List<uint> cands, int val)
    {
        List<uint> keep = new List<uint>();
        byte[] b = new byte[4]; int got;
        foreach (uint a in cands)
            if (ReadProcessMemory(hp, a, b, 4, out got) && got == 4 && BitConverter.ToInt32(b, 0) == val) keep.Add(a);
        return keep;
    }
    public static List<uint> Load(string path)
    {
        List<uint> l = new List<uint>();
        if (!File.Exists(path)) return l;
        byte[] b = File.ReadAllBytes(path);
        for (int i = 0; i + 4 <= b.Length; i += 4) l.Add(BitConverter.ToUInt32(b, i));
        return l;
    }
    public static void Save(string path, List<uint> l)
    {
        byte[] b = new byte[l.Count * 4];
        for (int i = 0; i < l.Count; i++) BitConverter.GetBytes(l[i]).CopyTo(b, i * 4);
        File.WriteAllBytes(path, b);
    }
    public static void DumpAround(IntPtr hp, List<uint> l, int max)
    {
        byte[] b = new byte[0x80]; int got;
        int n = 0;
        foreach (uint a in l)
        {
            if (n++ >= max) break;
            uint start = a >= 0x40 ? a - 0x40 : 0;
            if (ReadProcessMemory(hp, start, b, 0x80, out got))
                Log("around " + a.ToString("X8") + " (base-40): " + BitConverter.ToString(b, 0, got).Replace("-", " "));
        }
    }
    public static int RunAuto(string candPath)
    {
        System.Diagnostics.Process[] ps = System.Diagnostics.Process.GetProcessesByName("game");
        if (ps.Length == 0) { Log("auto: 没 game 进程"); return 1; }
        int pid = ps[0].Id;
        IntPtr hp = OpenProcess(0x0410, false, pid);
        if (hp == IntPtr.Zero) { Log("auto: OpenProcess 失败"); return 1; }
        Log("auto start pid=" + pid + " T0 扫值3");
        List<uint> c = ScanAll(hp, 3);
        Save(candPath, c);
        Log("T0 候选(==3): " + c.Count);
        int[] vals = new int[] { 2, 1, 0 };
        int[] waits = new int[] { 200, 100, 100 };
        for (int k = 0; k < 3; k++)
        {
            System.Threading.Thread.Sleep(waits[k] * 1000);
            if (System.Diagnostics.Process.GetProcessesByName("game").Length == 0) { Log("auto: game 已退出，停止"); break; }
            List<uint> n = Narrow(hp, c, vals[k]);
            Log("T+" + (k == 0 ? 200 : (k == 1 ? 300 : 400)) + "s 扫值" + vals[k] + ": " + n.Count + " (上轮 " + c.Count + ")");
            if (n.Count == 0) { Log("告警: 空集，保留上轮候选（死亡时间窗可能错位）"); continue; }
            c = n; Save(candPath, c);
            if (c.Count <= 200) DumpAround(hp, c, 40);
        }
        Log("最终候选: " + c.Count);
        foreach (uint a in c) Log("  cand=" + a.ToString("X8"));
        if (c.Count <= 200) DumpAround(hp, c, 60);
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if ($Auto) { exit [LifeScan]::RunAuto($Cand) }
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
$hp = [LifeScan]::OpenProcess(0x0410, $false, $p.Id)
if ($Phase -eq 1) {
    $c = [LifeScan]::ScanAll($hp, $Value)
    [LifeScan]::Save($Cand, $c)
    "phase1 候选(==$Value): " + $c.Count
} else {
    $c = [LifeScan]::Load($Cand)
    $n = [LifeScan]::Narrow($hp, $c, $Value)
    "phase$Phase 上轮 $($c.Count) -> 值$Value 命中 $($n.Count)"
    if ($n.Count -gt 0) { [LifeScan]::Save($Cand, $n); $c = $n }
    if ($c.Count -le 200) { [LifeScan]::DumpAround($hp, $c, 40) }
    foreach ($a in $c) { '  cand=' + $a.ToString('X8') }
}
[LifeScan]::CloseHandle($hp) | Out-Null