# 运行时内存补丁工具：改保护→写字节→读回验证→恢复保护。先打印原字节供回滚。不碰磁盘文件。
param([int]$TargetPid = 0, [string]$Addr = '', [string]$Bytes = '', [switch]$ReadOnly)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Runtime.InteropServices;
public static class PatchLive
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int written);
    [DllImport("kernel32.dll")] public static extern bool VirtualProtectEx(IntPtr h, uint addr, int n, uint newProt, out uint oldProt);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static string Run(int pid, uint addr, byte[] nb, bool readOnly)
    {
        IntPtr hp = OpenProcess(0x043A, false, pid);
        if (hp == IntPtr.Zero) return "OpenProcess 失败 pid=" + pid;
        byte[] ob = new byte[nb.Length]; int got;
        if (!ReadProcessMemory(hp, addr, ob, nb.Length, out got) || got != nb.Length) { CloseHandle(hp); return "读原字节失败 addr=" + addr.ToString("X8"); }
        string orig = BitConverter.ToString(ob).Replace("-", " ");
        if (readOnly) { CloseHandle(hp); return "addr=" + addr.ToString("X8") + " orig=" + orig; }
        uint old;
        if (!VirtualProtectEx(hp, addr, nb.Length, 0x40, out old)) { CloseHandle(hp); return "VirtualProtectEx 失败"; }
        int wr;
        bool ok = WriteProcessMemory(hp, addr, nb, nb.Length, out wr) && wr == nb.Length;
        uint tmp;
        VirtualProtectEx(hp, addr, nb.Length, old, out tmp);
        byte[] vb = new byte[nb.Length]; int vg;
        ReadProcessMemory(hp, addr, vb, nb.Length, out vg);
        CloseHandle(hp);
        return "addr=" + addr.ToString("X8") + " orig=" + orig + " wrote=" + ok + " verify=" + BitConverter.ToString(vb).Replace("-", " ");
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if ($TargetPid -eq 0) { $p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1; if (-not $p) { 'no game'; exit 1 }; $TargetPid = $p.Id }
$addr = [Convert]::ToUInt32($Addr, 16)
$nb = @($Bytes -split '[ ,]+' | Where-Object { $_ } | ForEach-Object { [Convert]::ToByte($_, 16) })
if ($nb.Count -eq 0) { 'empty bytes'; exit 1 }
[PatchLive]::Run($TargetPid, $addr, [byte[]]$nb, [bool]$ReadOnly)