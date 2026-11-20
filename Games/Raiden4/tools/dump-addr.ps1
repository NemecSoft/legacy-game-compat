# 任意地址 raw hex dump：读 len 字节按 16/行 打印，用于 struct 字段差分。
param([string]$Addrs = '', [int]$Len = 0x200, [string]$Out = '')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class DumpAddr
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static int Run(int pid, string addrs, int len, string outPath)
    {
        IntPtr hp = OpenProcess(0x0010, false, pid);
        if (hp == IntPtr.Zero) { Console.WriteLine("OpenProcess fail"); return 1; }
        StringBuilder all = new StringBuilder();
        foreach (string a in addrs.Split(','))
        {
            uint addr = Convert.ToUInt32(a.Trim(), 16);
            byte[] b = new byte[len]; int got;
            if (!ReadProcessMemory(hp, addr, b, len, out got) || got != len) { all.Append("read fail " + a + "\r\n"); continue; }
            all.Append("addr=" + addr.ToString("X8") + " t=" + DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n");
            for (int i = 0; i < len; i += 16)
            {
                all.Append("+" + i.ToString("X3") + ":");
                for (int j = 0; j < 16; j++) all.Append(" " + b[i + j].ToString("X2"));
                all.Append("\r\n");
            }
        }
        CloseHandle(hp);
        if (outPath != "") File.WriteAllText(outPath, all.ToString());
        Console.Write(all);
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
[DumpAddr]::Run($p.Id, $Addrs, $Len, $Out) | Out-Null