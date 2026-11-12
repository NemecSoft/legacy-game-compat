using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 暗黑破坏神III 夺魂之镰 终极邪恶版（SanctuaryPort 移植整合版）启动器（game_starter.exe）。
// 界面规范见仓库 .qoder/skills/legacy-launcher-gui/SKILL.md。
// 核心职责：替代整合包的「开始游戏.exe」——探测 game\CUSA00242（游戏本体）与
// CUSA00242-UPDATE（1.47 更新），设 SANCTUARY_GAME_DIR / SANCTUARY_PATCH_DIR /
// SANCTUARY_CONFIG 环境变量，再用整合包自带的 python\python.exe 跑 run.py 完整启动链
//（CPU 指令集检查、更新合并视图、模组与着色器准备），陪跑到游戏退出并原样回传退出码。
// 键位按战神III重制版方案注入（SANCTUARY_KEY_MAP）：J=□技能、K=△技能、L=○技能/返回、
// 空格=✕主要攻击、Q/E=L1/R1、U/O=L2/R2、Tab=OPTIONS、P/Esc=触摸板左右、1234=十字键；
// L3/R3 保留移植器默认（Z/X，走跑切换等），不注入。界面速查与注入表一致。

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革 / Skeuomorphic Leather） ----------
    static readonly Color BgTop = Color.FromArgb(0x3A, 0x20, 0x15);
    static readonly Color BgMid = Color.FromArgb(0x2C, 0x18, 0x10);
    static readonly Color BgBottom = Color.FromArgb(0x1C, 0x0F, 0x0A);
    static readonly Color CardTop = Color.FromArgb(0x5A, 0x34, 0x21);
    static readonly Color CardBottom = Color.FromArgb(0x3B, 0x21, 0x16);
    static readonly Color BandTop = Color.FromArgb(0x24, 0x13, 0x0D);
    static readonly Color BandBottom = Color.FromArgb(0x3A, 0x20, 0x15);
    static readonly Color PanelEdge = Color.FromArgb(0xB9, 0x8A, 0x55);
    static readonly Color Gold = Color.FromArgb(0xE3, 0xC0, 0x7A);
    static readonly Color GoldBright = Color.FromArgb(0xF0, 0xD5, 0x96);
    static readonly Color GoldDim = Color.FromArgb(0x9A, 0x6A, 0x2A);
    static readonly Color Ink = Color.FromArgb(0xF4, 0xE3, 0xC6);
    static readonly Color InkDim = Color.FromArgb(0xD8, 0xBD, 0x96);
    static readonly Color OnGold = Color.FromArgb(0x2A, 0x16, 0x0D);
    static readonly Color CapFace = Color.FromArgb(0x4A, 0x2A, 0x1B);
    static readonly Color CapTopHi = Color.FromArgb(0x9C, 0x6A, 0x3B);
    static readonly Color CapSide = Color.FromArgb(0x1C, 0x0F, 0x0A);
    static readonly Color LeatherDark = Color.FromArgb(0x1C, 0x0F, 0x0A);
    static readonly Color LeatherLight = Color.FromArgb(0x6F, 0x4A, 0x2C);
    static readonly Color Stitch = Color.FromArgb(0xB9, 0x8A, 0x55);
    static readonly Color Ok = Color.FromArgb(0xD9, 0xA4, 0x41);
    static readonly Color Warn = Color.FromArgb(0xE0, 0x7A, 0x5F);

    // ---------- launcher.ini 可覆盖 ----------
    string gameTitle = "暗黑破坏神III 夺魂之镰 终极邪恶版";
    string gameExe = "python\\python.exe";
    string gameArgs = "run.py";
    string intro =
        "《暗黑破坏神III 夺魂之镰》终极邪恶版，暴雪的刷宝经典：\n" +
        "选职业、下地宫、捡装备。键位照战神III重制版那套排：\n" +
        "J K L 空格 = □△○✕，WASD 走路、方向键翻滚，右手不离键。\n" +
        "\n" +
        "这份是 PS4 版的电脑移植整合包，免安装。\n" +
        "首次启动会准备运行文件和画面缓存，要多等一会儿。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「修复画面」，仍不行就点「打开运行日志」，里面有具体的出错记录";
    // 2026-11-05 用户定案：用途说明紧贴下拉框上方，前缀点明「本下拉框的作用是」
    string centerNote = "游戏会自动检测画面。";
    string comboHint = "本下拉框的作用是：如果有闪退等异常，换另一种模式，再启动游戏试试。";

    static readonly string[] LaunchModeNames =
    {
        "游戏自动检测（推荐）",
        "画面异常时（固定桌面分辨率）"
    };
    int launchMode;

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int hover = -1, focus = NoFocus;
    bool busy, finishing;
    bool openDetailOnStart;
    CheatSheetForm cheatSheet;
    string status = "已就绪";
    Color statusColor = Ok;
    volatile int exitCode;
    NotifyIcon tray;
    bool autoStart;
    int desktopW, desktopH;
    string desktopSource = "";

    // ---------- 字体/布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand, fConsole;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcLabelMode, rcComboHint, rcComboMode, rcStart, rcFix, rcLog, rcExit, rcCheats, rcConsole;
    SelectPanel selMode;
    ContextMenuStrip modeMenu;

    // ---------- 启动控制台（2026-11-13 用户要求：把 run.py / 移植器的控制台输出转发到界面） ----------
    // 子进程 stdout/stderr 由读线程收进 pendingConsole，UI 定时器批量刷进只读 TextBox。
    // 启动器自己的 Log() 也同步转推一份（带前缀），玩家在主窗口就能看完整启动链进度。
    TextBox consoleBox;
    System.Windows.Forms.Timer consoleTimer;
    static readonly object consoleGate = new object();
    static readonly List<string> pendingConsole = new List<string>();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    static int Main(string[] args)
    {
        Sanctuary.EnableDpiAwareness();

        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();

        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }

        if (HasArg(args, "/fixres") || HasArg(args, "-fixres") ||
            HasArg(args, "/fixresolution") || HasArg(args, "-fixresolution") ||
            HasArg(args, "/fixdisplay") || HasArg(args, "-fixdisplay"))
        {
            int w, h;
            string src;
            Sanctuary.GetDesktop(out w, out h, out src);
            string msg;
            bool ok = Sanctuary.ApplySafeDisplay(AppDir(), w, h, out msg);
            Log("命令行修复画面：" + msg + "（桌面 " + w + "×" + h + "，" + src + "）");
            return ok ? 0 : 1;
        }

        if (args.Length > 0 && (args[0] == "/selftest" || args[0] == "-selftest"))
            return SelfTest(Tail(args));

        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;
            if (a == "/desktop" || a == "-desktop" || a == "/fixed" || a == "-fixed") l.launchMode = 1;
            // 取证用：起来就同时开详细按键窗口，方便一次拍两张图（玩家不用记这个参数）
            if (a == "/detail" || a == "-detail") l.openDetailOnStart = true;
        }
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；父进程：" + ParentProcessName() +
            "；零交互：" + (l.autoStart ? "是" : "否"));
        Application.Run(l);
        return l.exitCode;
    }

    static bool HasArg(string[] args, string name)
    {
        foreach (string a in args) if (a == name) return true;
        return false;
    }

    static string[] Tail(string[] a)
    {
        string[] r = new string[a.Length - 1];
        Array.Copy(a, 1, r, 0, r.Length);
        return r;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct PROCESSENTRY32
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr h, int cmd);

    static int ParentPid(int pid)
    {
        IntPtr snap = CreateToolhelp32Snapshot(2, 0);
        if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) return 0;
        PROCESSENTRY32 e = new PROCESSENTRY32();
        e.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
        uint parent = 0;
        if (Process32First(snap, ref e))
        {
            do { if (e.th32ProcessID == (uint)pid) { parent = e.th32ParentProcessID; break; } }
            while (Process32Next(snap, ref e));
        }
        CloseHandle(snap);
        return (int)parent;
    }

    static string ParentProcessName()
    {
        try
        {
            int pid = ParentPid(Process.GetCurrentProcess().Id);
            if (pid == 0) return "";
            using (Process p = Process.GetProcessById(pid)) return p.ProcessName.ToLowerInvariant();
        }
        catch { return ""; }
    }

    static void HideParentConsole()
    {
        try
        {
            int parent = ParentPid(Process.GetCurrentProcess().Id);
            if (parent <= 0) return;
            string name;
            try { name = Process.GetProcessById(parent).ProcessName; }
            catch { return; }
            if (name != "cmd" && name != "powershell" && name != "pwsh") return;
            uint target = (uint)parent;
            EnumWindows(delegate(IntPtr h, IntPtr lp)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == target && IsWindowVisible(h)) ShowWindow(h, 0);
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    public Launcher()
    {
        LoadIni();
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Text = gameTitle;
        BackColor = BgMid;
        // 窗口尺寸在构造函数里一步到位：按 UI 规范基准 900×850、三栏 252/296/280，
        // 底部加一条启动控制台（转发 run.py / 移植器输出，2026-11-13 用户要求），
        // 完整按键速查放进独立普通窗口（用户定案 2026-11-05）。
        // CenterScreen 按窗口创建时的 Size 定位置，先给个小尺寸再放大会让左上角不跟着动
        // （实测 900×620 → 1340×880 偏右下 220×130、底边出屏，规范 28）。
        // 此时窗口句柄还没建，dpi 从桌面 DC 取；后续不再改尺寸。
        try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96f; }
        catch { dpi = 1f; }
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        ClientSize = new Size(Math.Min(S(900), wa.Width - S(30)), Math.Min(S(850), wa.Height - S(30)));
        MakeFonts();
        // 通用拟物键帽库：四平台一套标准件，注入 DPI 与键帽字体后全窗口复用
        Keycaps.Configure(dpi, fCap, fCapZh);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (selMode == null)
        {
            selMode = MakeSelect(LaunchModeNames, launchMode, delegate { ShowModeMenu(); });
            selMode.TabIndex = 0;
        }
        if (consoleBox == null)
        {
            // 启动控制台：只读深底文本框，跟着 rcConsole 走；刷新走 120ms 定时器批量粘贴
            consoleBox = new TextBox();
            consoleBox.Multiline = true;
            consoleBox.ReadOnly = true;
            consoleBox.ScrollBars = ScrollBars.Vertical;
            consoleBox.WordWrap = false;
            consoleBox.BorderStyle = BorderStyle.None;
            consoleBox.Font = fConsole;
            consoleBox.BackColor = Color.FromArgb(0x16, 0x0C, 0x08);
            consoleBox.ForeColor = Ink;
            consoleBox.Text = "控制台就绪：点「启动游戏」后，这里实时转发 run.py 与移植器的输出。\r\n";
            Controls.Add(consoleBox);
            consoleTimer = new System.Windows.Forms.Timer();
            consoleTimer.Interval = 120;
            consoleTimer.Tick += delegate { FlushConsole(); };
            consoleTimer.Start();
        }
        LayoutRects();
        Sanctuary.GetDesktop(out desktopW, out desktopH, out desktopSource);
        string msg = Sanctuary.Describe(desktopW, desktopH);
        status = "已就绪";
        statusColor = Ok;
        LoadGameIcon();
        string dirs;
        bool found = Sanctuary.ResolveDirs(AppDir(), out dirs);
        Log("启动 " + gameTitle + "｜桌面 " + msg + "（" + desktopSource + "）｜DPI " + dpi.ToString("0.##") +
            "｜" + dirs);
        if (!found) { status = "没找到游戏本体（game 目录）"; statusColor = Warn; }
        if (autoStart) BeginInvoke(new Action(delegate { StartGame(); }));
        if (openDetailOnStart) BeginInvoke(new Action(delegate { OpenCheatSheet(); }));
    }

    void MakeFonts()
    {
        string ui = PickFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" },
            SystemFonts.MessageBoxFont.Name);
        string mono = PickFont(new[] { "Consolas", "Courier New" }, "Courier New");
        fTitle = new Font(ui, 15f * dpi, FontStyle.Bold);
        fHead = new Font(ui, 10.5f * dpi, FontStyle.Bold);
        fRowSel = new Font(ui, 10f * dpi, FontStyle.Bold);
        fRow = new Font(ui, 10f * dpi, FontStyle.Regular);
        fBody = new Font(ui, 9.5f * dpi, FontStyle.Regular);
        fCap = new Font(ui, 9f * dpi, FontStyle.Bold);
        fCapZh = new Font(ui, 8.5f * dpi, FontStyle.Bold);
        fMono = new Font(mono, 9f * dpi, FontStyle.Regular);
        fConsole = new Font(mono, 8f * dpi, FontStyle.Regular);
        fBrand = new Font(ui, 12f * dpi, FontStyle.Bold);
    }

    static string PickFont(string[] candidates, string fallback)
    {
        try
        {
            using (InstalledFontCollection col = new InstalledFontCollection())
            {
                foreach (string name in candidates)
                    foreach (FontFamily fam in col.Families)
                        if (string.Equals(fam.Name, name, StringComparison.OrdinalIgnoreCase))
                            return fam.Name;
            }
        }
        catch { }
        return fallback;
    }

    int S(int v) { return (int)Math.Round(v * dpi); }

    void LayoutRects()
    {
        // 规范基准三栏 252 / 296 / 280，栏间距 16，外边距 20（900×850）
        int pad = S(20), gut = S(16);
        int c1 = S(252), c2 = S(296);
        int c3 = ClientSize.Width - pad * 2 - c1 - c2 - gut * 2;
        int top = S(88), statusH = S(54);
        // 底部控制台：优先给三栏留足 464 高（旧 620 基准），小屏时控制台缩水但不小于 64
        int avail = ClientSize.Height - top - statusH - S(34);
        int consoleH = Math.Max(S(64), Math.Min(S(150), avail - S(464)));
        int bottom = top + (avail - consoleH);
        colLeft = new Rectangle(pad, top, c1, bottom - top);
        colCenter = new Rectangle(pad + c1 + gut, top, c2, bottom - top);
        colRight = new Rectangle(pad + c1 + gut + c2 + gut, top, c3, bottom - top);
        rcConsole = new Rectangle(pad, bottom + S(30), ClientSize.Width - pad * 2, consoleH);
        if (consoleBox != null)
            consoleBox.Bounds = new Rectangle(rcConsole.X + S(2), rcConsole.Y + S(2),
                rcConsole.Width - S(4), rcConsole.Height - S(4));

        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        int noteBottom = colCenter.Y + S(46) + MeasureCenterNoteHeight(innerW);
        rcLabelMode = new Rectangle(inner, noteBottom + S(14), innerW, S(22));
        // 用途说明紧贴下拉框上方：标签 → 说明 → 框，三步挨着，不另拉空白（硬约束 18）
        rcComboHint = new Rectangle(inner, rcLabelMode.Bottom + S(4), innerW,
            MeasureComboHintHeight(innerW));
        rcComboMode = new Rectangle(inner, rcComboHint.Bottom + S(6), innerW, S(30));
        int startY = rcComboMode.Bottom + S(18);
        rcStart = new Rectangle(inner, startY, innerW, S(44));
        // 三个按钮不等宽：「打开运行日志」六个字必须完整显示，退出两个字可以窄
        int wFix = S(72), wLog = S(124);
        rcFix = new Rectangle(inner, rcStart.Bottom + S(10), wFix, S(34));
        rcLog = new Rectangle(rcFix.Right + S(8), rcFix.Y, wLog, S(34));
        rcExit = new Rectangle(rcLog.Right + S(8), rcLog.Y, innerW - (rcLog.Right + S(8) - inner), S(34));
        PlaceSelect(selMode, rcComboMode);

        // 右栏底部：一键打开完整按键速查（独立普通窗口，不模态、不遮挡主窗口）
        int rx = colRight.X + S(14);
        rcCheats = new Rectangle(rx, colRight.Bottom - S(52), colRight.Width - S(28), S(38));
    }

    int MeasureCenterNoteHeight(int width)
    {
        using (Graphics g = CreateGraphics())
        {
            int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(3);
            int total = 0;
            foreach (string raw in centerNote.Split('\n'))
            {
                if (raw.Length == 0) { total += lh / 2; continue; }
                bool head = raw.StartsWith("# ");
                Font f = head ? fHead : fBody;
                if (head) total += S(2);
                string line = head ? raw.Substring(2) : raw;
                while (line.Length > 0)
                {
                    int take = line.Length;
                    while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > width)
                        take--;
                    total += lh;
                    line = line.Substring(take);
                }
            }
            return total;
        }
    }

    // 下拉框用途说明按中栏宽度折行，返回它占几行的高度，供 LayoutRects 给下拉框让位
    int MeasureComboHintHeight(int width)
    {
        using (Graphics g = CreateGraphics())
        {
            int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(2);
            int lines = 0;
            string line = comboHint;
            while (line.Length > 0)
            {
                int take = line.Length;
                while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), fBody).Width > width)
                    take--;
                lines++;
                line = line.Substring(take);
            }
            return lines * lh;
        }
    }

    // ---------- 绘制 ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        DrawLeatherBg(g, all);
        Rectangle head = new Rectangle(0, 0, ClientSize.Width, S(76));
        DrawBand(g, head);
        int footY = ClientSize.Height - S(60);
        Rectangle foot = new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY);
        DrawBand(g, foot);

        DrawHeader(g);
        DrawLeft(g);
        DrawCenter(g);
        DrawRight(g);
        DrawConsoleChrome(g);
        DrawFooter(g);
    }

    // 启动控制台的拟物外框与标题（TextBox 子控件盖在框内，边框只露 1px）
    void DrawConsoleChrome(Graphics g)
    {
        if (rcConsole.Height <= 0 || consoleBox == null) return;
        TextRenderer.DrawText(g, "启动控制台 · run.py 与移植器的实时输出", fMono,
            new Point(rcConsole.X + S(2), rcConsole.Y - S(20)), InkDim, Color.Transparent);
        using (GraphicsPath p = Round(rcConsole, S(8)))
        using (Pen edge = new Pen(Color.FromArgb(130, LeatherLight), 1f))
            g.DrawPath(edge, p);
    }

    // ---------- 控制台转发 ----------
    // 任意线程可调：只往队列塞行，UI 线程的 120ms 定时器统一粘贴进 TextBox
    static void PushConsole(string line)
    {
        lock (consoleGate)
        {
            pendingConsole.Add(line);
            if (pendingConsole.Count > 2000) pendingConsole.RemoveRange(0, pendingConsole.Count - 2000);
        }
    }

    void FlushConsole()
    {
        string[] batch;
        lock (consoleGate)
        {
            if (pendingConsole.Count == 0 || consoleBox == null) return;
            batch = pendingConsole.ToArray();
            pendingConsole.Clear();
        }
        try
        {
            if (consoleBox.TextLength > 60000 || consoleBox.Lines.Length + batch.Length > 600)
            {
                // 行数超限：关重绘后整体重赋（保留最近 500 行），防止启动阶段几千行日志把 UI 拖死
                SendMessage(consoleBox.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);   // WM_SETREDRAW off
                List<string> keep = new List<string>(consoleBox.Lines);
                foreach (string ln in batch) keep.Add(ln);
                if (keep.Count > 500) keep.RemoveRange(0, keep.Count - 500);
                consoleBox.Lines = keep.ToArray();
                SendMessage(consoleBox.Handle, 0x000B, (IntPtr)1, IntPtr.Zero);     // WM_SETREDRAW on
                consoleBox.Invalidate();
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (string ln in batch) sb.Append(ln).Append("\r\n");
                consoleBox.AppendText(sb.ToString());
            }
            consoleBox.SelectionStart = consoleBox.TextLength;
            consoleBox.ScrollToCaret();
        }
        catch { }
    }

    void DrawLeatherBg(Graphics g, Rectangle r)
    {
        using (LinearGradientBrush b = new LinearGradientBrush(r, BgTop, BgBottom, LinearGradientMode.Vertical))
            g.FillRectangle(b, r);
        using (Pen dark = new Pen(Color.FromArgb(12, 0, 0, 0), 1f))
            for (int x = -r.Height; x < r.Width; x += 9)
                g.DrawLine(dark, x, 0, x + r.Height, r.Height);
        using (Pen light = new Pen(Color.FromArgb(8, 255, 220, 160), 1f))
            for (int x = 0; x < r.Width + r.Height; x += 13)
                g.DrawLine(light, x, 0, x - r.Height, r.Height);
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddRectangle(r);
            using (PathGradientBrush pb = new PathGradientBrush(path))
            {
                pb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                pb.SurroundColors = new Color[] { Color.FromArgb(120, 10, 4, 2) };
                g.FillPath(pb, path);
            }
        }
    }

    void DrawBand(Graphics g, Rectangle r)
    {
        using (LinearGradientBrush b = new LinearGradientBrush(r, BandTop, BandBottom, LinearGradientMode.Horizontal))
            g.FillRectangle(b, r);
        using (Pen p = new Pen(Color.FromArgb(14, 0, 0, 0), 1f))
            for (int x = r.X; x < r.Right; x += 7)
                g.DrawLine(p, x, r.Y, x, r.Bottom);
        using (Pen hi = new Pen(Color.FromArgb(45, 255, 220, 160), 1f))
            g.DrawLine(hi, r.X, r.Y, r.Right, r.Y);
    }

    void DrawHeader(Graphics g)
    {
        TextRenderer.DrawText(g, gameTitle, fTitle,
            new Point(colLeft.X + S(1), S(17)), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, gameTitle, fTitle, new Point(colLeft.X, S(16)), GoldBright, Color.Transparent);
        TextRenderer.DrawText(g, "Diablo III: Reaper of Souls · Ultimate Evil Edition (SanctuaryPort)",
            fMono, new Point(colLeft.X, S(48)), InkDim, Color.Transparent);
        using (Pen p = new Pen(GoldDim, 1f))
            g.DrawLine(p, colLeft.X, S(76), ClientSize.Width - S(20), S(76));
        using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
            g.DrawLine(sh, colLeft.X, S(77), ClientSize.Width - S(20), S(77));
    }

    void DrawLeft(Graphics g)
    {
        Card(g, colLeft);
        int x = colLeft.X + S(16), y = colLeft.Y + S(14);
        TextRenderer.DrawText(g, "关于这款游戏", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(28);
        y += S(10);
        // 左栏只写游戏说明；表的看法写在右栏自己头上，翻页玩家用滚轮，不用教
        DrawWrapped(g, intro, x, y, colLeft.Width - S(32), fBody, Ink);
    }

    void DrawCenter(Graphics g)
    {
        Card(g, colCenter);
        int x = colCenter.X + S(16), y = colCenter.Y + S(14);
        TextRenderer.DrawText(g, "启动说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(32);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);
        TextRenderer.DrawText(g, "启动选项", fHead,
            new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);
        // 紧贴下拉框上方的用途说明（用户定案 2026-11-05）
        DrawWrapped(g, comboHint, rcComboHint.X, rcComboHint.Y, rcComboHint.Width, fBody, InkDim);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFix, "修复画面", false, !busy,
            hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcLog, "打开运行日志", false, true,
            hover == 2 || focus == 2, focus == 2);
        DrawButton(g, rcExit, "退出", false, true,
            hover == 3 || focus == 3, focus == 3);
        if (focus == 4 && selMode != null) FocusRing(g, selMode.Bounds);
        // 中栏只放玩家用得上的东西。启动器内部做了什么属于维护者笔记，
        // 写在「适配过程分析.md」4.1，不上界面（2026-11-04 用户定案）。
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

    // 主窗口右栏只放极简常用键 + 一个「详细按键」按钮；完整六卡片速查在独立窗口里。
    // 键位来源：战神III重制版方案（SANCTUARY_KEY_MAP 注入），动作语义是暗黑3主机版布局。
    static readonly CheatRow[] QuickRows =
    {
        new CheatRow("ps:stick_l", "pc:W pc:A pc:S pc:D", "移动"),
        new CheatRow("ps:square ps:triangle ps:circle ps:cross", "pc:J pc:K pc:L pc:Space", "技能 / 技能 / 返回 / 主要攻击"),
        new CheatRow("ps:R2", "pc:O", "次要攻击（可长按连放）"),
        new CheatRow("ps:stick_r", "pc:Up pc:Down pc:Left pc:Right", "翻滚闪避"),
        new CheatRow("ps:L1 ps:R1 ps:L2", "pc:Q pc:E pc:U", "菜单页签 / 技能位"),
        new CheatRow("ps:dpad ps:options", "pc:1 pc:2 pc:3 pc:4 pc:Tab", "拾取轮换 / 暂停菜单")
    };

    void DrawRight(Graphics g)
    {
        Card(g, colRight);
        int x = colRight.X + S(14), w = colRight.Width - S(28);
        int y = colRight.Y + S(14);
        int headH = TextRenderer.MeasureText(g, "国", fHead).Height;
        TextRenderer.DrawText(g, "操作说明", fHead, new Point(x, y), GoldBright, Color.Transparent);
        y += headH + S(6);
        TextRenderer.DrawText(g, "深色 = 手柄键，金色 = 键盘键", fBody,
            new Point(x, y), InkDim, Color.Transparent);
        y += headH + S(12);
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(2);
        int capH = S(21);
        foreach (CheatRow r in QuickRows)
        {
            int gw = Keycaps.GroupWidth(g, r.Glyph, capH);
            Keycaps.DrawGroup(g, r.Glyph, x, y, capH, false);
            Keycaps.DrawGroup(g, r.Pc, x + gw + S(6), y, capH, true);
            y += capH + S(3);
            foreach (string ln in WrapLines(g, r.Text, w, fBody))
            {
                TextRenderer.DrawText(g, ln, fBody, new Point(x, y), Ink, Color.Transparent);
                y += lh;
            }
            y += S(6);
        }
        DrawButton(g, rcCheats, "详细按键", false, true,
            hover == 5 || focus == 5, focus == 5);
    }

    // ---------- 按键速查：照 PS4 官方默认键位图复刻，每行补上本启动器的全键盘键 ----------
    // 金色键帽 = 玩家要按的键盘键；深色键帽 = 游戏画面上的手柄符号，只用来对照。
    class CheatRow
    {
        public string Glyph;
        public string Pc;
        public string Text;
        public CheatRow(string glyph, string pc, string text) { Glyph = glyph; Pc = pc; Text = text; }
    }

    class CheatCard
    {
        public string Title;
        public Color Accent;
        public CheatRow[] Rows;
        public CheatCard(string title, Color accent, params CheatRow[] rows)
        {
            Title = title; Accent = accent; Rows = rows;
        }
    }

    CheatCard[] cheatCards;
    CheatCard[] CheatCards
    {
        get { if (cheatCards == null) cheatCards = BuildCheatCards(); return cheatCards; }
    }

    // 底部提醒同样按「手柄键帽 + 金色 PC 键帽 + 说明」排版：
    // 说明里提到的每个手柄键都要能在这行键帽上找到对应键盘键，别让玩家看着手柄键名猜键盘。
    static readonly CheatRow[] CheatNotes =
    {
        new CheatRow("ps:square ps:triangle ps:circle ps:cross", "pc:J pc:K pc:L pc:Space",
            "1  手柄四个主键照战神3习惯排在 J K L 空格：□ 技能 / △ 技能 / ○ 技能兼菜单返回 / ✕ 主要攻击（长按连打）。"),
        new CheatRow("ps:R2 ps:stick_r", "pc:O pc:Up pc:Down pc:Left pc:Right",
            "2  次要攻击在 O（R2）上，一般是输出主力；翻滚闪避在方向键（右摇杆），往哪按往哪滚。"),
        new CheatRow("ps:L3 ps:dpad", "pc:Z pc:X pc:1 pc:2 pc:3 pc:4",
            "3  Z X 是按下左右摇杆（走跑切换等），保留游戏默认；1 2 3 4 对应十字键，游戏里轮换新掉落的装备。")
    };

    static CheatCard[] BuildCheatCards()
    {
        Color red = Warn, cyan = Color.FromArgb(0x7C, 0xC3, 0xEE);
        // Glyph / Pc 都是通用键帽库的 id 串（见 templates\Keycaps.cs）：
        // 空格分隔 = 一排独立键帽，"+" = 组合键的小加号；"—" = 这行没有这个键。
        // 键位是战神III重制版方案（启动器经 SANCTUARY_KEY_MAP 注入），功能语义是
        // 暗黑3主机版官方布局（主要攻击=✕、次要攻击=R2、□△○=三个技能位，
        // L1/R1=菜单页签翻页、十字键=拾取装备轮换）。
        return new CheatCard[]
        {
            new CheatCard("基础移动", red,
                new CheatRow("ps:stick_l", "pc:W pc:A pc:S pc:D", "左摇杆移动：走路、走位全靠它，按住别放"),
                new CheatRow("ps:stick_r", "pc:Up pc:Down pc:Left pc:Right", "右摇杆翻滚闪避：往推的方向滚出一段，主机版独有动作"),
                new CheatRow("ps:options", "pc:Tab", "暂停菜单：难度、技能、装备、退出都在这"),
                new CheatRow("ps:L3 ps:R3", "pc:Z pc:X", "按下左 / 右摇杆：走跑切换等，保留游戏默认没改")),

            new CheatCard("攻击与技能", red,
                new CheatRow("ps:cross", "pc:Space", "✕ 主要攻击：连按或长按；菜单里同时是确认键"),
                new CheatRow("ps:R2", "pc:O", "R2 次要攻击：一般是输出主力，可长按连放"),
                new CheatRow("ps:square ps:triangle", "pc:J pc:K", "□（J）与 △（K）：两个技能位"),
                new CheatRow("ps:circle", "pc:L", "○（L）：第三个技能位；菜单里当返回 / 取消")),

            new CheatCard("肩键与十字键", Gold,
                new CheatRow("ps:L1 ps:R1", "pc:Q pc:E", "L1 / R1：背包、技能、随从、任务页签左右翻"),
                new CheatRow("ps:L2", "pc:U", "L2（U）：扳机键，技能位，功能照游戏内屏幕提示"),
                new CheatRow("ps:dpad", "pc:1 pc:2 pc:3 pc:4", "十字键上右下左 = 1 2 3 4：游戏里挑新掉落的装备、菜单导航")),

            new CheatCard("界面与系统", cyan,
                new CheatRow("ps:touchpad", "pc:P pc:Esc", "触摸板左半 = P、右半 = Esc：背包、角色与系统界面"),
                new CheatRow("ps:options", "pc:Tab", "OPTIONS = Tab：暂停 + 存档、难度与技能管理"),
                new CheatRow("ps:share", "—", "SHARE 键未绑定键盘；截图用系统的截图键")),

            new CheatCard("一句话速记", Gold,
                new CheatRow("ps:stick_l ps:stick_r", "pc:W pc:A pc:S pc:D pc:Up pc:Down pc:Left pc:Right", "WASD 走路，方向键翻滚"),
                new CheatRow("ps:square ps:triangle ps:circle ps:cross", "pc:J pc:K pc:L pc:Space", "J K L 空格 = □△○✕：技能、技能、返回、主攻"),
                new CheatRow("ps:R2 ps:L2", "pc:O pc:U", "O 副攻，U 技能"),
                new CheatRow("ps:L1 ps:R1 ps:options", "pc:Q pc:E pc:Tab", "Q E 翻页签，Tab 暂停")),

            new CheatCard("移植器画面菜单", cyan,
                new CheatRow("ps:R3 + ps:L2", "pc:Insert", "游戏内按 Insert（或 R3+L2）打开移植器画面菜单：分辨率、帧率"),
                new CheatRow("—", "pc:Insert", "画面不对也可回到启动器点「修复画面」，效果等同"),
                new CheatRow("ps:ps", "—", "存档在游戏目录 user\\savedata，迁移时连 user 文件夹一起带走"))
        };
    }

    // 完整速查表：沿用原来「两列六卡片 + 底部全宽红框」的结构，只是把窗口做宽、
    // 去掉顶部标题，说明基本一行放下，争取一屏看全不用滚轮（用户定案 2026-11-05）。
    // col 由调用方给，不写死 colRight。
    int DrawCheatContent(Graphics g, Rectangle col, int y, int top, int bottom)
    {
        int x = col.X + S(12);
        int w = col.Width - S(24);
        int gap = S(6);
        int cw = (w - gap) / 2;
        int[] lh = new int[CheatCards.Length];
        for (int i = 0; i < CheatCards.Length; i++) lh[i] = CheatCardHeight(g, CheatCards[i], cw);
        // 两列行数配平（左 4+7+3=14 行，右 5+5+4=14 行），比原来 16/12 更矮更齐
        int[] left = { 0, 1, 3 }, right = { 2, 4, 5 };
        int ly = y, ry = y;
        foreach (int i in left)
        {
            DrawCheatCard(g, CheatCards[i], x, ly, cw, top, bottom);
            ly += lh[i] + gap;
        }
        foreach (int i in right)
        {
            DrawCheatCard(g, CheatCards[i], x + cw + gap, ry, cw, top, bottom);
            ry += lh[i] + gap;
        }
        y = Math.Max(ly, ry) + S(2);
        return y + DrawCheatNotes(g, x, y, w, top, bottom);
    }

    int CheatCardHeight(Graphics g, CheatCard card, int w)
    {
        // 间距收紧：详细窗口要一屏看全，靠加宽减少折行，不靠缩字号。
        // 尾部 6px 是卡片下沿留白，别让最后一行说明压在缝线/高光线上
        int h = S(20) + S(10) + S(6);
        foreach (CheatRow r in card.Rows) h += CheatRowHeight(g, r, w);
        return h;
    }

    // 统一版式：一行键帽 + 下面整卡宽度的说明。
    // 多键组合行最多六个键帽，跟说明挤在同一行会把字压到卡片外，别再试那种排法。
    int CheatRowHeight(Graphics g, CheatRow row, int w)
    {
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(2);
        int n = WrapLines(g, row.Text, w - S(28), fBody).Length;
        return S(21) + S(2) + n * lh + S(2);
    }

    void DrawCheatCard(Graphics g, CheatCard card, int x, int y, int w, int top, int bottom)
    {
        int pad = S(12), capH = S(21), headH = S(20);
        int h = CheatCardHeight(g, card, w);
        Rectangle box = new Rectangle(x, y, w, h);
        Card(g, box);
        using (SolidBrush b = new SolidBrush(card.Accent))
            g.FillRectangle(b, x + S(10), y + S(10), S(3), S(15));
        if (InViewport(y, headH, top, bottom))
            TextRenderer.DrawText(g, card.Title, fHead, new Point(x + S(20), y + S(5)), GoldBright, Color.Transparent);

        int ry = y + S(20) + S(10);
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(2);
        foreach (CheatRow row in card.Rows)
        {
            int rh = CheatRowHeight(g, row, w);
            int gw = Keycaps.GroupWidth(g, row.Glyph, capH);
            int capY = ry + S(1);
            if (InViewport(capY, capH, top, bottom))
            {
                // 深棕 = 游戏画面上的手柄键，金色 = 玩家要按的键盘键；
                // 两边都用同一套通用拟物键帽，一组多键就是多个独立键帽。
                Keycaps.DrawGroup(g, row.Glyph, x + pad, capY, capH, false);
                Keycaps.DrawGroup(g, row.Pc, x + pad + gw + S(6), capY, capH, true);
            }
            string[] lines = WrapLines(g, row.Text, w - S(28), fBody);
            int ty = capY + capH + S(2);
            foreach (string ln in lines)
            {
                if (InViewport(ty, lh, top, bottom))
                    TextRenderer.DrawText(g, ln, fBody, new Point(x + pad, ty), Ink, Color.Transparent);
                ty += lh;
            }
            ry += rh;
        }
    }

    string[] WrapLines(Graphics g, string text, int width, Font font)
    {
        List<string> res = new List<string>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw;
            if (line.Length == 0) { res.Add(""); continue; }
            while (line.Length > 0)
            {
                int take = line.Length;
                while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), font).Width > width)
                    take--;
                res.Add(line.Substring(0, take));
                line = line.Substring(take);
            }
        }
        return res.ToArray();
    }

    int DrawCheatNotes(Graphics g, int x, int y, int w, int top, int bottom)
    {
        int pad = S(14), capH = S(21);
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(2);
        int h = S(22) + S(8) + S(6);
        foreach (CheatRow r in CheatNotes) h += CheatRowHeight(g, r, w);
        Rectangle box = new Rectangle(x, y, w, h);
        Card(g, box);
        using (GraphicsPath p = Round(box, S(12)))
        using (Pen pen = new Pen(Color.FromArgb(170, Warn), 1.2f)) g.DrawPath(pen, p);
        if (InViewport(y, S(22), top, bottom))
            TextRenderer.DrawText(g, "最容易按错的三件事", fHead, new Point(x + pad, y + S(5)), Warn, Color.Transparent);
        int ry = y + S(22);
        foreach (CheatRow r in CheatNotes)
        {
            int rh = CheatRowHeight(g, r, w);
            int capY = ry + S(1);
            int gw = Keycaps.GroupWidth(g, r.Glyph, capH);
            if (InViewport(capY, capH, top, bottom))
            {
                Keycaps.DrawGroup(g, r.Glyph, x + pad, capY, capH, false);
                Keycaps.DrawGroup(g, r.Pc, x + pad + gw + S(6), capY, capH, true);
            }
            int ty = capY + capH + S(2);
            foreach (string ln in WrapLines(g, r.Text, w - S(28), fBody))
            {
                if (InViewport(ty, lh, top, bottom))
                    TextRenderer.DrawText(g, ln, fBody, new Point(x + pad, ty), Ink, Color.Transparent);
                ty += lh;
            }
            ry += rh;
        }
        return h;
    }

    // 通用细滚动条：主窗口右栏不用了，独立详细按键窗口调用同一套画法
    void DrawScrollBar(Graphics g, int x, int y, int h, int scroll, int max, int contentH, int viewport)
    {
        if (max <= 0) return;
        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, Gold)))
            g.FillRectangle(b, x, y, S(3), h);
        int thumbH = Math.Max(S(24), (int)((long)h * viewport / Math.Max(1, contentH)));
        int thumbY = y + (int)((long)(h - thumbH) * scroll / Math.Max(1, max));
        using (GraphicsPath p = Round(new Rectangle(x - S(1), thumbY, S(5), thumbH), S(2)))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(160, Gold)))
            g.FillPath(b, p);
    }

    static class BrandGuard
    {
        static readonly byte[] Encoded = { 0x03, 0x2F, 0x34, 0x1D, 0x3B, 0x37, 0x3F };
        static readonly byte[] Expected =
        {
            0xF3, 0x2F, 0x49, 0x3D, 0x11, 0xB5, 0xFC, 0xB6, 0xBD, 0xD9, 0x20, 0xBB, 0x61, 0x8E, 0x54, 0x25,
            0xD9, 0xC8, 0x72, 0xD1, 0x17, 0x18, 0xDC, 0xAF, 0x65, 0x56, 0x1B, 0xD6, 0xDC, 0xE4, 0xB4, 0x03
        };
        public static string Text { get { return Decode(); } }
        static string Decode()
        {
            char[] chars = new char[Encoded.Length];
            for (int i = 0; i < Encoded.Length; i++) chars[i] = (char)(Encoded[i] ^ 0x5A);
            return new string(chars);
        }
        public static bool Verify()
        {
            string text = Decode();
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(text));
                if (hash.Length != Expected.Length) return false;
                for (int i = 0; i < hash.Length; i++) if (hash[i] != Expected[i]) return false;
            }
            return text.Length == 7;
        }
    }

    void DrawFooter(Graphics g)
    {
        int y = ClientSize.Height - S(42);
        using (Pen p = new Pen(GoldDim, 1f))
            g.DrawLine(p, colLeft.X, ClientSize.Height - S(54), ClientSize.Width - S(20), ClientSize.Height - S(54));
        using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
            g.DrawLine(sh, colLeft.X, ClientSize.Height - S(53), ClientSize.Width - S(20), ClientSize.Height - S(53));
        StatusDot(g, status, statusColor, colLeft.X, y);
        string hint = busy ? "关窗只是隐藏，游戏继续跑" : "游戏退出后本窗口自动关闭";
        Size sz = TextRenderer.MeasureText(g, hint, fMono);
        TextRenderer.DrawText(g, hint, fMono,
            new Point(ClientSize.Width - S(20) - sz.Width, y), InkDim, Color.Transparent);
        Size brand = TextRenderer.MeasureText(g, BrandGuard.Text, fBrand);
        Point bp = new Point((ClientSize.Width - brand.Width) / 2, y);
        TextRenderer.DrawText(g, BrandGuard.Text, fBrand,
            new Point(bp.X + S(1), bp.Y + S(1)), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, BrandGuard.Text, fBrand, bp, GoldBright, Color.Transparent);
    }

    void Card(Graphics g, Rectangle r)
    {
        Rectangle box = new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
        using (GraphicsPath p = Round(r, S(12)))
        using (LinearGradientBrush b = new LinearGradientBrush(box, CardTop, CardBottom, LinearGradientMode.Vertical))
            g.FillPath(b, p);
        Rectangle inner = Rectangle.Inflate(r, -S(8), -S(8));
        using (Pen grain = new Pen(Color.FromArgb(10, 0, 0, 0), 1f))
            for (int x = inner.Left - inner.Height; x < inner.Right; x += 8)
                g.DrawLine(grain, x, inner.Top, x + inner.Height, inner.Bottom);
        using (GraphicsPath p = Round(r, S(12)))
        {
            using (Pen edge = new Pen(LeatherDark, 1.5f)) g.DrawPath(edge, p);
            using (GraphicsPath st = Round(Rectangle.Inflate(r, -S(6), -S(6)), S(9)))
            using (Pen stitch = new Pen(Color.FromArgb(155, Stitch), 1f))
            {
                stitch.DashStyle = DashStyle.Dash;
                stitch.DashPattern = new float[] { 2.5f, 2.2f };
                g.DrawPath(stitch, st);
            }
        }
        using (Pen hi = new Pen(Color.FromArgb(55, 255, 230, 180), 1f))
            g.DrawLine(hi, r.X + S(14), r.Y + 1, r.Right - S(14), r.Y + 1);
        using (Pen sh = new Pen(Color.FromArgb(80, 0, 0, 0), 1f))
            g.DrawLine(sh, r.X + S(14), r.Bottom - 2, r.Right - S(14), r.Bottom - 2);
    }

    void DrawButton(Graphics g, Rectangle r, string text, bool primary, bool enabled, bool hot, bool focused)
    {
        Color top, bottom, edge, fore;
        if (!enabled)
        {
            top = Color.FromArgb(0x4A, 0x3A, 0x2E);
            bottom = Color.FromArgb(0x2E, 0x21, 0x19);
            edge = Color.FromArgb(0x5A, 0x4A, 0x3A);
            fore = Color.FromArgb(0xB8, 0xA5, 0x8E);
        }
        else if (primary)
        {
            top = hot ? GoldBright : Gold;
            bottom = hot ? Color.FromArgb(0xD8, 0xB2, 0x5F) : GoldDim;
            edge = LeatherLight;
            fore = OnGold;
        }
        else
        {
            top = hot ? Color.FromArgb(0x6B, 0x42, 0x28) : CardTop;
            bottom = hot ? CapFace : CardBottom;
            edge = PanelEdge;
            fore = Ink;
        }
        Rectangle box = new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
        using (GraphicsPath p = Round(r, S(8)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(box, top, bottom, LinearGradientMode.Vertical))
                g.FillPath(b, p);
            using (Pen pen = new Pen(edge, 1f)) g.DrawPath(pen, p);
        }
        using (Pen hi = new Pen(Color.FromArgb(primary ? 95 : 45, 255, 230, 180), 1f))
            g.DrawLine(hi, r.X + S(10), r.Y + S(2), r.Right - S(10), r.Y + S(2));
        using (Pen sh = new Pen(Color.FromArgb(80, 0, 0, 0), 1f))
            g.DrawLine(sh, r.X + S(10), r.Bottom - S(3), r.Right - S(10), r.Bottom - S(3));
        TextRenderer.DrawText(g, text, primary ? fRowSel : fRow, r, fore, Color.Transparent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (focused) FocusRing(g, r);
    }

    void StatusDot(Graphics g, string text, Color dot, int x, int y)
    {
        using (SolidBrush b = new SolidBrush(dot))
            g.FillEllipse(b, x, y + S(5), S(9), S(9));
        TextRenderer.DrawText(g, text, fBody, new Point(x + S(16), y), InkDim, Color.Transparent);
    }

    void FocusRing(Graphics g, Rectangle r)
    {
        using (GraphicsPath q = Round(Rectangle.Inflate(r, S(5), S(5)), S(10)))
        using (Pen soft = new Pen(Color.FromArgb(120, 0, 0, 0), 3f))
            g.DrawPath(soft, q);
        using (GraphicsPath p = Round(Rectangle.Inflate(r, S(3), S(3)), S(9)))
        using (Pen pen = new Pen(GoldBright, 2f))
            g.DrawPath(pen, p);
    }

    static GraphicsPath Round(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    int DrawWrapped(Graphics g, string text, int x, int y, int width, Font font, Color color)
    {
        int lh = TextRenderer.MeasureText(g, "国", font).Height + S(3);
        foreach (string raw in text.Split('\n'))
        {
            if (raw.Length == 0) { y += lh / 2; continue; }
            bool head = raw.StartsWith("# ");
            Font f = head ? fHead : font;
            Color c = head ? Gold : color;
            if (head) y += S(2);
            string line = head ? raw.Substring(2) : raw;
            while (line.Length > 0)
            {
                int take = line.Length;
                while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > width)
                    take--;
                TextRenderer.DrawText(g, line.Substring(0, take), f, new Point(x, y), c, Color.Transparent);
                y += lh;
                line = line.Substring(take);
            }
        }
        return y;
    }

    // 键帽全部走通用库 src\Keycaps.cs（PC / PS / Switch / Xbox 一套标准件），
    // 本文件不再自带任何临时键帽画法。

    // 键帽绘制只在通用库里，见 templates\Keycaps.cs

    // ---------- 输入 ----------
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (!busy && rcFix.Contains(pt)) return 1;
        if (rcLog.Contains(pt)) return 2;
        if (rcExit.Contains(pt)) return 3;
        if (rcCheats.Contains(pt)) return 5;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(Rectangle.Union(Rectangle.Union(rcStart, rcFix), rcLog), rcExit);
            return Rectangle.Union(r, rcCheats);
        }
    }

    void RepaintControls()
    {
        Invalidate(Rectangle.Inflate(ButtonsRect, S(8), S(8)));
        if (selMode != null) Invalidate(Rectangle.Inflate(selMode.Bounds, S(8), S(8)));
    }

    void PlaceSelect(Control c, Rectangle slot)
    {
        if (c == null) return;
        c.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);
    }

    SelectPanel MakeSelect(string[] names, int current, EventHandler open)
    {
        SelectPanel p = new SelectPanel();
        p.BackColor = Color.Transparent;
        p.ForeColor = Ink;
        p.Font = fRow;
        p.Text = names[current];
        p.Cursor = Cursors.Hand;
        p.Paint += PaintSelect;
        p.Click += open;
        Controls.Add(p);
        return p;
    }

    void PaintSelect(object sender, PaintEventArgs e)
    {
        Control p = (Control)sender;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle box = new Rectangle(0, 0, Math.Max(1, p.Width), Math.Max(1, p.Height));
        Color top = p.Enabled ? CardTop : Color.FromArgb(0x4A, 0x3A, 0x2E);
        Color bottom = p.Enabled ? Color.FromArgb(0x2A, 0x16, 0x0D) : Color.FromArgb(0x2E, 0x21, 0x19);
        using (GraphicsPath path = Round(box, S(8)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(box, top, bottom, LinearGradientMode.Vertical))
                e.Graphics.FillPath(b, path);
            using (Pen pen = new Pen(p.Enabled ? PanelEdge : Color.FromArgb(0x5A, 0x4A, 0x3A), 1f))
                e.Graphics.DrawPath(pen, path);
        }
        using (Pen hi = new Pen(Color.FromArgb(45, 255, 230, 180), 1f))
            e.Graphics.DrawLine(hi, S(10), S(2), p.Width - S(10), S(2));
        Rectangle tr = new Rectangle(S(12), 0, p.Width - S(36), p.Height);
        TextRenderer.DrawText(e.Graphics, p.Text, fRow, tr,
            p.Enabled ? Ink : Color.FromArgb(0xB8, 0xA5, 0x8E), Color.Transparent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using (Pen pen = new Pen(p.Enabled ? Gold : GoldDim, 1.6f))
        {
            int cx = p.Width - S(15), cy = p.Height / 2;
            e.Graphics.DrawLine(pen, cx - S(4), cy - S(2), cx, cy + S(2));
            e.Graphics.DrawLine(pen, cx, cy + S(2), cx + S(4), cy - S(2));
        }
    }

    void ShowModeMenu()
    {
        if (busy) return;
        if (modeMenu == null)
            modeMenu = BuildMenu(LaunchModeNames, delegate(int i)
            {
                launchMode = i;
                selMode.Text = LaunchModeNames[i];
                selMode.Invalidate();
                Log("选择启动方式：" + LaunchModeNames[i]);
            });
        ShowMenu(modeMenu, LaunchModeNames, launchMode, selMode);
    }

    void StepChoice(int which, int delta)
    {
        int n = LaunchModeNames.Length;
        launchMode = ((launchMode + delta) % n + n) % n;
        selMode.Text = LaunchModeNames[launchMode];
        selMode.Invalidate();
        Log("选择启动方式：" + LaunchModeNames[launchMode]);
    }

    ContextMenuStrip BuildMenu(string[] names, Action<int> pick)
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.ShowImageMargin = false;
        m.ShowCheckMargin = false;
        m.BackColor = LeatherMenuRenderer.MenuBack;
        m.ForeColor = Ink;
        m.Font = fRow;
        m.Padding = new Padding(S(4));
        m.Renderer = new LeatherMenuRenderer();
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            ToolStripMenuItem item = new ToolStripMenuItem(names[i]);
            item.ForeColor = Ink;
            item.Padding = new Padding(S(8), S(4), S(8), S(4));
            item.Click += delegate { pick(idx); };
            m.Items.Add(item);
        }
        return m;
    }

    void ShowMenu(ContextMenuStrip m, string[] names, int current, Control anchor)
    {
        for (int i = 0; i < m.Items.Count; i++)
            m.Items[i].Text = (i == current ? "✓  " : "    ") + names[i];
        m.Show(anchor, new Point(0, anchor.Height + S(2)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; RepaintControls(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hover != -1) { hover = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (colRight.Contains(e.Location)) { Focus(); }
        int i = HitTest(e.Location);
        if (i < 0) return;
        focus = i;
        Activate(i);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
            case Keys.Down:
                if (!busy && focus == 4) { StepChoice(focus, 1); return true; }
                break;
            case Keys.Up:
                if (!busy && focus == 4) { StepChoice(focus, -1); return true; }
                break;
            case Keys.Escape: Close(); return true;
            case Keys.Enter:
                if (busy) { Close(); return true; }
                if (focus >= 0) { Activate(focus); return true; }
                break;
            case Keys.Space:
                if (!busy && focus >= 0) { Activate(focus); return true; }
                break;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void CycleFocus(int delta)
    {
        int[] order = busy ? new int[] { 2, 3 } : new int[] { 4, 0, 1, 2, 3, 5 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
        if (i == 4) { ShowModeMenu(); return; }
        if (i == 5) { OpenCheatSheet(); return; }
        if (i == 0) { StartGame(); return; }
        if (i == 1) { FixResolution(); return; }
        if (i == 2) { OpenLog(); return; }
        if (i == 3) { Close(); }
    }

    // 「详细按键」按钮：开一个普通窗口，不用 ShowDialog，主窗口照样能点、能关、能启动游戏
    void OpenCheatSheet()
    {
        if (cheatSheet != null && !cheatSheet.IsDisposed)
        {
            cheatSheet.Activate();
            return;
        }
        cheatSheet = new CheatSheetForm(this);
        cheatSheet.Show();
        Log("打开完整按键速查窗口（普通窗口，非模态，位置错开主窗口）");
    }

    // ---------- 启动逻辑 ----------
    void StartGame()
    {
        if (busy) return;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到自带的 Python 运行环境";
            statusColor = Warn;
            Log("找不到 " + exePath + "（整合包的 python\\ 目录缺失，游戏没法启动）");
            Invalidate();
            return;
        }
        string dirs, gameDir, updateDir;
        if (!Sanctuary.ResolveDirs(AppDir(), out dirs, out gameDir, out updateDir))
        {
            status = "没找到游戏本体，点「打开运行日志」看详情";
            statusColor = Warn;
            Log("启动中止：" + dirs);
            Invalidate();
            return;
        }

        Sanctuary.GetDesktop(out desktopW, out desktopH, out desktopSource);
        int mode = launchMode;
        SetBusy(true, "正在启动…");
        Log("点「启动游戏」：" + LaunchModeNames[mode]);
        Thread t = new Thread(delegate ()
        {
            try
            {
                string msg;
                if (mode == 1)
                {
                    if (!Sanctuary.ApplySafeDisplay(AppDir(), desktopW, desktopH, out msg))
                    {
                        Log("画面设置失败：" + msg);
                        SetStatus("画面设置失败，请点「打开运行日志」", Warn);
                        if (autoStart) { exitCode = 1; FinishWith(1); }
                        return;
                    }
                    Log("画面：" + msg);
                }
                else
                {
                    Log("自动检测：使用游戏自己的画面设置");
                }
                Log("键位注入（战神3方案）：" + Sanctuary.KeyMapOf());
                SetStatus("游戏运行中…首次启动要多等一会儿", Ok);
                int code = RunGame(exePath);
                Log("游戏退出，退出码 0x" + ((uint)code).ToString("X8") + "，启动器随之退出");
                FinishWith(code);
            }
            catch (Exception ex)
            {
                Log("启动失败：" + ex);
                exitCode = 1;
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
                        Show();
                        WindowState = FormWindowState.Normal;
                    }));
                }
                catch { }
                SetStatus("启动失败：" + ex.Message + "（关窗退出）", Warn);
                if (autoStart) FinishWith(1);
            }
            finally
            {
                SetBusy(false, null);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    void FixResolution()
    {
        if (busy) return;
        SetBusy(true, "正在修复画面…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                Sanctuary.GetDesktop(out desktopW, out desktopH, out desktopSource);
                string msg;
                bool ok = Sanctuary.ApplySafeDisplay(AppDir(), desktopW, desktopH, out msg);
                Log("修复画面：" + msg);
                SetStatus(ok ? msg : "修复失败：" + msg, ok ? Ok : Warn);
            }
            catch (Exception ex)
            {
                Log("修复画面失败：" + ex);
                SetStatus("修复失败：" + ex.Message, Warn);
            }
            finally
            {
                SetBusy(false, null);
                try { BeginInvoke(new Action(Invalidate)); } catch { }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    int RunGame(string exePath)
    {
        // 真正的入口是整合包自带的 run.py：它做 CPU 指令集检查、把 1.47 更新合成临时视图、
        // 准备模组与着色器，最后拉起 sanctuary-probe / sanctuary-play 并陪跑到游戏退出。
        // 本启动器只负责把 SANCTUARY_* 环境变量设对，链路与整合包「开始游戏.exe」一致。
        string root = AppDir();
        ProcessStartInfo psi = new ProcessStartInfo(exePath, gameArgs);
        psi.WorkingDirectory = root;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;          // python.exe 是控制台程序，别让黑窗口弹出来
        // 2026-11-13 用户要求：控制台不再黑箱，输出转发到主窗口的「启动控制台」。
        // PYTHONIOENCODING 统一 python 与移植器两边的输出编码为 UTF-8：
        // probe 是 UTF-8，python 管道默认走 GBK，混着解必然一边乱码。
        Environment.SetEnvironmentVariable("PYTHONIOENCODING", "utf-8");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        Sanctuary.ApplyLaunchEnvironment(psi, root);
        string gameDir, updateDir, probe;
        Sanctuary.ResolveDirs(root, out probe, out gameDir, out updateDir);
        Log("启动链：" + gameExe + " " + gameArgs + "｜游戏本体 " + gameDir +
            "｜更新 " + (updateDir.Length > 0 ? updateDir : "（无）"));
        using (Process game = Process.Start(psi))
        {
            Log("启动进程 " + game.Id + "（python 壳），父进程 " + Process.GetCurrentProcess().Id +
                "；游戏本体挂在这条进程树下，退出码原样回传");
            // 两条读线程分别收 stdout / stderr，行进队列，UI 定时器批量刷控制台
            Thread ro = new Thread((ThreadStart)delegate { PumpReader(game.StandardOutput); });
            Thread re = new Thread((ThreadStart)delegate { PumpReader(game.StandardError); });
            ro.IsBackground = true; re.IsBackground = true;
            ro.Start(); re.Start();
            game.WaitForExit();
            try { ro.Join(1500); re.Join(1500); } catch { }
            PushConsole("—— 启动链退出，退出码 " + ((uint)game.ExitCode).ToString("X8") + " ——");
            return game.ExitCode;
        }
    }

    // 子进程输出行泵：逐行读进控制台队列；流关了就静默结束
    void PumpReader(System.IO.StreamReader reader)
    {
        try
        {
            string line;
            while ((line = reader.ReadLine()) != null) PushConsole(line);
        }
        catch { }
    }

    void FinishWith(int code)
    {
        exitCode = code;
        finishing = true;
        try
        {
            BeginInvoke(new Action(delegate
            {
                if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
                Close();
            }));
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy && !finishing)
        {
            e.Cancel = true;
            Log("玩家关窗，转入后台陪跑（游戏 pid 仍挂在本进程下）");
            GoToTray();
            return;
        }
        base.OnFormClosing(e);
    }

    void GoToTray()
    {
        if (tray == null)
        {
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = gameTitle + " · 游戏运行中";
            tray.Visible = true;
            tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; BringToFront(); Activate(); };
        }
        Hide();
    }

    void LoadGameIcon()
    {
        try
        {
            Icon ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (ic != null) { Icon = ic; return; }
        }
        catch { }
        Icon = SystemIcons.Application;
        Log("警告：exe 未嵌入统一启动器图标（构建时 assets\\ico.ico 缺失）");
    }

    static int RunAndWait(string dir, string file, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false;
        using (Process p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    static int SelfTest(string[] args)
    {
        try
        {
            string dir = AppDir();
            string file = args[0];
            string rest = "";
            for (int i = 1; i < args.Length; i++) rest += (rest.Length == 0 ? "" : " ") + args[i];
            if (!Path.IsPathRooted(file))
            {
                string local = Path.Combine(dir, file);
                if (File.Exists(local)) file = local;
            }
            return RunAndWait(dir, file, rest);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    void SetBusy(bool value, string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), value, text); return; }
        busy = value;
        hover = -1;
        if (value) focus = NoFocus;
        if (selMode != null) { selMode.Enabled = !value; selMode.Invalidate(); }
        if (text != null) { status = text; statusColor = value ? Ok : statusColor; }
        RepaintControls();
    }

    void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string, Color>(SetStatus), text, color); return; }
        status = text;
        statusColor = color;
        Invalidate();
    }

    static string AppDir() { return Path.GetDirectoryName(Application.ExecutablePath); }

    static readonly object logGate = new object();
    static string LogPath { get { return Path.Combine(AppDir(), "运行日志.txt"); } }

    static void Log(string msg)
    {
        // 启动器自己的日志也转推一份到主窗口控制台（▪ 前缀区分来源），
        // 玩家不用开记事本就能看完整启动链
        PushConsole("▪ " + msg);
        try
        {
            lock (logGate)
            {
                string p = LogPath;
                FileInfo fi = new FileInfo(p);
                if (!fi.Exists)
                    File.WriteAllText(p, "运行日志：游戏出问题时，这里记着具体的出错信息\r\n", Encoding.UTF8);
                else if (fi.Length > 200000)
                {
                    string[] all = File.ReadAllLines(p, Encoding.UTF8);
                    int keepN = Math.Min(all.Length, 400);
                    string[] keep = new string[keepN];
                    Array.Copy(all, all.Length - keepN, keep, 0, keepN);
                    File.WriteAllLines(p, keep, Encoding.UTF8);
                }
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", Encoding.UTF8);
            }
        }
        catch { }
    }

    void OpenLog()
    {
        try
        {
            Log("玩家点开运行日志");
            ProcessStartInfo psi = new ProcessStartInfo("notepad.exe", "\"" + LogPath + "\"");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log("日志打不开：" + ex.Message);
            status = "日志打不开：" + ex.Message;
            statusColor = Warn;
            RepaintControls();
        }
    }

    void LoadIni()
    {
        string path = Path.Combine(AppDir(), "launcher.ini");
        if (!File.Exists(path)) return;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim().Replace("\\n", "\r\n");
            switch (key)
            {
                case "title": gameTitle = val; break;
                case "gameexe": gameExe = val; break;
                case "gameargs": gameArgs = val; break;
                                case "intro": intro = val; break;
                                case "centernote": centerNote = val; break;
                                case "combohint": comboHint = val; break;
            }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (fTitle != null) fTitle.Dispose();
        if (fHead != null) fHead.Dispose();
        if (fRowSel != null) fRowSel.Dispose();
        if (fRow != null) fRow.Dispose();
        if (fBody != null) fBody.Dispose();
        if (fCap != null) fCap.Dispose();
        if (fCapZh != null) fCapZh.Dispose();
        if (fMono != null) fMono.Dispose();
        if (fBrand != null) fBrand.Dispose();
        if (modeMenu != null) { modeMenu.Dispose(); modeMenu = null; }
        if (cheatSheet != null)
        {
            try { cheatSheet.Close(); } catch { }
            cheatSheet = null;
        }

        base.OnFormClosed(e);
    }

    class SelectPanel : Panel
    {
        public SelectPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }
    }

    class LeatherMenuRenderer : ToolStripProfessionalRenderer
    {
        public static readonly Color MenuBack = Color.FromArgb(0x2A, 0x16, 0x0D);
        static readonly Color MenuHot = Color.FromArgb(0x6B, 0x42, 0x28);
        static readonly Color MenuEdge = Color.FromArgb(0xB9, 0x8A, 0x55);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(MenuBack))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            using (Pen hi = new Pen(Color.FromArgb(40, 255, 220, 160), 1f))
                e.Graphics.DrawLine(hi, 1, 1, e.ToolStrip.Width - 2, 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush b = new SolidBrush(e.Item.Selected ? MenuHot : MenuBack))
                e.Graphics.FillRectangle(b, r);
            if (e.Item.Selected)
                using (SolidBrush bar = new SolidBrush(Gold))
                    e.Graphics.FillRectangle(bar, new Rectangle(r.X + 2, r.Y + 3, 2, Math.Max(1, r.Height - 6)));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? GoldBright : Ink;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(MenuEdge, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    // ---------- 完整按键速查：独立普通窗口（用户定案 2026-11-05） ----------
    // 主窗口回归规范三栏 900×620，长表搬到这里。用 Show() 不用 ShowDialog()：
    // 非模态、主窗口照常能点；位置自动错开放在主窗口旁边，不盖住主窗口。
    // 绘制、键帽、数据全部复用主窗口那套方法，不另抄一份，避免两边漂移。
    class CheatSheetForm : Form
    {
        readonly Launcher owner;
        readonly Point hostHome;
        int scroll, scrollMax;
        Size loggedSize;

        public CheatSheetForm(Launcher host)
        {
            owner = host;
            hostHome = host.Location;
            Text = "暗黑破坏神III 夺魂之镰 终极邪恶版 · 完整按键速查";
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScaleMode = AutoScaleMode.None;
            // 允许玩家最大化 / 拖边框放大（用户定案 2026-11-05）：绘制按 ClientSize 自适应重排两列，
            // 放大后说明更容易一行放下，仍争取一屏看全。主窗口仍是 FixedDialog 不许放大。
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = BgMid;
            KeyPreview = true;
            PlaceBesideHost();
        }

        // 两窗口并排：主窗口让位到工作区左侧，详细窗口占右侧全部剩余宽度。
        // 这样详细窗口够宽（两列、说明一行、争取一屏看全），又完全不盖住主窗口。
        void PlaceBesideHost()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int gap = owner.S(12);
            int hostW = owner.Bounds.Width;
            int w = Math.Min(wa.Width - hostW - gap * 3, owner.S(1400));
            int h = wa.Height - gap * 2;
            if (w < owner.S(700))
            {
                // 屏幕太窄并排不下：退化为贴右的大窗口，仍是非模态普通窗口
                w = Math.Min(owner.S(1400), wa.Width - gap * 2);
                Size = new Size(w, h);
                Location = new Point(wa.Right - w - gap, wa.Top + gap);
                return;
            }
            Size = new Size(w, h);
            owner.Location = new Point(wa.Left + gap, Math.Max(wa.Top, hostHome.Y));
            Location = new Point(owner.Bounds.Right + gap, wa.Top + gap);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PlaceBesideHost();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            owner.DrawLeatherBg(g, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            int pad = owner.S(8);
            Rectangle col = new Rectangle(pad, pad, ClientSize.Width - pad * 2, ClientSize.Height - pad * 2);
            owner.Card(g, col);
            Region oldClip = g.Clip;
            g.SetClip(Rectangle.Inflate(col, -owner.S(6), -owner.S(6)), CombineMode.Replace);
            int top = col.Y + owner.S(6), bottom = col.Bottom - owner.S(6);
            int startY = col.Y + owner.S(8) - scroll;
            int endY = owner.DrawCheatContent(g, col, startY, top, bottom);
            int contentH = endY - startY;
            int viewport = col.Height - owner.S(8) - owner.S(8);
            int max = Math.Max(0, contentH - viewport);
            scrollMax = max;
            // 尺寸变化（含玩家最大化 / 拖边框）都记一行，实机可确认放大后仍需滚动 0
            if (loggedSize != ClientSize)
            {
                loggedSize = ClientSize;
                Log("详细按键窗口：" + ClientSize.Width + "×" + ClientSize.Height +
                    "，内容高 " + contentH + "，视口 " + viewport + "，需滚动 " + max + "px");
            }
            int clamped = Math.Max(0, Math.Min(scroll, max));
            if (clamped != scroll) { scroll = clamped; Invalidate(); }
            g.Clip = oldClip;
            owner.DrawScrollBar(g, col.Right - owner.S(9), col.Y + owner.S(10),
                col.Height - owner.S(20), scroll, max, contentH, viewport);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(-e.Delta);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.PageDown: ScrollBy(ClientSize.Height - owner.S(90)); return true;
                case Keys.PageUp: ScrollBy(-(ClientSize.Height - owner.S(90))); return true;
                case Keys.Home: ScrollBy(-1000000); return true;
                case Keys.End: ScrollBy(1000000); return true;
                case Keys.Escape: Close(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void ScrollBy(int delta)
        {
            int v = Math.Max(0, Math.Min(scroll + delta, scrollMax));
            if (v == scroll) return;
            scroll = v;
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (owner.cheatSheet == this) owner.cheatSheet = null;
            // 详细窗口关掉后把主窗口放回原位（启动仍是规范要求的屏幕居中）
            try { if (!owner.IsDisposed) owner.Location = hostHome; } catch { }
        }
    }

    // ---------- 显示守卫 ----------
    static class Sanctuary
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        public static void EnableDpiAwareness()
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }
        }

        public static void GetDesktop(out int w, out int h, out string source)
        {
            w = 0; h = 0; source = "未知";
            try
            {
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, -1, ref dm) && dm.dmPelsWidth > 0 && dm.dmPelsHeight > 0)
                {
                    w = dm.dmPelsWidth; h = dm.dmPelsHeight; source = "EnumDisplaySettings";
                    return;
                }
            }
            catch { }
            try
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                if (b.Width > 0 && b.Height > 0)
                {
                    w = b.Width; h = b.Height; source = "Screen";
                }
            }
            catch { }
        }

        public static string Describe(int w, int h)
        {
            if (w <= 0 || h <= 0) return "桌面分辨率";
            return w + "×" + h;
        }

        // ---------- 目录探测（自愈式：固定路径优先，缺失时向下扫一层） ----------
        // 整合包布局：game\CUSA00242 是游戏本体（含 eboot.bin），game\CUSA00242-UPDATE
        // 是 1.47 更新（run.py 的 game_update 会把它合成临时覆盖视图，不改两个原始目录）。
        // 换机器 / 目录被整理过也能找到：扫一层含 eboot.bin 的目录兜底。
        static string resolvedGame, resolvedUpdate;
        static bool resolved;

        // 四参轻量版：只取探测结果文本（供启动日志）
        public static bool ResolveDirs(string root, out string message)
        {
            string g, u;
            bool ok = ResolveDirs(root, out message, out g, out u);
            return ok;
        }

        public static bool ResolveDirs(string root, out string message, out string gameDir, out string updateDir)
        {
            if (resolved)
            {
                gameDir = resolvedGame; updateDir = resolvedUpdate;
                message = "游戏本体 " + resolvedGame + "｜更新 " +
                    (resolvedUpdate.Length > 0 ? resolvedUpdate : "（无）");
                return gameDir.Length > 0;
            }
            gameDir = ""; updateDir = "";
            try
            {
                // 游戏本体：固定路径 → game\ 下逐个找 → 根目录下逐个找（跳过更新目录）
                string fixedGame = Path.Combine(root, Path.Combine("game", "CUSA00242"));
                if (HasEboot(fixedGame)) gameDir = fixedGame;
                if (gameDir.Length == 0) gameDir = FindEbootDir(Path.Combine(root, "game"), "*");
                if (gameDir.Length == 0)
                    gameDir = FindEbootDir(root, "*", delegate(string name) { return name.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) < 0; });
                // 更新：固定名 → 名字带 UPDATE 的目录（可选，没有也能跑，就是用的旧版本内容）
                string fixedUpdate = Path.Combine(root, Path.Combine("game", "CUSA00242-UPDATE"));
                if (HasEboot(fixedUpdate) && NotSameDir(fixedUpdate, gameDir)) updateDir = fixedUpdate;
                if (updateDir.Length == 0)
                {
                    string found = FindEbootDir(Path.Combine(root, "game"), "*UPDATE*");
                    if (found.Length > 0 && NotSameDir(found, gameDir)) updateDir = found;
                }
            }
            catch (Exception ex)
            {
                Log("探测游戏目录出错：" + ex.Message);
            }
            resolvedGame = gameDir; resolvedUpdate = updateDir; resolved = true;
            message = "游戏本体 " + (gameDir.Length > 0 ? gameDir : "没找到（要有 eboot.bin）") +
                "｜更新 " + (updateDir.Length > 0 ? updateDir : "（无，用旧版内容也能玩）");
            return gameDir.Length > 0;
        }

        static bool HasEboot(string dir)
        {
            return dir.Length > 0 && File.Exists(Path.Combine(dir, "eboot.bin"));
        }

        static bool NotSameDir(string a, string b)
        {
            return a.Length > 0 && b.Length > 0 &&
                !string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        static string FindEbootDir(string parent, string pattern)
        {
            return FindEbootDir(parent, pattern, null);
        }

        static string FindEbootDir(string parent, string pattern, Predicate<string> accept)
        {
            try
            {
                if (!Directory.Exists(parent)) return "";
                DirectoryInfo[] dirs = new DirectoryInfo(parent).GetDirectories(pattern);
                Array.Sort(dirs, delegate(DirectoryInfo a, DirectoryInfo b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
                foreach (DirectoryInfo d in dirs)
                    if ((accept == null || accept(d.Name)) && File.Exists(Path.Combine(d.FullName, "eboot.bin")))
                        return d.FullName;
            }
            catch { }
            return "";
        }

        // 画面异常时的兜底：无边框 + 原生渲染分辨率。sanctuary.ini 是无节的平键值文件
        //（run.py 的 graphics_options 就是这么读的），缺文件时按移植器的做法补一个带头注释的。
        // 暗黑3 的 D3 profile 已把 vsync / readbacks 定死在代码里（SANCTUARY_MENU_HIDE），
        // 这些键不写——写了也会被 profile 的环境变量盖掉，白改还误导人。
        public static bool ApplySafeDisplay(string dir, int w, int h, out string message)
        {
            string path = Path.Combine(dir, "sanctuary.ini");
            try
            {
                string nl; bool finalNewline;
                List<string> lines;
                if (File.Exists(path))
                {
                    lines = ReadIniLines(path, out nl, out finalNewline);
                }
                else
                {
                    lines = new List<string>();
                    lines.Add("# sanctuary settings (in-game menu: Insert / R3+L2)");
                    nl = "\r\n"; finalNewline = true;
                }
                bool changed = false;
                SetFlat(lines, "display_mode", "borderless", ref changed);
                SetFlat(lines, "render_resolution", "native", ref changed);
                if (changed)
                {
                    Backup(path, dir);
                    WriteIniLines(path, lines, nl, finalNewline);
                    message = "已改为无边框全屏、桌面 " + Describe(w, h) + " 原生画面";
                }
                else
                {
                    message = "画面已就绪：无边框全屏，桌面 " + Describe(w, h);
                }
                return true;
            }
            catch (Exception ex)
            {
                message = "写画面设置失败：" + ex.Message;
                Log("写 sanctuary.ini 失败：" + ex);
                return false;
            }
        }

        // ---------- 键位表（按战神III重制版方案，2026-11-13 用户定案） ----------
        // 按钮名必须是移植器认识的（二进制按钮表实测：cross circle square triangle
        // l1 r1 l2 r2 l3 r3 options touchpad touchpad_left touchpad_right up down left right），
        // 不认识的名字会被移植器忽略并记 "pad map: ignored"。
        // 动作语义照暗黑3主机版官方布局：✕=主要攻击、R2=次要攻击、□△○=三个技能位、
        // L1/R1=菜单页签左右翻、十字键=拾取装备轮换、右摇杆=翻滚闪避。
        // l3/r3 故意不列：保留移植器默认 Z/X（走跑切换等暗黑3实用功能）。
        static readonly string[,] Actions =
        {
            { "square", "J" },
            { "triangle", "K" },
            { "circle", "L" },
            { "cross", "Space" },
            { "l1", "Q" },
            { "r1", "E" },
            { "l2", "U" },
            { "r2", "O" },
            { "options", "Tab" },
            { "touchpad_left", "P" },
            { "touchpad_right", "Escape" },
            { "up", "1" },
            { "down", "3" },
            { "left", "4" },
            { "right", "2" }
        };

        // 传给移植器的键位映射串：square=J,triangle=K,...（与战神3启动器同格式）
        public static string KeyMapOf()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Actions.GetLength(0); i++)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(Actions[i, 0]).Append('=').Append(Actions[i, 1]);
            }
            return sb.ToString();
        }

        // 拉起启动链时要传给移植器的环境变量（与 run.py 的默认约定逐项对齐）。
        // 键位按战神3重制版方案注入 SANCTUARY_KEY_MAP；玩家后续仍可在游戏里改键。
        // 注意不能碰 psi.EnvironmentVariables：很多机器（含本机）环境里同时挂着
        // HTTPS_PROXY 和 https_proxy 这类大小写不同的重复键，.NET 的 StringDictionary
        // 一访问就抛「已添加项」把启动炸掉（2026-11-13 实测）。
        // 改成把变量设进当前进程环境，子进程原样继承，语义等价且不炸。
        public static void ApplyLaunchEnvironment(ProcessStartInfo psi, string root)
        {
            string gameDir, updateDir, msg;
            ResolveDirs(root, out msg, out gameDir, out updateDir);
            Put("SANCTUARY_GAME_DIR", gameDir);
            Put("SANCTUARY_PATCH_DIR", updateDir);   // 空则删除，恢复「无更新」语义
            Put("SANCTUARY_CONFIG", Path.Combine(root, "sanctuary.ini"));
            Put("SANCTUARY_DATA_DIR", root);
            Put("SANCTUARY_KEY_MAP", KeyMapOf());
        }

        static void Put(string name, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) Environment.SetEnvironmentVariable(name, null);
                else Environment.SetEnvironmentVariable(name, value);
            }
            catch (Exception ex)
            {
                Log("设置环境变量 " + name + " 失败（继续）：" + ex.Message);
            }
        }

        // 平键值文件（无 [节]）里改一个键：相同不动、不同只改那一个、缺了追加到末尾
        static void SetFlat(List<string> lines, string key, string value, ref bool changed)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    string oldValue = t.Substring(eq + 1).Trim();
                    if (!string.Equals(oldValue, value, StringComparison.Ordinal))
                    {
                        lines[i] = key + "=" + value;
                        changed = true;
                    }
                    return;
                }
            }
            lines.Add(key + "=" + value);
            changed = true;
        }

        static List<string> ReadIniLines(string path, out string nl, out bool finalNewline)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            nl = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            finalNewline = text.EndsWith("\n", StringComparison.Ordinal);
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> lines = new List<string>(raw);
            if (finalNewline && lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        static void WriteIniLines(string path, List<string> lines, string nl, bool finalNewline)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Count - 1 || finalNewline) sb.Append(nl);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        static void Backup(string path, string dir)
        {
            try
            {
                string bak = Path.Combine(dir, Path.Combine("_legacy_compat_backup", "DiabloIII"));
                Directory.CreateDirectory(bak);
                string dst = Path.Combine(bak, Path.GetFileName(path));
                if (!File.Exists(dst)) File.Copy(path, dst);
            }
            catch (Exception ex)
            {
                Log("备份设置文件失败（继续）：" + ex.Message);
            }
        }

    }
}
