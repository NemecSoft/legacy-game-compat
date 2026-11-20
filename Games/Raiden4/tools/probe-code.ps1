# 纯读探针：列 game 进程全部可执行内存区域（base/size/protect/type），并查指定 EIP 的归属属性。
param([string]$Eips = '60DE07DA,64FA1098,6504A8F1,65249ADE,60EECCC2')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class CodeProbe
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, int len);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct MBI
    {
        public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
        public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
    }
    public static List<string> Run(int pid, uint[] eips)
    {
        List<string> outp = new List<string>();
        IntPtr hp = OpenProcess(0x0410, false, pid);
        if (hp == IntPtr.Zero) { outp.Add("OpenProcess 失败"); return outp; }
        long addr = 0;
        while (true)
        {
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)addr, out mbi, Marshal.SizeOf(typeof(MBI)));
            if (r == 0) break;
            long size = mbi.RegionSize.ToInt64();
            if (size <= 0) break;
            uint pr = mbi.Protect;
            bool exec = (pr == 0x10 || pr == 0x20 || pr == 0x40 || pr == 0x80);
            if (mbi.State == 0x1000 && exec)
                outp.Add("region base=" + mbi.BaseAddress.ToInt64().ToString("X8")
                    + " alloc=" + mbi.AllocationBase.ToInt64().ToString("X8")
                    + " size=" + size.ToString("X8")
                    + " protect=" + pr.ToString("X2")
                    + " type=" + mbi.Type.ToString("X8"));
            foreach (uint e in eips)
                if (e >= (uint)mbi.BaseAddress.ToInt64() && e < (uint)(mbi.BaseAddress.ToInt64() + size))
                    outp.Add("EIP " + e.ToString("X8") + " -> base=" + mbi.BaseAddress.ToInt64().ToString("X8")
                        + " alloc=" + mbi.AllocationBase.ToInt64().ToString("X8")
                        + " protect=" + pr.ToString("X2") + " type=" + mbi.Type.ToString("X8"));
            addr += size;
            if (addr <= 0 || addr > 0x7FFF0000L) break;
        }
        CloseHandle(hp);
        return outp;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'probe: 没有 game 进程'; exit 1 }
$es = @()
foreach ($s in $Eips.Split(',')) { $es += [Convert]::ToUInt32($s, 16) }
foreach ($line in [CodeProbe]::Run($p.Id, $es)) { Write-Output $line }