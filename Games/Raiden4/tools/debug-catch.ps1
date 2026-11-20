param([int]$GamePid, [string]$Out = 'D:\Temp\crash-context.txt', [string]$EvLog = 'D:\Temp\dbg-events.txt')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class DebugCatch
{
    [DllImport("kernel32.dll")] public static extern bool DebugActiveProcess(int pid);
    [DllImport("kernel32.dll")] public static extern bool DebugActiveProcessStop(int pid);
    [DllImport("kernel32.dll")] public static extern bool WaitForDebugEvent(byte[] ev, int ms);
    [DllImport("kernel32.dll")] public static extern bool ContinueDebugEvent(int pid, int tid, uint status);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenThread(uint acc, bool inh, int tid);
    [DllImport("kernel32.dll")] public static extern bool Wow64GetThreadContext(IntPtr h, byte[] ctx);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    static string Regs(byte[] c)
    {
        StringBuilder sb = new StringBuilder();
        string[] names = { "Edi", "Esi", "Ebx", "Edx", "Ecx", "Eax", "Ebp", "Eip", "Esp" };
        int[] offs = { 164, 168, 172, 176, 180, 184, 188, 192, 204 };
        for (int i = 0; i < names.Length; i++)
            sb.Append(names[i]).Append("=").Append(BitConverter.ToUInt32(c, offs[i]).ToString("X8")).Append(" ");
        return sb.ToString();
    }
    public static int Run(int pid, string outPath, string evLog)
    {
        if (!DebugActiveProcess(pid)) return 1;
        try
        {
            IntPtr hp = OpenProcess(0x0410, false, pid);
            byte[] ev = new byte[256];
            StringBuilder log = new StringBuilder();
            while (WaitForDebugEvent(ev, -1))
            {
                int code = BitConverter.ToInt32(ev, 0);
                int tid = BitConverter.ToInt32(ev, 8);
                uint cont = 0x00010002;
                string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
                if (code == 1)
                {
                    uint ecode = BitConverter.ToUInt32(ev, 16);
                    uint eaddr = BitConverter.ToUInt32(ev, 32);
                    uint first = BitConverter.ToUInt32(ev, 168);
                    log.AppendLine(stamp + " EXC code=" + ecode.ToString("X8") + " addr=" + eaddr.ToString("X8") + " first=" + first + " tid=" + tid);
                    if (ecode == 0xC0000005)
                    {
                        IntPtr ht = OpenThread(0x0048, false, tid);
                        byte[] ctx = new byte[1024];
                        BitConverter.GetBytes((uint)0x10007).CopyTo(ctx, 0);
                        string line = stamp + " AV addr=" + eaddr.ToString("X8") + " first=" + first + "\r\n";
                        if (ht != IntPtr.Zero && Wow64GetThreadContext(ht, ctx))
                        {
                            line += Regs(ctx) + "\r\n";
                            uint esp = BitConverter.ToUInt32(ctx, 204);
                            byte[] stack = new byte[512];
                            int got;
                            if (ReadProcessMemory(hp, esp, stack, 512, out got))
                            {
                                line += "stack@" + esp.ToString("X8") + ":\r\n";
                                for (int i = 0; i + 4 <= got; i += 4)
                                    line += "  +" + i.ToString("X3") + " " + BitConverter.ToUInt32(stack, i).ToString("X8") + "\r\n";
                            }
                            uint eip = BitConverter.ToUInt32(ctx, 192);
                            byte[] codebuf = new byte[64];
                            if (ReadProcessMemory(hp, eip - 32, codebuf, 64, out got))
                            {
                                line += "code@" + (eip - 32).ToString("X8") + ": " + BitConverter.ToString(codebuf, 0, got).Replace("-", " ") + "\r\n";
                            }
                        }
                        else line += "Wow64GetThreadContext fail\r\n";
                        File.AppendAllText(outPath, line + "\r\n");
                        if (ht != IntPtr.Zero) CloseHandle(ht);
                        cont = 0x80010001;
                    }
                    else if (ecode == 0x80000003 || ecode == 0x40010006) cont = 0x00010002;
                    else cont = 0x80010001;
                }
                else if (code == 5)
                {
                    log.AppendLine(stamp + " EXIT_PROCESS code=" + BitConverter.ToUInt32(ev, 16));
                    File.AppendAllText(evLog, log.ToString());
                    ContinueDebugEvent(pid, tid, cont);
                    break;
                }
                else log.AppendLine(stamp + " event=" + code + " tid=" + tid);
                File.AppendAllText(evLog, log.ToString());
                log.Length = 0;
                ContinueDebugEvent(pid, tid, cont);
            }
            if (hp != IntPtr.Zero) CloseHandle(hp);
            return 0;
        }
        finally
        {
            DebugActiveProcessStop(pid);
        }
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$r = [DebugCatch]::Run($GamePid, $Out, $EvLog)
Write-Output ("debugcatch exit=" + $r)