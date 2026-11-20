param([int]$GamePid, [string]$Out = 'D:\Temp\crash-context.txt')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class CrashRead
{
    [DllImport("kernel32.dll")] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll")] public static extern bool Thread32First(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern bool Thread32Next(IntPtr h, byte[] te);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenThread(uint acc, bool inh, int tid);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool Wow64GetThreadContext(IntPtr h, byte[] ctx);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static int Run(int pid, string outPath)
    {
        IntPtr snap = CreateToolhelp32Snapshot(4, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return 1;
        IntPtr hp = OpenProcess(0x0410, false, pid);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("read@" + DateTime.Now.ToString("HH:mm:ss.fff") + " pid=" + pid);
        byte[] te = new byte[28];
        BitConverter.GetBytes((uint)28).CopyTo(te, 0);
        if (Thread32First(snap, te))
        {
            do
            {
                int tid = BitConverter.ToInt32(te, 8);
                int owner = BitConverter.ToInt32(te, 12);
                if (owner != pid) continue;
                IntPtr ht = OpenThread(0x0048, false, tid);
                if (ht == IntPtr.Zero) { sb.AppendLine("tid=" + tid + " open fail"); continue; }
                byte[] ctx = new byte[1024];
                BitConverter.GetBytes((uint)0x10007).CopyTo(ctx, 0);
                if (!Wow64GetThreadContext(ht, ctx)) { sb.AppendLine("tid=" + tid + " ctx fail"); CloseHandle(ht); continue; }
                uint eip = BitConverter.ToUInt32(ctx, 192);
                uint esp = BitConverter.ToUInt32(ctx, 204);
                sb.AppendLine("tid=" + tid + " Eip=" + eip.ToString("X8") + " Esp=" + esp.ToString("X8")
                    + " Eax=" + BitConverter.ToUInt32(ctx, 184).ToString("X8")
                    + " Ecx=" + BitConverter.ToUInt32(ctx, 180).ToString("X8")
                    + " Edx=" + BitConverter.ToUInt32(ctx, 176).ToString("X8")
                    + " Ebx=" + BitConverter.ToUInt32(ctx, 172).ToString("X8")
                    + " Esi=" + BitConverter.ToUInt32(ctx, 168).ToString("X8")
                    + " Edi=" + BitConverter.ToUInt32(ctx, 164).ToString("X8")
                    + " Ebp=" + BitConverter.ToUInt32(ctx, 188).ToString("X8"));
                if (eip >= 0x400000 && eip < 0x520000)
                {
                    byte[] stack = new byte[512];
                    int got;
                    if (ReadProcessMemory(hp, esp, stack, 512, out got))
                    {
                        sb.AppendLine("  stack@" + esp.ToString("X8") + ":");
                        for (int i = 0; i + 4 <= got; i += 4)
                            sb.AppendLine("    +" + i.ToString("X3") + " " + BitConverter.ToUInt32(stack, i).ToString("X8"));
                    }
                    byte[] code = new byte[64];
                    if (ReadProcessMemory(hp, eip - 32, code, 64, out got))
                        sb.AppendLine("  code@" + (eip - 32).ToString("X8") + ": " + BitConverter.ToString(code, 0, got).Replace("-", " "));
                }
                CloseHandle(ht);
            } while (Thread32Next(snap, te));
        }
        CloseHandle(snap);
        if (hp != IntPtr.Zero) CloseHandle(hp);
        File.WriteAllText(outPath, sb.ToString());
        Console.Write(sb.ToString());
        return 0;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$r = [CrashRead]::Run($GamePid, $Out)
Write-Output ("crashread exit=" + $r)