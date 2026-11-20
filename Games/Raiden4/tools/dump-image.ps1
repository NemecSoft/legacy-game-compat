# 纯读 dump game.exe 映像内全部可执行区（运行时解密代码候选），带熵/head 清单。不写游戏内存。
param([string]$OutDir = 'D:\Temp\code-image')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class ImgDump
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
    static double Entropy(byte[] b, int n)
    {
        int[] freq = new int[256];
        for (int i = 0; i < n; i++) freq[b[i]]++;
        double e = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = (double)freq[i] / n;
            e -= p * Math.Log(p, 2);
        }
        return e;
    }
    public static int Run(int pid, long modBase, long modSize, string outDir)
    {
        Directory.CreateDirectory(outDir);
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess 失败"); return 1; }
        string manifest = Path.Combine(outDir, "manifest.txt");
        long addr = modBase;
        long end = modBase + modSize;
        int dumped = 0;
        while (addr < end)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            bool exec = (mbi.Protect == 0x10 || mbi.Protect == 0x20 || mbi.Protect == 0x40 || mbi.Protect == 0x80);
            if (mbi.State == 0x1000 && exec && mbi.Type == 0x1000000)
            {
                byte[] buf = new byte[size];
                int got;
                string line = "base=" + addr.ToString("X8") + " size=" + size.ToString("X8") + " protect=" + mbi.Protect.ToString("X2");
                if (ReadProcessMemory(hp, (uint)addr, buf, (int)size, out got) && got == size)
                {
                    File.WriteAllBytes(Path.Combine(outDir, addr.ToString("X8") + ".bin"), buf);
                    line += " entropy=" + Entropy(buf, got).ToString("F3") + " head=" + BitConverter.ToString(buf, 0, 32).Replace("-", " ") + " DUMPED";
                    dumped++;
                }
                else line += " READ_FAIL";
                File.AppendAllText(manifest, line + "\r\n");
                Console.WriteLine(line);
            }
            addr += size;
        }
        CloseHandle(hp);
        Console.WriteLine("dumped=" + dumped);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Start-Process -FilePath 'D:\YunGame\G\Raiden4\game_starter.exe' -ArgumentList '/go'
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p) { break }
    }
}
if (-not $p) { Write-Output '没等到 game 进程'; exit 1 }
$mod = $p.Modules | Where-Object { $_.FileName -match 'game\.exe$' } | Select-Object -First 1
[ImgDump]::Run($p.Id, $mod.BaseAddress.ToInt64(), $mod.ModuleMemorySize, $OutDir) | Out-Null