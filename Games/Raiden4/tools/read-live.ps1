# 纯读：读游戏进程指定地址 64 字节＋区域信息；附完整模块表定位地址归属。
param([string[]]$Addrs = @('6E201000', '6E2A8FBB', '004E29F2'))
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Runtime.InteropServices;
public static class LiveRead
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
    public static void Run(int pid, string[] addrs)
    {
        IntPtr hp = OpenProcess(0x00100410, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess 失败"); return; }
        foreach (string s in addrs)
        {
            uint a = Convert.ToUInt32(s, 16);
            MBI mbi;
            int r = VirtualQueryEx(hp, (IntPtr)a, out mbi, Marshal.SizeOf(typeof(MBI)));
            string info = "addr=" + s + " query=" + r;
            if (r != 0) info += " base=" + mbi.BaseAddress.ToInt64().ToString("X8") + " size=" + mbi.RegionSize.ToInt64().ToString("X8") + " state=" + mbi.State.ToString("X") + " protect=" + mbi.Protect.ToString("X") + " type=" + mbi.Type.ToString("X");
            byte[] b = new byte[64]; int got;
            if (ReadProcessMemory(hp, a, b, 64, out got) && got == 64)
                info += "\n  " + BitConverter.ToString(b).Replace("-", " ");
            else info += " READ_FAIL";
            Console.WriteLine(info);
        }
        CloseHandle(hp);
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output '没找到 game 进程'; exit 1 }
Write-Output ('pid=' + $p.Id)
[LiveRead]::Run($p.Id, $Addrs)
Write-Output '-- modules --'
$p.Modules | ForEach-Object { 'base=' + $_.BaseAddress.ToInt64().ToString('X8') + ' size=' + $_.ModuleMemorySize.ToString('X8') + ' ' + $_.FileName }