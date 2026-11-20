param([string]$Addr, [int]$N = 16, [int]$GamePid = 0)
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class DumpNear
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static string Run(int pid, uint addr, int n)
    {
        IntPtr hp = OpenProcess(0x0410, false, pid);
        if (hp == IntPtr.Zero) return "open fail";
        uint start = addr - (uint)(n * 4);
        byte[] buf = new byte[n * 8 + 4];
        int got;
        if (!ReadProcessMemory(hp, start, buf, buf.Length, out got)) { CloseHandle(hp); return "read fail"; }
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i + 4 <= got; i += 4)
        {
            uint a = start + (uint)i;
            string mark = (a == addr) ? " [" : "  ";
            sb.Append(mark).Append(BitConverter.ToUInt32(buf, i).ToString("X8")).Append(a == addr ? "]" : "").Append(' ');
            if ((i / 4 + 1) % 8 == 0) sb.AppendLine();
        }
        CloseHandle(hp);
        return sb.ToString();
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if ($GamePid -eq 0) { $GamePid = (Get-Process game -ErrorAction Stop | Select-Object -First 1).Id }
$a = [Convert]::ToUInt32($Addr, 16)
Write-Output ([DumpNear]::Run($GamePid, $a, $N))