# 零配合等标题屏演示进战斗：轮询读 ring EIP 处字节，非零=实战代码已解包，立即纯读 dump 整个区域。
param([int]$Minutes = 6, [string]$Out = 'D:\Temp\raiden-mem\642DF000.combat.bin')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class DemoWait
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [StructLayout(LayoutKind.Sequential)] public struct MBI
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    static bool NonZero(IntPtr hp, uint addr)
    {
        byte[] b = new byte[64]; int got;
        if (!ReadProcessMemory(hp, addr, b, 64, out got) || got < 16) return false;
        for (int i = 0; i < got; i++) if (b[i] != 0) return true;
        return false;
    }
    public static int Run(int pid, long tpBase, string outPath, int minutes)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess 失败"); return 1; }
        uint probe1 = (uint)(tpBase + 0x42F1098);
        uint probe2 = (uint)(tpBase + 0x439A8F1);
        uint region = (uint)(tpBase + 0x362F000);
        DateTime deadline = DateTime.Now.AddMinutes(minutes);
        int n = 0;
        while (DateTime.Now < deadline)
        {
            n++;
            if (WaitForSingleObject(hp, 0) == 0)
            {
                Console.WriteLine("game 进程已退出（标题屏空闲自退），本轮结束");
                CloseHandle(hp);
                return 3;
            }
            if (NonZero(hp, probe1) || NonZero(hp, probe2))
            {
                MBI mbi;
                int r = VirtualQueryEx(hp, (IntPtr)region, out mbi, Marshal.SizeOf(typeof(MBI)));
                long size = r != 0 ? mbi.RegionSize.ToInt64() : 0x10B6000;
                if (size <= 0 || size > (1 << 27)) size = 0x10B6000;
                Console.WriteLine("演示已进战斗(第" + n + "轮探测)，dump 区域 " + region.ToString("X8") + " size=" + size);
                using (FileStream fs = File.Create(outPath))
                {
                    byte[] buf = new byte[1 << 20];
                    long done = 0;
                    while (done < size)
                    {
                        int chunk = (int)Math.Min(buf.Length, size - done);
                        int got;
                        if (!ReadProcessMemory(hp, (uint)(region + done), buf, chunk, out got) || got <= 0) break;
                        fs.Write(buf, 0, got);
                        done += got;
                    }
                    Console.WriteLine("dump 完成 " + done + " 字节 -> " + outPath);
                }
                CloseHandle(hp);
                return 0;
            }
            if (n % 30 == 0) Console.WriteLine("已等 " + (n * 2) + " 秒，解包区仍全零");
            System.Threading.Thread.Sleep(2000);
        }
        Console.WriteLine("超时：演示未进战斗（或标题屏无演示），需用户进战斗后手动 dump");
        CloseHandle(hp);
        return 2;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Write-Output '没有 game 进程，自起测试链 /go'
    Start-Process -FilePath 'D:\YunGame\G\Raiden4\game_starter.exe' -ArgumentList '/go' -WorkingDirectory 'D:\YunGame\G\Raiden4'
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { break }
    }
}
if (-not $p) { Write-Output '自起后仍没有 game 进程'; exit 1 }
$mod = $null
for ($j = 0; $j -lt 30; $j++) {
    $p.Refresh()
    $mod = $p.Modules | Where-Object { $_.ModuleName -eq 'TeknoParrot.dll' } | Select-Object -First 1
    if ($mod) { break }
    Start-Sleep -Seconds 1
}
if (-not $mod) { Write-Output '等了30秒仍没找到 TeknoParrot.dll 模块'; exit 1 }
$base = $mod.BaseAddress.ToInt64()
Write-Output ("game pid=" + $p.Id + " TP base=" + $base.ToString('X8'))
[DemoWait]::Run($p.Id, $base, $Out, $Minutes)