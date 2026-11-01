using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// 迷你调试器：启动目标进程，打印模块加载 / 首次异常 / 退出码。
// 编译：csc -nologo -out:procdbg.exe procdbg.cs
class ProcDbg
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb;
        public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [StructLayout(LayoutKind.Sequential)]
    struct DEBUG_EVENT
    {
        public int dwDebugEventCode, dwProcessId, dwThreadId;
        public UInt64 u1, u2, u3, u4, u5, u6, u7, u8, u9, u10;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit,
        uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WaitForDebugEvent(ref DEBUG_EVENT de, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ContinueDebugEvent(int pid, int tid, uint status);
    [DllImport("kernel32.dll")]
    static extern bool DebugActiveProcessStop(int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetFinalPathNameByHandle(IntPtr h, StringBuilder buf, uint len, uint flags);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")]
    static extern bool TerminateProcess(IntPtr h, uint code);

    const uint DEBUG_ONLY_THIS_PROCESS = 0x2;
    const int CREATE_UNICODE_ENVIRONMENT = 0x400;
    const uint DBG_CONTINUE = 0x00010002;
    const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;

    static void Main(string[] args)
    {
        string exe = args[0];
        string cmdline = "\"" + exe + "\"";
        for (int i = 1; i < args.Length; i++) cmdline += " " + args[i];
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        string cwd = Path.GetDirectoryName(exe);
        if (!CreateProcess(exe, cmdline, IntPtr.Zero, IntPtr.Zero, false,
                DEBUG_ONLY_THIS_PROCESS | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, cwd, ref si, out pi))
        {
            Console.WriteLine("CreateProcess failed: " + Marshal.GetLastWin32Error());
            return;
        }
        Console.WriteLine("pid=" + pi.dwProcessId);
        bool exited = false;
        DateTime t0 = DateTime.Now;
        while (!exited)
        {
            DEBUG_EVENT de = new DEBUG_EVENT();
            if (!WaitForDebugEvent(ref de, 20000)) { Console.WriteLine("timeout"); TerminateProcess(pi.hProcess, 1); break; }
            uint cont = DBG_CONTINUE;
            double el = (DateTime.Now - t0).TotalSeconds;
            switch (de.dwDebugEventCode)
            {
                case 1: // EXCEPTION
                {
                    uint code = (uint)(de.u1 & 0xFFFFFFFF);
                    if (code != 0x80000003)
                        Console.WriteLine(string.Format("{0,6:n2}s EXCEPTION {1:X8} addr={2:X8}", el, code, de.u3 & 0xFFFFFFFF));
                    break;
                }
                case 3: // CREATE_PROCESS
                    Console.WriteLine(string.Format("{0,6:n2}s CREATE_PROCESS", el));
                    break;
                case 5: // EXIT_PROCESS
                    Console.WriteLine(string.Format("{0,6:n2}s EXIT code=0x{1:X8}", el, de.u1 & 0xFFFFFFFF));
                    exited = true;
                    break;
                case 6: // LOAD_DLL
                {
                    IntPtr hFile = (IntPtr)(long)de.u1;
                    IntPtr baseAddr = (IntPtr)(long)de.u2;
                    string name = "?";
                    try
                    {
                        StringBuilder sb = new StringBuilder(1024);
                        if (GetFinalPathNameByHandle(hFile, sb, 1024, 0) > 0) name = sb.ToString();
                    }
                    catch { }
                    Console.WriteLine(string.Format("{0,6:n2}s LOAD {1:X8} {2}", el, (long)baseAddr & 0xFFFFFFFF, name));
                    if (hFile != IntPtr.Zero) CloseHandle(hFile);
                    break;
                }
            }
            ContinueDebugEvent(de.dwProcessId, de.dwThreadId, cont);
        }
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    }
}