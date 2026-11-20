// 血战上海滩 —— 一键秘籍引擎（Cheats.cs，与 Launcher.cs 一起编译）。
//
// 出处：用户 2026-11-15 提供的权威秘籍表（与游戏自带 说明(必看).txt 一致）：
//   按 ~ 打开控制台 → 输入指令回车 → 再按 ~ 关闭：
//   god_on / god_off / haveallweapon / addammo / ammonolimit
//
// 技术要点（CSDN 社区文章 agent.csdn.net/6a4f298a662f9a54cb8cb05a 同款思路，本文件独立实现）：
//   1. Angelica 引擎（2003）读的是 DirectInput 扫描码，普通 VK 事件部分键不认，
//      所以全部用 SendInput + KEYEVENTF_SCANCODE（硬件扫描码），~ 键扫描码 0x29。
//   2. 下划线 '_' 是 shift + minus(0x0C)，god_on/god_off 里要带上。
//   3. 发送前必须把游戏窗口拉到前台（SetForegroundWindow），否则键发给了启动器自己。
//   4. 每个键之间留 60-80ms——老引擎的输入轮询慢，打太快会丢字符（社区文章实测同结论）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class Cheats
{
    public class Cheat
    {
        public string Id;        // 命令行用 id（/cheat=xxx）
        public string Label;     // 按钮文案
        public string[] Lines;   // 依次输入的指令（不含首尾 ~，引擎自动包）
        public Cheat(string id, string label, params string[] lines)
        { Id = id; Label = label; Lines = lines; }
    }

    // 秘籍表：只收录用户提供的 5 条 + 一键全开组合（无敌+全武器+无限弹药，玩家最常用的三连）。
    public static readonly Cheat[] All = new Cheat[]
    {
        new Cheat("god",        "无敌开",   "god_on"),
        new Cheat("godoff",     "无敌关",   "god_off"),
        new Cheat("weapons",    "全武器",   "haveallweapon"),
        new Cheat("ammo",       "补弹药",   "addammo"),
        new Cheat("nolimit",    "无限弹药", "ammonolimit"),
        new Cheat("all",        "一键全开", "god_on", "haveallweapon", "ammonolimit"),
    };

    // US 键盘 Set-1 扫描码（只列秘籍用到的字符，不全抄表）
    static readonly Dictionary<char, byte> Scan = new Dictionary<char, byte>
    {
        { 'a', 0x1E }, { 'd', 0x20 }, { 'e', 0x12 }, { 'f', 0x21 }, { 'g', 0x22 },
        { 'h', 0x23 }, { 'i', 0x17 }, { 'l', 0x26 }, { 'm', 0x32 }, { 'n', 0x31 },
        { 'o', 0x18 }, { 'p', 0x19 }, { 't', 0x14 }, { 'u', 0x16 }, { 'v', 0x2F },
        { 'w', 0x11 },
        { '_', 0x0C },   // minus 键，需配合 Shift
    };
    const byte SC_SHIFT = 0x2A, SC_ENTER = 0x1C, SC_GRAVE = 0x29;   // `~ 键 = 0x29

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time, dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
        // 补齐到 MOUSEINPUT 大小（x64 上 40 字节），SendInput 校验 cbSize 必须是整个 INPUT 的尺寸；
        // csc 未开 /unsafe，不能写 fixed 缓冲，用普通 uint 字段补
        public uint pad0, pad1, pad2, pad3, pad4, pad5, pad6;
    }
    const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint n, INPUT[] inputs, int cbSize);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr lp);
    const int SW_RESTORE = 9;

    // 找游戏主窗口：进程 shanghai.exe 的可见、非最小化、面积最大的顶层窗口。
    public static IntPtr FindGameWindow()
    {
        IntPtr best = IntPtr.Zero; long bestArea = 0;
        Process[] ps = Process.GetProcessesByName("shanghai");
        if (ps.Length == 0) return IntPtr.Zero;
        HashSet<uint> pids = new HashSet<uint>();
        foreach (Process p in ps) { try { pids.Add((uint)p.Id); } catch { } }
        EnumWindows(delegate (IntPtr h, IntPtr lp)
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != 0 && pids.Contains(pid) && IsWindowVisible(h) && !IsIconic(h))
            {
                RECT r; GetWindowRect(h, out r);
                long area = (long)(r.R - r.L) * (r.B - r.T);
                if (area > bestArea) { bestArea = area; best = h; }
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    static void Key(byte scan, bool up)
    {
        INPUT[] inp = new INPUT[1];
        inp[0].type = INPUT_KEYBOARD;
        inp[0].ki.wScan = scan;
        inp[0].ki.dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0);
        SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }

    static void Tap(byte scan, int downMs, int afterMs)
    {
        Key(scan, false); Thread.Sleep(downMs);
        Key(scan, true); Thread.Sleep(afterMs);
    }

    // 打一个字符：字母直按；'_' 走 Shift+Minus
    static void TypeChar(char c)
    {
        byte sc;
        if (!Scan.TryGetValue(c, out sc)) return;
        if (c == '_')
        {
            Key(SC_SHIFT, false); Thread.Sleep(20);
            Tap(sc, 20, 60);
            Key(SC_SHIFT, true); Thread.Sleep(30);
        }
        else Tap(sc, 20, 70);
    }

    // 发送一条完整秘籍：~ → 指令 → 回车 → ~。所有节奏按老引擎输入轮询放宽。
    static void SendLine(string line)
    {
        Tap(SC_GRAVE, 30, 350);                       // 打开控制台
        foreach (char c in line) TypeChar(c);
        Thread.Sleep(150);
        Tap(SC_ENTER, 30, 250);                       // 回车执行
        Tap(SC_GRAVE, 30, 250);                       // 关闭控制台
    }

    /// <summary>对运行中的游戏发送秘籍。返回 null=成功，否则为失败原因（已给玩家看的话）。</summary>
    public static string Send(Cheat c, Action<string> log)
    {
        IntPtr wnd = FindGameWindow();
        if (wnd == IntPtr.Zero) return "没找到正在运行的游戏窗口";
        ShowWindow(wnd, SW_RESTORE);                  // 万一被最小化，先还原
        SetForegroundWindow(wnd);
        Thread.Sleep(400);                            // 等引擎接管键盘

        log("秘籍「" + c.Label + "」：窗口已切到游戏，开始发送（~ 打开控制台 → 指令 → 回车 → ~ 关闭）");
        for (int i = 0; i < c.Lines.Length; i++)
        {
            SendLine(c.Lines[i]);
            log("已发送指令 " + (i + 1) + "/" + c.Lines.Length + "：" + c.Lines[i]);
            if (i < c.Lines.Length - 1) Thread.Sleep(400);
        }
        log("秘籍「" + c.Label + "」发送完毕");
        return null;
    }
}
