# 一次性写玩家 struct 偏移：找玩家特征对象(+0x20==0x3FF 或 +0x24==self+0x40)，写指定偏移值，读回验证。
# 用途：测 +0x20 碰撞掩码清零是否=真无敌（敌弹不判定）。纯数据写，不碰代码。
param([int]$Offset = 0x20, [int]$Value = 0, [string]$Out = 'D:\Temp\god-write.log', [switch]$Dump)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class GodWrite
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int written);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
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
    public static int Run(int pid, int offset, int value, string outPath, bool dump)
    {
        IntPtr hp = OpenProcess(0x00100438, false, pid);
        if (hp == IntPtr.Zero) { Log(outPath, "OpenProcess 失败 pid=" + pid); return 1; }
        List<long> found = FindObj(hp);
        List<long> players = new List<long>();
        foreach (long o in found) if (IsPlayer(hp, o)) players.Add(o);
        Log(outPath, "玩家对象 " + players.Count + " 个");
        foreach (long o in players)
        {
            if (dump)
            {
                byte[] b = new byte[0x100]; int got;
                ReadProcessMemory(hp, (uint)o, b, 0x100, out got);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 0x100; i += 16)
                {
                    sb.Append("+" + i.ToString("X3") + ":");
                    for (int j = 0; j < 16; j++) sb.Append(" " + b[i + j].ToString("X2"));
                    sb.Append("\r\n");
                }
                Log(outPath, "obj=" + o.ToString("X8") + "\r\n" + sb);
            }
            else
            {
                uint before = ReadDword(hp, o + offset);
                byte[] wb = BitConverter.GetBytes(value); int wr;
                bool ok = WriteProcessMemory(hp, (uint)(o + offset), wb, 4, out wr) && wr == 4;
                uint after = ReadDword(hp, o + offset);
                Log(outPath, "obj=" + o.ToString("X8") + " +" + offset.ToString("X") + " " + before.ToString("X8") + " -> " + value.ToString("X8") + " ok=" + ok + " readback=" + after.ToString("X8") + " life=" + ReadDword(hp, o + 0x30).ToString("X"));
            }
        }
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
[GodWrite]::Run($p.Id, $Offset, $Value, $Out, $Dump.IsPresent) | Out-Null