# 玩家对象字段高频采样：每 100ms dump 前 0x400 字节带 tick 头，离线找 respawn 无敌计时器（倒计时候选）。
param([int]$Seconds = 90, [string]$Out = 'D:\Temp\field-watch.bin', [string]$Log = 'D:\Temp\field-watch.log')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
public static class FieldWatch
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
    public static int Run(int pid, string outPath, string logPath, int seconds)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { File.AppendAllText(logPath, "OpenProcess 失败\r\n"); return 1; }
        long obj = 0;
        DateTime deadline = DateTime.Now.AddSeconds(seconds);
        using (FileStream fs = File.Create(outPath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            byte[] frame = new byte[0x400];
            int n = 0;
            while (DateTime.Now < deadline)
            {
                if (WaitForSingleObject(hp, 0) == 0) { File.AppendAllText(logPath, "game 退出\r\n"); break; }
                if (obj == 0 || n % 50 == 0)
                {
                    List<long> found = FindObj(hp);
                    long pick = 0;
                    foreach (long o in found) if (IsPlayer(hp, o)) { pick = o; break; }
                    if (pick != 0 && pick != obj)
                    {
                        obj = pick;
                        File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " 锁定玩家对象 " + obj.ToString("X8") + "\r\n");
                    }
                }
                if (obj != 0)
                {
                    int got;
                    if (ReadProcessMemory(hp, (uint)obj, frame, 0x400, out got) && got == 0x400)
                    {
                        bw.Write(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                        bw.Write((uint)obj);
                        bw.Write(frame);
                        n++;
                    }
                    else { obj = 0; }
                }
                Thread.Sleep(50);
            }
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " 采样结束 frames=" + n + "\r\n");
        }
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Start-Process -FilePath 'D:\YunGame\G\Raiden4\game_starter.exe' -ArgumentList '/go' -WorkingDirectory 'D:\YunGame\G\Raiden4'
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { break }
    }
}
if (-not $p) { Write-Output '没有 game 进程'; exit 1 }
[FieldWatch]::Run($p.Id, $Out, $Log, $Seconds)