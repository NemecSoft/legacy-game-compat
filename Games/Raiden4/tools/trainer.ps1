# 命数锁定 trainer：纯数据写，不碰代码。15ms 轮询玩家特征对象 +0x30，偏离锁定值立即写回。
# 玩家特征：+0x20==0x3FF 或 +0x24==self+0x40。对象池签名：+0x08=0xFFEEFFEE +0x1C=self +0x30=0..9。
param([int]$Lock = 3, [string]$Out = 'D:\Temp\trainer.log', [string]$PidFile = 'D:\YunGame\G\Raiden4\trainer.pid', [switch]$Once)
$ErrorActionPreference = 'Stop'
$created = $false
$mutex = New-Object System.Threading.Mutex($true, 'Raiden4LifeLockTrainer', [ref]$created)
if (-not $created) { Write-Output '已有 trainer 在跑（Mutex 存在），本实例退出'; exit 0 }
Set-Content -Path $PidFile -Value $PID -NoNewline -Encoding ASCII
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class LifeLock
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int written);
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
    static bool WriteLife(IntPtr hp, long obj, int val)
    {
        byte[] b = BitConverter.GetBytes(val); int wr;
        return WriteProcessMemory(hp, (uint)(obj + 0x30), b, 4, out wr) && wr == 4;
    }
    public static int Run(int pid, int lockVal, string outPath, bool once)
    {
        IntPtr hp = OpenProcess(0x00100438, false, pid);
        if (hp == IntPtr.Zero) { Log(outPath, "OpenProcess 失败 pid=" + pid); return 1; }
        Log(outPath, "trainer start pid=" + pid + " lock=" + lockVal);
        long[] objs = new long[0];
        int writes = 0; int rounds = 0;
        while (true)
        {
            if (WaitForSingleObject(hp, 0) == 0) { Log(outPath, "game 进程退出，trainer 结束 writes=" + writes); break; }
            rounds++;
            if (objs.Length == 0 || rounds % 100 == 0)
            {
                List<long> found = FindObj(hp);
                List<long> players = new List<long>();
                foreach (long o in found) if (IsPlayer(hp, o)) players.Add(o);
                objs = players.ToArray();
                StringBuilder ob = new StringBuilder();
                foreach (long o in objs) ob.Append(o.ToString("X8")).Append(' ');
                Log(outPath, "玩家特征对象 " + objs.Length + " 个: " + ob);
                if (once && objs.Length > 0)
                {
                    foreach (long o in objs)
                    {
                        int lv; ReadLife(hp, o, out lv);
                        bool ok = WriteLife(hp, o, lockVal);
                        Log(outPath, "  once write " + o.ToString("X8") + " " + lv + " -> " + lockVal + " ok=" + ok);
                    }
                    CloseHandle(hp);
                    return 0;
                }
            }
            for (int i = 0; i < objs.Length; i++)
            {
                int lv;
                if (!ReadLife(hp, objs[i], out lv)) { objs = new long[0]; break; }
                if (lv != lockVal)
                {
                    bool ok = WriteLife(hp, objs[i], lockVal);
                    writes++;
                    if (writes <= 40 || writes % 200 == 0)
                        Log(outPath, "write " + objs[i].ToString("X8") + " " + lv + " -> " + lockVal + " ok=" + ok + " total=" + writes);
                }
            }
            Thread.Sleep(15);
        }
        CloseHandle(hp);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
try {
    while ($true) {
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { [LifeLock]::Run($p.Id, $Lock, $Out, $Once.IsPresent) | Out-Null }
        if ($Once) { break }
        Start-Sleep -Milliseconds 2000
    }
} finally {
    Remove-Item -Path $PidFile -ErrorAction SilentlyContinue
    try { $mutex.ReleaseMutex() } catch { }
}