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

// 命令与征服：红色警戒（OpenRA playtest-20261026 简体中文便携整合版）启动器 game_starter.exe。
// 界面规范：仓库 .qoder/skills/legacy-launcher-gui/SKILL.md + references/leather-recipes.md
//（纯 GDI+ 自绘、三栏 900×620、拟物皮革、全窗口 TextRenderer ClearType、键帽走通用库 Keycaps.cs）。
// 本文件以 Games\PokemonSV\src\Launcher.cs 为底本外科式移植：单按钮启动（本作只有一个 ra mod），
// 没有兼容层、不写系统、不改刷新率（现代 .NET 10 自包含引擎，SDL2 + OpenGL/ANGLE）。
// 右栏一屏放全按键说明（用户定案 2026-11-20：本作内容不多，不开独立窗口）。
//
// 已确认的事实（2026-11-20，取证见 Games\CC1RedAlert\evidence\）：
//   自带 启动游戏.exe 是 .NET Framework 托管启动器，ilspycmd 反编译拿到启动契约：
//   主程序 Engine\RedAlert.exe，工作目录 = Engine；
//   参数 = Game.Mod=ra + "Engine.LaunchPath=<Engine\RedAlert.exe 全路径>"（MSVCRT 引号规则）；
//   启动前创建 Engine\Support，settings.yaml 缺失时写默认（窗口 1280×800、限帧 60、现代鼠标方案）。
//   本启动器复刻这些绿色动作，另加一条用户定案的功能：每次启动前把本机电脑名写进
//   settings.yaml 的 Player: Name:（联机显示名，网吧按机器名自动设置；只改这一行，其余不碰）。
//   不复刻自带版的两样东西：① ra.ftl / mod.yaml 的 XDGAME 标题改写（那是他们的品牌，
//   发行包已改好的现状保持不动）；② TitleProtection 每 500ms 强改游戏窗口标题（违反规则 44 零干预）。
//   键位表照自带 游戏启动说明.txt 逐条抄（默认「现代」操作方案），不自编、不改键。
//   settings.yaml 由游戏内设置读写，除 Player: Name: 外玩家改过的设置永远不覆盖。
//
// 命令行开关（都不需要玩家碰，给平台和远程排障用；规则 42 一律 -- 前缀，单斜杠 /xxx 继续兼容）：
//   --go            零交互直接启动（平台入口）
//   --hideparent    藏掉父控制台
//   --readings      只采现场读数写日志、不启动游戏（换机器先跑这条）
//   --checkfiles    只检查 Engine 核心文件并写日志、不启动游戏
//   --wer "<Report.wer>" [结论另存文件]  只解析崩溃报告（结论必须另存 UTF-8 文件再断言，别读 stdout 中文）
//   --selftest <程序> [参数…]            直接拉起该程序并原样回传退出码（不启动游戏，用来验托管契约）
//
// 平台托管契约（规则 8 / 技能「平台托管契约」）：游戏用 CreateProcess 拉起，是 game_starter.exe 的**直接子进程**、
// 工作目录 = Engine；游戏运行期间本进程陪跑（主窗口保持可见）；游戏退出后本进程退出并**原样回传游戏退出码**；
// 玩家关窗口只是隐藏到托盘陪跑，不杀游戏。对游戏窗口零干预（规则 44）。
// 陪跑带看门狗：游戏连续 45s 无任何可见/最小化窗口即判卡死，补送 WM_CLOSE → 10s 不退再结束进程并写日志。

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革，与 leather-recipes.md 逐字一致） ----------
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

    // 游戏文件缺失时给玩家看的那一句人话（规则 34：不出现 bat 名字、不出现「管理员」、不指挥玩家做任何一步）
    const string FailFilesText = "游戏文件不完整，请把运行日志发给我们";

    // ---------- launcher.ini 可覆盖 ----------
    string gameTitle = "命令与征服：红色警戒1复刻版-网吧联机版";
    string gameSubtitle = "OpenRA playtest-20261026 · 简体中文便携整合版";
    string intro =
        "本游戏包含所有红色警戒1内容：\n" +
        "本体 + 危机反击 Counterstrike\n" +
        "+ 劫后余生 The Aftermath\n" +
        "+ OpenRA 扩展任务。\n" +
        "\n" +
        "单机玩：单人游戏 -\n" +
        "遭遇战或战役任务。\n" +
        "\n" +
        "联机玩：\n" +
        "主机：多人游戏 - 创建 -\n" +
        "更换底图 - 创建。\n" +
        "客机：多人游戏 - 找到主机\n" +
        "双击加入。\n" +
        "设置和存档都在游戏文件夹内，\n" +
        "移动或备份时保留整个文件夹。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」把内容发给我们";
    string centerNote = "联机显示名自动使用本机电脑名。";

    // ---------- 游戏路径（一切相对 exe 所在目录，规则 43.1） ----------
    static string EngineDir() { return Path.Combine(AppDir(), "Engine"); }
    static string GameExePath() { return Path.Combine(EngineDir(), "RedAlert.exe"); }
    static string SupportDir() { return Path.Combine(EngineDir(), "Support"); }

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, filesOk = true;
    string status = "已就绪";
    Color statusColor = Ok;
    volatile int exitCode;
    volatile bool finishing;
    NotifyIcon tray;
    bool autoStart;

    // ---------- 字体 / 布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcCenterHead, rcCenterNote;
    Rectangle rcStart, rcLog, rcExit;

    // ---------- 键位表（照自带 游戏启动说明.txt 逐条抄，默认「现代」操作方案，不自编、不改键） ----------
    // 本作是纯 PC 键鼠游戏，没有手柄映射：Glyph 一律 "—"（不画深色帽），金色键帽 = 玩家真要按的键。
    class CheatRow
    {
        public string Glyph;
        public string Pc;
        public string Text;
        public CheatRow(string glyph, string pc, string text) { Glyph = glyph; Pc = pc; Text = text; }
    }

    // 右栏一屏放全说明（用户定案 2026-11-20：不开独立窗口）。
    // 同类动作合并一行、一帽一键；说明压到一行省高度，键位一个不丢。
    static readonly CheatRow[] QuickRows =
    {
        new CheatRow("—", "pc:Mouse1 pc:Mouse2", "选择 / 框选 / 放置建筑；移动 / 攻击"),
        new CheatRow("—", "pc:Mouse3 pc:WheelUp pc:WheelDown", "中键平移视野；滚轮放大 - 缩小界面"),
        new CheatRow("—", "pc:A pc:S pc:D pc:F", "攻击移动 / 停止 / 警戒 / 部署"),
        new CheatRow("—", "pc:Q pc:W", "选全部作战单位 / 同类单位"),
        new CheatRow("—", "pc:Up pc:Down pc:Left pc:Right pc:H pc:Space", "移视野 / 回基地 / 最近事件"),
        new CheatRow("—", "pc:Ctrl pc:1", "Ctrl+数字编队，数字选编队"),
        new CheatRow("—", "pc:F1 pc:F12", "F1–F12 生产栏快捷键"),
        new CheatRow("—", "pc:Esc", "菜单：目标 / 保存 / 读取 / 设置"),
        new CheatRow("—", "pc:Ctrl pc:P pc:Enter pc:M", "截图 / 快速保存(小键盘) / 静音")
    };

    // ==========================================================================
    // 入口
    // ==========================================================================
    [STAThread]
    static int Main(string[] args)
    {
        // 规则 42：--xxx 在入口归一化成 /xxx，内部逻辑只认一种写法；
        // 日志显示原始参数（rawArgs），排障时看清平台到底传了什么。
        string[] rawArgs = (string[])args.Clone();
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith("--")) args[i] = "/" + args[i].Substring(2);
        foreach (string a in rawArgs)
            if (a.StartsWith("/") && a.IndexOf(":/") >= 0)
                Log("【警告】参数疑似被 shell 路径转换：" + a);

        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();
        Application.EnableVisualStyles();

        Launcher l = new Launcher();
        foreach (string a in args)
            if (a == "/go" || a == "-go" || a == "/auto") l.autoStart = true;
        string parentName = ParentProcessName();
        Log("启动参数：" + (rawArgs.Length == 0 ? "（无）" : string.Join(" ", rawArgs)) +
            "；父进程：" + (parentName.Length == 0 ? "未知" : parentName) +
            "；零交互：" + (l.autoStart ? "是" : "否"));

        foreach (string a0 in args)
        {
            // --readings 只读数、--checkfiles 只查文件、--wer 只解析崩溃报告、--selftest 只跑指定的小工具：都不启动游戏
            if (a0 == "/readings" || a0 == "-readings") return DumpReadings();
            if (a0 == "/checkfiles" || a0 == "-checkfiles")
            {
                string msg;
                bool ok = l.CheckAllFiles(out msg);
                Console.WriteLine(ok ? "FILES OK" : "FILES FAIL");
                return ok ? 0 : 1;
            }
            if (a0 == "/selftest" || a0 == "-selftest") return SelfTest(Tail(args));
            if (a0 == "/wer")
            {
                if (args.Length < 2 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe --wer \"<Report.wer 路径>\" [结论另存的 UTF-8 文件]");
                    return 2;
                }
                string line = "崩溃捕获：" + ParseWer(args[1]);
                // 结论同时另存一份 UTF-8：控制台代码页五花八门，脚本要断言的是那份字节，不是 stdout
                if (args.Length > 2)
                {
                    try { File.AppendAllText(args[2], line + "\r\n", new UTF8Encoding(false)); }
                    catch (Exception ex) { Console.WriteLine("结论另存失败：" + ex.Message); }
                }
                Console.WriteLine(line);
                return line.IndexOf("没读到") >= 0 || line.IndexOf("没认出") >= 0 ? 3 : 0;
            }
        }

        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        Application.Run(l);
        return l.exitCode;
    }

    static string[] Tail(string[] a)
    {
        string[] r = new string[a.Length - 1];
        Array.Copy(a, 1, r, 0, r.Length);
        return r;
    }

    // ==========================================================================
    // 父进程 / 藏控制台（--hideparent：只对 cmd / powershell / pwsh 动手，不碰游戏窗口）
    // ==========================================================================
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
    static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")]
    static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")]
    static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr h, int cmd);
    // 注意：SetForegroundWindow / AllowSetForegroundWindow 已按 AGENTS 规则 44 删除——
    // 启动器对游戏窗口零干预（外部进程任何前台操作都会和引擎的全屏切换打架，坑 114）。

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

    // ==========================================================================
    // 构造与布局（规则 28：最终尺寸在构造函数里一步算完，CenterScreen 才量得准）
    // ==========================================================================
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
        BackColor = BgBottom;

        // 句柄还没建，DPI 从桌面 DC 取；取不到（0 或离谱值）一律退回 1.0，
        // 否则 S() 会把所有尺寸算成 0，字体尺寸 0 直接抛异常、界面全塌
        float rawDpi = 96f;
        try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) rawDpi = g.DpiX; }
        catch { }
        dpi = rawDpi > 0f ? rawDpi / 96f : 1f;
        if (dpi < 0.5f || dpi > 4f) dpi = 1f;
        MakeFonts();
        Keycaps.Configure(dpi, fCap, fCapZh);
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        ClientSize = new Size(Math.Min(S(900), wa.Width - S(30)), Math.Min(S(620), wa.Height - S(30)));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        LayoutRects();
        string msg;
        filesOk = CheckAllFiles(out msg);
        status = msg;
        statusColor = filesOk ? Ok : Warn;
        LoadGameIcon();
        Log("界面 DPI 缩放=" + dpi.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            "（DpiX=" + Math.Round(dpi * 96f) + "），窗口客户区 " + ClientSize.Width + "×" + ClientSize.Height);
        if (autoStart) BeginInvoke(new Action(delegate { StartGame(); }));
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
        int pad = S(20), gut = S(16);
        int c1 = S(252), c2 = S(296);
        int c3 = ClientSize.Width - pad * 2 - c1 - c2 - gut * 2;
        int top = S(88), bottom = ClientSize.Height - S(68);
        colLeft = new Rectangle(pad, top, c1, bottom - top);
        colCenter = new Rectangle(pad + c1 + gut, top, c2, bottom - top);
        colRight = new Rectangle(pad + c1 + gut + c2 + gut, top, c3, bottom - top);

        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        int headH = S(22);
        int noteH = MeasureWrappedHeight(centerNote, innerW, fBody);
        // 中栏一叠：标题 → 一句说明 → 启动游戏 → 日志 → 退出。
        // 标题与左右栏同一水平线；内容少时不垂直居中，否则顶部一大块空洞
        //（规则 20：不上下拉出大段空白）。
        int y = colCenter.Y + S(12);

        rcCenterHead = new Rectangle(inner, y, innerW, headH);
        rcCenterNote = new Rectangle(inner, rcCenterHead.Bottom + S(8), innerW, noteH);
        rcStart = new Rectangle(inner, rcCenterNote.Bottom + S(18), innerW, S(44));
        rcLog = new Rectangle(inner, rcStart.Bottom + S(16), innerW, S(34));
        rcExit = new Rectangle(inner, rcLog.Bottom + S(10), innerW, S(34));
    }

    // ==========================================================================
    // 绘制
    // ==========================================================================
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        DrawLeatherBg(g, all);
        DrawBand(g, new Rectangle(0, 0, ClientSize.Width, S(76)));
        int footY = ClientSize.Height - S(60);
        DrawBand(g, new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY));

        DrawHeader(g);
        DrawLeft(g);
        DrawCenter(g);
        DrawRight(g);
        DrawFooter(g);
    }

    // 背景：竖直渐变 + 两组反向 1px 斜线当皮纹 + PathGradientBrush 压暗四角（全程现画，不引图片）
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
        TextRenderer.DrawText(g, gameSubtitle, fMono, new Point(colLeft.X, S(48)), InkDim, Color.Transparent);
        DrawDivider(g, S(76));
    }

    void DrawDivider(Graphics g, int y)
    {
        using (Pen p = new Pen(GoldDim, 1f))
            g.DrawLine(p, colLeft.X, y, ClientSize.Width - S(20), y);
        using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
            g.DrawLine(sh, colLeft.X, y + 1, ClientSize.Width - S(20), y + 1);
    }

    // 左栏只讲游戏本身 + 「玩不了或报错」那一句（规则 19）
    void DrawLeft(Graphics g)
    {
        Card(g, colLeft);
        int x = colLeft.X + S(16), y = colLeft.Y + S(14);
        TextRenderer.DrawText(g, "关于这款游戏", fHead, new Point(x, y), Gold, Color.Transparent);
        DrawWrapped(g, intro, x, y + S(30), colLeft.Width - S(32), fBody, Ink);
    }

    void DrawCenter(Graphics g)
    {
        Card(g, colCenter);
        TextRenderer.DrawText(g, "启动说明", fHead, new Point(rcCenterHead.X, rcCenterHead.Y), Gold, Color.Transparent);
        DrawWrapped(g, centerNote, rcCenterNote.X, rcCenterNote.Y, rcCenterNote.Width, fBody, Ink);
        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏",
            true, !busy && filesOk, hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcLog, "打开运行日志", false, true, hover == 2 || focus == 2, focus == 2);
        DrawButton(g, rcExit, "退出", false, true, hover == 3 || focus == 3, focus == 3);
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

    // 右栏：一屏放全操作说明（用户定案 2026-11-20：不开独立窗口）；滚动只作兜底
    void DrawRight(Graphics g)
    {
        Card(g, colRight);
        int top = colRight.Y + S(6);
        int bottom = colRight.Bottom - S(6);
        int startY = colRight.Y + S(10) - rightScroll;
        int endY = DrawRightContent(g, startY, top, bottom);
        int contentH = endY - startY;
        int viewport = colRight.Height - S(14);   // 无底部按钮，整栏高度都给内容
        rightScrollMax = Math.Max(0, contentH - viewport);
        int clamped = Math.Max(0, Math.Min(rightScroll, rightScrollMax));
        if (clamped != rightScroll) { rightScroll = clamped; Invalidate(colRight); }
        DrawRightScroll(g, viewport, contentH);
        LogRightTableFit(contentH, viewport);
    }

    bool rightFitLogged;
    void LogRightTableFit(int contentH, int viewport)
    {
        if (rightFitLogged) return;
        rightFitLogged = true;
        Log("右栏按键表自检：行数=" + QuickRows.Length + " 内容高=" + contentH +
            " 视口高=" + viewport + " 需要滚动=" + (contentH > viewport));
    }

    int DrawRightContent(Graphics g, int y, int top, int bottom)
    {
        int x = colRight.X + S(14), w = colRight.Width - S(28);
        int headH = TextRenderer.MeasureText(g, "国", fHead).Height;
        // 表的看法写在右栏自己头上（一行就够；不写翻页提示）
        if (InViewport(y, headH, top, bottom))
            TextRenderer.DrawText(g, "操作说明 · 默认「现代」方案", fHead, new Point(x, y), Gold, Color.Transparent);
        y += headH + S(4);
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height;
        int capH = S(20);
        foreach (CheatRow r in QuickRows)
        {
            int gw = Keycaps.GroupWidth(g, r.Glyph, capH);
            if (InViewport(y, capH, top, bottom))
            {
                Keycaps.DrawGroup(g, r.Glyph, x, y, capH, false);
                Keycaps.DrawGroup(g, r.Pc, x + gw + S(6), y, capH, true);
            }
            y += capH + S(1);
            foreach (string ln in WrapLines(g, r.Text, w, fBody))
            {
                if (InViewport(y, lh, top, bottom))
                    TextRenderer.DrawText(g, ln, fBody, new Point(x, y), Ink, Color.Transparent);
                y += lh;
            }
            y += S(2);
        }
        // 用户定案（2026-11-20）：右栏补一条模式切换提示（菜单路径，不是按键，不套键帽）
        foreach (string ln in WrapLines(g, "游戏内「设置 - 操作性」可选择经典和现代操作模式。", w, fBody))
        {
            if (InViewport(y, lh, top, bottom))
                TextRenderer.DrawText(g, ln, fBody, new Point(x, y), InkDim, Color.Transparent);
            y += lh;
        }
        y += S(1);
        return y;
    }

    void DrawRightScroll(Graphics g, int viewport, int contentH)
    {
        if (rightScrollMax <= 0) return;
        int trackX = colRight.Right - S(7), trackY = colRight.Y + S(10);
        int trackH = colRight.Height - S(20);
        int thumbH = Math.Max(S(24), (int)((long)trackH * viewport / Math.Max(1, contentH)));
        int thumbY = trackY + (int)((long)(trackH - thumbH) * rightScroll / Math.Max(1, rightScrollMax));
        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, Gold)))
            g.FillRectangle(b, trackX, trackY, S(3), trackH);
        using (GraphicsPath p = Round(new Rectangle(trackX - S(1), thumbY, S(5), thumbH), S(2)))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(160, Gold)))
            g.FillPath(b, p);
    }

    void DrawFooter(Graphics g)
    {
        int footTop = ClientSize.Height - S(54);
        DrawDivider(g, footTop);
        int y = ClientSize.Height - S(42);
        StatusDot(g, status, statusColor, colLeft.X, y);
        string hint = busy ? "关窗只是隐藏，游戏继续跑" : "游戏退出后本窗口自动关闭";
        Size sz = TextRenderer.MeasureText(g, hint, fMono);
        TextRenderer.DrawText(g, hint, fMono,
            new Point(ClientSize.Width - S(20) - sz.Width, y), InkDim, Color.Transparent);
        string brand = BrandGuard.Text;
        Size bs = TextRenderer.MeasureText(g, brand, fBrand);
        Point bp = new Point((ClientSize.Width - bs.Width) / 2, y);
        TextRenderer.DrawText(g, brand, fBrand,
            new Point(bp.X + S(1), bp.Y + S(1)), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, brand, fBrand, bp, GoldBright, Color.Transparent);
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
        using (GraphicsPath p = Round(Rectangle.Inflate(r, S(2), S(2)), S(8)))
        using (Pen pen = new Pen(GoldBright, 2f)) g.DrawPath(pen, p);
        using (GraphicsPath p = Round(Rectangle.Inflate(r, S(4), S(4)), S(10)))
        using (Pen pen = new Pen(Color.FromArgb(120, 0, 0, 0), 3f)) g.DrawPath(pen, p);
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

    // 折行只当兜底：每条独立信息自带 \n，中文按字符断词不好看
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

    int MeasureWrappedHeight(string text, int width, Font font)
    {
        using (Graphics g = CreateGraphics())
        {
            int lh = TextRenderer.MeasureText(g, "国", font).Height + S(3);
            int total = 0;
            foreach (string raw in text.Split('\n'))
            {
                if (raw.Length == 0) { total += lh / 2; continue; }
                string line = raw.StartsWith("# ") ? raw.Substring(2) : raw;
                while (line.Length > 0)
                {
                    int take = line.Length;
                    while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), font).Width > width)
                        take--;
                    total += lh;
                    line = line.Substring(take);
                }
            }
            return total;
        }
    }

    // ==========================================================================
    // 输入：鼠标 + 键盘（Tab 走 启动游戏 → 日志 → 退出）
    // ==========================================================================
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcLog.Contains(pt)) return 2;
        if (rcExit.Contains(pt)) return 3;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, Rectangle.Union(rcLog, rcExit));
            return Rectangle.Inflate(r, S(8), S(8));
        }
    }

    void RepaintControls() { Invalidate(ButtonsRect); }

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
        if (colRight.Contains(e.Location)) Focus();
        int i = HitTest(e.Location);
        if (i < 0) return;
        focus = i;
        Activate(i);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (rightScrollMax > 0 && colRight.Contains(e.Location))
        {
            rightScroll -= e.Delta;
            if (rightScroll < 0) rightScroll = 0;
            if (rightScroll > rightScrollMax) rightScroll = rightScrollMax;
            Invalidate(colRight);
            return;
        }
        base.OnMouseWheel(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
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
        int[] order = busy ? new int[] { 2, 3 } : new int[] { 0, 2, 3 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
        if (i == 0) { StartGame(); return; }
        if (i == 2) { OpenLog(); return; }
        if (i == 3) { Close(); return; }
    }

    // ==========================================================================
    // 现场读数（规则 37：取不到的值一律换成安全值并写日志，绝不当 0 用）
    // 本作是现代 .NET 10 自包含引擎（OpenRA，SDL2 + OpenGL/ANGLE），不拿这些值当除数、不改刷新率；
    // 读数只作排障快照（规则 40 现场实测）。
    // ==========================================================================
    class EnvRead
    {
        public int refresh, cx, cy, dpiX, dpiY, screens;
        public string root = "", driveType = "";
        public uint bps, spc, freeC, totalC;
        public bool diskOk;
        public int modeW, modeH, modeRefresh;
        public bool devModeOk;
        public int devModeSize;
        public List<string> fallback = new List<string>();
        public List<string> bad = new List<string>();
    }

    static EnvRead ReadEnvironment()
    {
        EnvRead e = new EnvRead();
        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                e.refresh = GetDeviceCaps(hdc, VREFRESH);
                e.cx = GetDeviceCaps(hdc, HORZRES);
                e.cy = GetDeviceCaps(hdc, VERTRES);
                e.dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                e.dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
        }
        catch (Exception ex) { Log("取显示参数失败：" + ex.Message); }

        // 当前显示模式另读一次：DEVMODEW 结构大小必须 220，少写一个字段会整体错位 4 字节，
        // 读出来的宽高就成 32×1920 那种鬼值（英雄无敌3 客户机踩过）
        try
        {
            e.devModeSize = Marshal.SizeOf(typeof(DEVMODE));
            e.devModeOk = e.devModeSize == 220;
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (ushort)e.devModeSize;
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                e.modeW = (int)dm.dmPelsWidth;
                e.modeH = (int)dm.dmPelsHeight;
                e.modeRefresh = (int)dm.dmDisplayFrequency;
            }
            else Log("EnumDisplaySettings 没给当前显示模式（只用 GetDeviceCaps 的读数）");
        }
        catch (Exception ex) { Log("取当前显示模式失败：" + ex.Message); }

        try { e.screens = Screen.AllScreens.Length; }
        catch (Exception ex) { Log("数屏幕失败：" + ex.Message); }

        try { e.root = Path.GetPathRoot(AppDir()) ?? ""; } catch { }
        if (e.root.Length > 0)
        {
            e.driveType = DriveTypeName(GetDriveTypeW(e.root));
            try { e.diskOk = GetDiskFreeSpaceA(e.root, out e.spc, out e.bps, out e.freeC, out e.totalC); }
            catch (Exception ex) { Log("取磁盘参数失败：" + ex.Message); }
        }
        return e;
    }

    // 断言 + 兜底：每个读回来的值都要有区间，越界就换安全值并记下用的是什么
    static void AssertReadings(EnvRead e)
    {
        if (e.refresh < 1 || e.refresh > 1000)
        {
            e.bad.Add("刷新率=" + e.refresh);
            e.fallback.Add("刷新率 " + e.refresh + "→60（虚拟显示 / 多屏 / 2K 屏常报 0）");
            e.refresh = 60;
        }
        if (e.cx <= 0 || e.cy <= 0)
        {
            e.bad.Add("桌面尺寸=" + e.cx + "×" + e.cy);
            try
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                if (b.Width > 0 && b.Height > 0)
                {
                    e.fallback.Add("桌面尺寸 " + e.cx + "×" + e.cy + "→" + b.Width + "×" + b.Height);
                    e.cx = b.Width; e.cy = b.Height;
                }
                else e.fallback.Add("桌面尺寸取不到，也没有可用兜底（记录为不可信）");
            }
            catch { e.fallback.Add("桌面尺寸取不到，兜底也失败（记录为不可信）"); }
        }
        if (e.dpiX < 96 || e.dpiX > 480 || e.dpiY < 96 || e.dpiY > 480)
        {
            e.bad.Add("DPI=" + e.dpiX + "/" + e.dpiY);
            e.fallback.Add("DPI " + e.dpiX + "/" + e.dpiY + "→96/96");
            e.dpiX = 96; e.dpiY = 96;
        }
        if (e.screens < 1) { e.bad.Add("屏幕数=" + e.screens); e.fallback.Add("屏幕数 " + e.screens + "→1"); e.screens = 1; }
        if (!e.devModeOk)
        {
            e.bad.Add("DEVMODEW 结构 " + e.devModeSize + " 字节（应 220）");
            e.fallback.Add("DEVMODEW 结构大小不符，那份模式读数不用（只用 GetDeviceCaps）");
            e.modeW = 0; e.modeH = 0; e.modeRefresh = 0;
        }
        else if (e.modeRefresh >= 1 && e.modeRefresh <= 1000 && e.refresh != e.modeRefresh)
        {
            // 两条通道互校：GetDeviceCaps 与 EnumDisplaySettings 不一致就说明其中一条不可信
            e.fallback.Add("刷新率两读不一：GetDeviceCaps=" + e.refresh +
                " EnumDisplaySettings=" + e.modeRefresh + "，取 EnumDisplaySettings");
            e.refresh = e.modeRefresh;
        }
        if (!e.diskOk) { e.bad.Add("游戏盘读数失败"); e.fallback.Add("游戏盘几何取不到（本启动器不拿它当除数，只记日志）"); }
        else
        {
            if (e.bps != 512 && e.bps != 1024 && e.bps != 2048 && e.bps != 4096)
            { e.bad.Add("每扇区字节=" + e.bps); e.fallback.Add("每扇区字节 " + e.bps + "→512（只记日志用）"); e.bps = 512; }
            if (e.spc == 0) { e.bad.Add("每簇扇区=0"); e.fallback.Add("每簇扇区 0→8（只记日志用）"); e.spc = 8; }
            if (e.totalC > 0 && e.freeC > e.totalC)
            {
                e.bad.Add("可用簇 " + e.freeC + " > 总簇 " + e.totalC);
                e.fallback.Add("可用簇 " + e.freeC + "→总簇 " + e.totalC + "（映射盘返回了不自洽的值）");
                e.freeC = e.totalC;
            }
        }
    }

    static string ReadingsLine(EnvRead e)
    {
        return "现场快照：显示 " + e.cx + "×" + e.cy + " 刷新率=" + e.refresh +
            " DPI=" + e.dpiX + "/" + e.dpiY + " 屏幕数=" + e.screens +
            "；当前模式读数 " + e.modeW + "×" + e.modeH + "@" + e.modeRefresh +
            "（DEVMODEW " + e.devModeSize + " 字节，" + (e.devModeOk ? "结构大小对" : "结构大小不符") + "）" +
            "；游戏盘 " + e.root + (e.driveType.Length == 0 ? "" : "（" + e.driveType + "）") +
            " 每扇区字节=" + e.bps + " 每簇扇区=" + e.spc +
            " 簇 总" + e.totalC + "/可用" + e.freeC + (e.diskOk ? "" : "（GetDiskFreeSpace 失败）");
    }

    // --readings：只读数 + 写日志，不启动游戏（换机器先跑这一条；收尾自检也跑它）
    static int DumpReadings()
    {
        EnvRead e = ReadEnvironment();
        AssertReadings(e);
        Log(ReadingsLine(e));
        Log("读数校验：" + (e.bad.Count == 0 ? "全部合理" : "可疑 —— " + string.Join("、", e.bad.ToArray())));
        foreach (string f in e.fallback) Log("读数兜底：" + f);
        Console.WriteLine(ReadingsLine(e));
        Console.WriteLine("读数校验：" + (e.bad.Count == 0 ? "全部合理" : "可疑"));
        // 读数可疑不拦退出码：兜底已经做完、本启动器不把这些值当除数交给游戏，
        // 这条开关的意义是「把每一项取值和判定打在日志里」，收尾自检断言的是日志内容。
        return 0;
    }

    void LogEnvironment()
    {
        try
        {
            EnvRead e = ReadEnvironment();
            AssertReadings(e);
            Log(ReadingsLine(e));
            Log("读数校验：" + (e.bad.Count == 0 ? "全部合理" : "可疑 —— " + string.Join("、", e.bad.ToArray())));
            foreach (string f in e.fallback) Log("读数兜底：" + f);
        }
        catch (Exception ex) { Log("采现场读数失败：" + ex); }
    }
    // ==========================================================================
    // 启动前文件检查（Engine 核心文件存在性，不做哈希校验）。
    // 缺文件没有留底可自修，界面只说一句人话，缺什么进日志（规则 34）。
    // ==========================================================================
    static readonly string[] RequiredFiles =
    {
        "RedAlert.exe", "RedAlert.dll", "RedAlert.runtimeconfig.json",
        "OpenRA.Game.dll", "OpenRA.Mods.Common.dll", "OpenRA.Mods.Cnc.dll",
        "OpenRA.Platforms.Default.dll", "SDL2.dll", "soft_oal.dll",
        "coreclr.dll", "hostfxr.dll", "hostpolicy.dll", @"mods\ra\mod.yaml"
    };

    bool CheckAllFiles(out string message)
    {
        message = "已就绪";
        string dir = EngineDir();
        List<string> missing = new List<string>();
        foreach (string f in RequiredFiles)
            if (!File.Exists(Path.Combine(dir, f))) missing.Add(f);
        if (missing.Count > 0)
        {
            message = FailFilesText;
            Log("文件检查：缺 " + string.Join("、", missing.ToArray()) + "（目录 " + dir + "）");
            return false;
        }
        Log("文件检查：Engine 核心 " + RequiredFiles.Length + " 个文件齐全");
        return true;
    }

    // 复刻自带启动器 Prepare 的绿色部分：创建 Support 目录；settings.yaml 缺失时写默认。
    // 已有 settings.yaml 绝不整份覆盖（玩家改过的设置只在 SetPlayerName 里动 Name 那一行）。
    // 不复刻 ra.ftl / mod.yaml 的 XDGAME 标题改写：那是发行方的品牌动作，现状保持不动。
    static void EnsureSupport()
    {
        try
        {
            Directory.CreateDirectory(SupportDir());
            string settings = Path.Combine(SupportDir(), "settings.yaml");
            if (!File.Exists(settings))
            {
                string def = "Graphics:\n\tMode: Windowed\n\tWindowedSize: 1280, 800\n" +
                    "\tCapFramerate: true\n\tMaxFramerate: 60\nPlayer:\n\tName: 指挥官\n" +
                    "Game:\n\tMouseControlStyle: Modern\n";
                File.WriteAllText(settings, def, new UTF8Encoding(false));
                Log("settings.yaml 不存在，已创建默认设置（窗口 1280×800、限帧 60、现代鼠标方案）");
            }
        }
        catch (Exception ex) { Log("Support 目录准备失败（继续启动）：" + ex.Message); }
    }

    // 用户定案（2026-11-20）：每次启动前把本机电脑名写进 settings.yaml 的 Player: Name:。
    // 这是联机显示名，网吧按机器名自动设置。只改这一行的值，其余字节不碰；
    // 值已经等于机器名就不写盘（幂等）；Player 节 / Name 行缺失就补上。
    static void SetPlayerName()
    {
        try
        {
            string settings = Path.Combine(SupportDir(), "settings.yaml");
            if (!File.Exists(settings)) { Log("玩家名：settings.yaml 不存在，跳过（EnsureSupport 会建）"); return; }
            string name = Environment.MachineName;
            string text = File.ReadAllText(settings, Encoding.UTF8);
            string nl = text.IndexOf("\r\n") >= 0 ? "\r\n" : "\n";
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd('\r');

            int playerLine = -1, nameLine = -1, sectionEnd = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i];
                if (t.Length > 0 && t[0] != ' ' && t[0] != '\t')
                {
                    if (playerLine >= 0) { sectionEnd = i; break; }
                    if (t.TrimEnd() == "Player:" || t.StartsWith("Player:")) playerLine = i;
                }
                else if (playerLine >= 0 && t.TrimStart().StartsWith("Name:"))
                {
                    nameLine = i;
                }
            }
            string quoted = YamlValue(name);
            if (playerLine < 0)
            {
                // 没有 Player 节：整份文件末尾补一节（只加不改）
                string add = "Player:" + nl + "\tName: " + quoted + nl;
                File.WriteAllText(settings, text + (text.EndsWith("\n") ? "" : nl) + add, new UTF8Encoding(false));
                Log("玩家名：settings.yaml 没有 Player 节，已补上，联机显示名 = 机器名 " + name);
                return;
            }
            if (nameLine < 0)
            {
                // 有 Player 节没有 Name 行：紧跟节头插一行
                List<string> ls = new List<string>(lines);
                ls.Insert(playerLine + 1, "\tName: " + quoted);
                File.WriteAllText(settings, string.Join(nl, ls.ToArray()), new UTF8Encoding(false));
                Log("玩家名：Player 节没有 Name 行，已补上，联机显示名 = 机器名 " + name);
                return;
            }
            string old = lines[nameLine];
            int colon = old.IndexOf(':');
            string indent = old.Substring(0, colon + 1);
            string oldVal = old.Substring(colon + 1).Trim();
            if (oldVal == quoted || oldVal == name)
            {
                Log("玩家名：已是本机电脑名 " + name + "，不改动 settings.yaml");
                return;
            }
            lines[nameLine] = indent + " " + quoted;
            File.WriteAllText(settings, string.Join(nl, lines), new UTF8Encoding(false));
            Log("玩家名：联机显示名设为本机电脑名 " + name + "（原值 " + oldVal + "；只改 Name 这一行）");
        }
        catch (Exception ex) { Log("玩家名设置失败（继续启动）：" + ex.Message); }
    }

    // YAML 裸字符串放不下时（含特殊字符 / 首尾空白）转双引号写法；普通机器名原样写
    static string YamlValue(string v)
    {
        bool needQuote = v.Length == 0 || v.Trim() != v;
        foreach (char c in v)
            if (c == ':' || c == '#' || c == '{' || c == '}' || c == '[' || c == ']' ||
                c == ',' || c == '&' || c == '*' || c == '?' || c == '|' || c == '>' ||
                c == '\'' || c == '"' || c == '%' || c == '@' || c == '`' || c == '\\')
                needQuote = true;
        if (!needQuote) return v;
        return "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    // 自带启动器的 MSVCRT 引号规则（反斜杠计数 + 双引号前加倍），逐字复刻：
    // Engine.LaunchPath 的值含空格 / 反斜杠时必须按这个包，游戏才解析得对。
    static string Quote(string text)
    {
        StringBuilder sb = new StringBuilder("\"");
        int num = 0;
        foreach (char c in text)
        {
            if (c == '\\') { num++; continue; }
            sb.Append('\\', (c == '"') ? (num * 2 + 1) : num);
            sb.Append(c);
            num = 0;
        }
        sb.Append('\\', num * 2);
        return sb.Append('"').ToString();
    }

    // ==========================================================================
    // 启动游戏（单按钮；--go 同一条路径）
    // ==========================================================================
    void StartGame()
    {
        if (busy) return;
        string message;
        if (!CheckAllFiles(out message))
        {
            status = message;
            statusColor = Warn;
            filesOk = false;
            Invalidate();
            return;
        }
        string exePath = GameExePath();
        string engineDir = EngineDir();
        string launchArgs = "Game.Mod=ra " + Quote("Engine.LaunchPath=" + exePath);
        Log("点「启动游戏」：" + exePath + "（工作目录 = Engine，参数 " + launchArgs + "）");
        SetBusy(true, "游戏运行中…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                LogEnvironment();
                EnsureSupport();
                SetPlayerName();
                DateTime start = DateTime.Now;
                int code = RunGame(exePath, launchArgs, engineDir);
                int secs = (int)(DateTime.Now - start).TotalSeconds;
                bool abnormal = (uint)code >= 0xC0000000;
                Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                    (abnormal ? "（系统级异常）" : ""));
                if (abnormal) CaptureWerReport("RedAlert.exe");
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
                SetStatus("启动失败，请把运行日志发给我们", Warn);
            }
            finally { SetBusy(false, null); }
        });
        t.IsBackground = true;
        t.Start();
    }

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
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit,
        uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")]
    static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr h, out uint code);

    // 游戏必须是本进程的直接子进程：CreateProcess + 工作目录 = Engine，等它退出并原样回传退出码。
    // 不做「包一层再去找实际进程」，少一层少一处出错。
    int RunGame(string exePath, string launchArgs, string workingDir)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        string cmd = "\"" + exePath + "\"" + (launchArgs.Length > 0 ? " " + launchArgs : "");
        bool started = CreateProcess(exePath, cmd, IntPtr.Zero, IntPtr.Zero, false,
            0, IntPtr.Zero, workingDir, ref si, out pi);
        if (!started)
        {
            int err = Marshal.GetLastWin32Error();
            Log("CreateProcess 失败（err=" + err + "），改用 Process.Start");
            ProcessStartInfo psi = new ProcessStartInfo(exePath, launchArgs);
            psi.WorkingDirectory = workingDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process game = Process.Start(psi))
            {
                game.WaitForExit();
                Log("游戏退出码：" + game.ExitCode);
                return game.ExitCode;
            }
        }
        Log("已启动游戏 " + Path.GetFileName(exePath) + "，pid=" + pi.dwProcessId + "（本进程的 direct child）");
        // 启动器对游戏窗口零干预（AGENTS 规则 44 / 坑 114）：全屏游戏自己会进前台，外部进程
        // 任何窗口操作（SetForegroundWindow / ShowWindow 还原 / 前台授权）都是在和引擎的全屏
        // 切换打架。陪跑只观察不动手。
        // 看门狗：窗口已销毁、进程空转、启动器无限等待 = 平台挂死。等游戏时每 1s 一查：
        // 开局 60s 宽限后，游戏连续 45s 没有任何可见/最小化窗口 → 判卡死：先补送 WM_CLOSE，
        // 10s 仍不退再 TerminateProcess 收尾，全程写日志。玩家正常玩时窗口一直在，不会误杀。
        DateTime t0 = DateTime.Now;
        DateTime lastVisible = t0;
        while (WaitForSingleObject(pi.hProcess, 1000) != 0)
        {
            if (GameHasWindow(pi.dwProcessId)) lastVisible = DateTime.Now;
            if ((DateTime.Now - t0).TotalSeconds > 60 && (DateTime.Now - lastVisible).TotalSeconds > 45)
            {
                Log("看门狗：游戏进程 45s 没有任何窗口（多半卡在退出流程），补送 WM_CLOSE");
                PostCloseToAny(pi.dwProcessId);
                if (WaitForSingleObject(pi.hProcess, 10000) != 0)
                {
                    Log("看门狗：仍不退，按托管契约结束游戏进程（不留孤儿进程）");
                    TerminateProcess(pi.hProcess, 1);
                    WaitForSingleObject(pi.hProcess, 10000);
                }
                break;
            }
        }
        uint code;
        bool got = GetExitCodeProcess(pi.hProcess, out code);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        Log("游戏退出码：" + (got ? ((int)code).ToString() : "取不到"));
        return got ? (int)code : -1;
    }

    // 看门狗用：该 pid 有没有「可见或最小化」的顶层窗口（最小化算活着，不许误杀挂后台的玩家）
    static bool GameHasWindow(int pid)
    {
        bool[] hit = new bool[1];
        uint want = (uint)pid;
        EnumWindows(delegate(IntPtr h, IntPtr lp)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if (p == want && (IsWindowVisible(h) || IsIconic(h))) { hit[0] = true; return false; }
            return true;
        }, IntPtr.Zero);
        return hit[0];
    }

    static void PostCloseToAny(int pid)
    {
        uint want = (uint)pid;
        EnumWindows(delegate(IntPtr h, IntPtr lp)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if (p == want) { PostMessageW(h, 0x0010, IntPtr.Zero, IntPtr.Zero); return false; }
            return true;
        }, IntPtr.Zero);
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

    // 玩家关窗口 ≠ 关游戏：隐藏到托盘继续陪跑，游戏退出时才带着它的退出码结束
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

    // 规则 21：只用 exe 自己内嵌的统一启动器图标（构建时 -win32icon:assets\ico.ico）。
    // 绝不从游戏 exe 抽图标（任务管理器 / 平台会把启动器认成游戏进程），也不去找本机路径兜底。
    void LoadGameIcon()
    {
        try
        {
            Icon own = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (own != null) { Icon = own; Log("窗口图标：取 exe 自身内嵌图标成功"); return; }
            Log("窗口图标：exe 自身没量到图标，退回系统默认图标");
        }
        catch (Exception ex) { Log("取 exe 图标失败，退回系统默认图标：" + ex.Message); }
        Icon = SystemIcons.Application;
    }

    void SetBusy(bool value, string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), value, text); return; }
        busy = value;
        hover = -1;
        if (value) focus = NoFocus;
        if (text != null) status = text;
        if (!value) { status = filesOk ? "已就绪" : FailFilesText; statusColor = filesOk ? Ok : Warn; }
        RepaintControls();
    }

    void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string, Color>(SetStatus), text, color); return; }
        status = text;
        statusColor = color;
        Invalidate();
    }

    // ==========================================================================
    // 崩溃取证（规则 35）：读系统自动生成的 WER 报告，抄成一行进同一份日志，本体存 崩溃转储\
    // 已判死不许再试：WER LocalDumps 注册表（要动系统）、调试器拉起游戏（实测窗口出不来）
    // ==========================================================================
    void CaptureWerReport(string exeName)
    {
        try
        {
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] roots =
            {
                Path.Combine(common, @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(common, @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(local, @"Microsoft\Windows\WER\ReportQueue")
            };
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            // 报告是系统后台写的，立刻读会读不到：重试几秒，读不到就在日志里写明
            for (int wait = 0; wait < 8 && best == null; wait++)
            {
                if (wait > 0) Thread.Sleep(1000);
                best = FindWerFolder(roots, exeName, out bestTime);
            }
            if (best == null) { Log("崩溃报告：没抓到（WER 里没有本游戏 " + exeName + " 的记录，或系统还没写完）"); return; }
            string[] wers = Directory.GetFiles(best, "*.wer");
            if (wers.Length == 0) { Log("崩溃报告：没抓到（" + best + " 里没有 .wer）"); return; }
            string dstFile = Path.Combine(AppDir(), "崩溃转储", Path.GetFileName(best) + ".wer");
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
            File.Copy(wers[0], dstFile, true);
            Log("崩溃捕获：" + ParseWer(wers[0]) + "；报告已存 " + dstFile);
        }
        catch (Exception ex) { Log("读崩溃报告失败：" + ex); }
    }

    // .wer 是 UTF-16，Sig[n].Name / Sig[n].Value 成对排；名字随系统语言变
    // （中文机「异常代码 / 异常偏移 / 故障模块名称」，英文机 "Exception Code / Exception Offset / Fault Module Name"），
    // 所以按名字认、不许按序号猜，也别把「故障模块版本 / Fault Module Version」当成模块名。
    static string ParseWer(string werPath)
    {
        string code = "", offset = "", module = "", want = "";
        foreach (string raw in File.ReadAllLines(werPath, Encoding.Unicode))
        {
            string s = raw.Trim();
            int eq = s.IndexOf('=');
            if (eq <= 0) continue;
            string key = s.Substring(0, eq), val = s.Substring(eq + 1).Trim();
            if (key.EndsWith(".Name")) want = val;
            else if (key.EndsWith(".Value"))
            {
                string kind = SigKind(want);
                if (kind == "code") code = val;
                else if (kind == "offset") offset = val;
                else if (kind == "module") module = val;
                want = "";
            }
            else if (key == "ModName" && module.Length == 0) module = val;
        }
        if (code.Length == 0 && offset.Length == 0 && module.Length == 0)
            return "报告里没认出异常码/模块（原文见报告）";
        return "异常码 " + (code.Length == 0 ? "没读到" : "0x" + code) +
               "，出错地址 " + (offset.Length == 0 ? "没读到" : "+0x" + offset) +
               "，故障模块 " + (module.Length == 0 ? "没读到" : module);
    }

    static string SigKind(string name)
    {
        if (name.Length == 0) return "";
        if (name.IndexOf("异常代码") >= 0 || name.IndexOf("Exception Code") >= 0) return "code";
        if (name.IndexOf("异常偏移") >= 0 || name.IndexOf("Exception Offset") >= 0) return "offset";
        if (name.IndexOf("模块名称") >= 0 ||
            (name.IndexOf("Module") >= 0 && name.IndexOf("Name") >= 0)) return "module";
        return "";
    }

    static string FindWerFolder(string[] roots, string exeName, out DateTime newest)
    {
        string best = null;
        newest = DateTime.MinValue;
        foreach (string root in roots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (string d in Directory.GetDirectories(root, "AppCrash_" + exeName + "*"))
                {
                    DateTime tm = Directory.GetLastWriteTime(d);
                    if (tm < DateTime.Now.AddMinutes(-15)) continue;
                    if (tm > newest) { newest = tm; best = d; }
                }
            }
            catch { }
        }
        return best;
    }

    // ==========================================================================
    // --selftest <程序> [参数…]：拉一个小程序并原样回传它的退出码。
    // 不启动游戏就能验证「退出码回传」这条托管契约（收尾自检用 cmd.exe /c exit 7 跑一次）。
    // ==========================================================================
    static int SelfTest(string[] args)
    {
        try
        {
            if (args.Length < 1) { Console.Error.WriteLine("用法：game_starter.exe --selftest <程序> [参数…]"); return 2; }
            string dir = AppDir();
            string file = args[0];
            string rest = "";
            for (int i = 1; i < args.Length; i++) rest += (rest.Length == 0 ? "" : " ") + args[i];
            if (!Path.IsPathRooted(file))
            {
                string local = Path.Combine(dir, file);
                if (File.Exists(local)) file = local;
            }
            ProcessStartInfo psi = new ProcessStartInfo(file, rest ?? "");
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
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

    // ==========================================================================
    // 品牌字样：exe 内不留明文，运行时解出来再对 SHA-256（规则 10：只提高门槛，不宣称绝对防改）
    // ==========================================================================
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
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(text));
                if (hash.Length != Expected.Length) return false;
                for (int i = 0; i < hash.Length; i++) if (hash[i] != Expected[i]) return false;
            }
            return text.Length == 7;
        }
    }

    // ==========================================================================
    // 运行日志：玩家报障的唯一入口（界面只说人话，技术细节全进这里）
    // ==========================================================================
    static string AppDir() { return Path.GetDirectoryName(Application.ExecutablePath); }

    static readonly object logGate = new object();
    static string LogPath { get { return Path.Combine(AppDir(), "运行日志.txt"); } }

    static void Log(string msg)
    {
        try
        {
            lock (logGate)
            {
                string p = LogPath;
                FileInfo fi = new FileInfo(p);
                if (!fi.Exists)
                    File.WriteAllText(p, "运行日志：出问题时把这个文件发给我们\r\n", Encoding.UTF8);
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
            status = "日志打不开，请把运行日志发给我们";
            statusColor = Warn;
            RepaintControls();
        }
    }

    // ==========================================================================
    // launcher.ini（UTF-8；值里的 \n 换成换行，左栏文案 / 默认版本都能在外面改）
    // ==========================================================================
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
                case "subtitle": gameSubtitle = val; break;
                case "intro": intro = val; break;
                case "centernote": centerNote = val; break;
            }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        if (fTitle != null) fTitle.Dispose();
        if (fHead != null) fHead.Dispose();
        if (fRowSel != null) fRowSel.Dispose();
        if (fRow != null) fRow.Dispose();
        if (fBody != null) fBody.Dispose();
        if (fCap != null) fCap.Dispose();
        if (fCapZh != null) fCapZh.Dispose();
        if (fMono != null) fMono.Dispose();
        if (fBrand != null) fBrand.Dispose();
        base.OnFormClosed(e);
    }

    // ==========================================================================
    // Win32：环境读数（常量抄错 = 日志撒谎，规则 24 第 12 类）
    // ==========================================================================
    // VREFRESH = 116（0 是 DRIVERVERSION，真实驱动回 0x4000=16384，抄错就把刷新率记成 16384）
    const int VREFRESH = 116, HORZRES = 8, VERTRES = 10, LOGPIXELSX = 88, LOGPIXELSY = 90;
    const int ENUM_CURRENT_SETTINGS = -1;
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern uint GetDriveTypeW(string root);
    // 注意出参顺序：每簇扇数、每扇区字节、可用簇、总簇（MSDN 就是这个顺序，写反日志就标错）
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool GetDiskFreeSpaceA(string root, out uint sectorsPerCluster,
        out uint bytesPerSector, out uint freeClusters, out uint totalClusters);

    // DEVMODEW 规范布局：dmPosition 是 POINTL(8) + dmDisplayOrientation(4) + dmDisplayFixedOutput(4)，
    // 少一个字段整体错位 4 字节，读回来的宽高就成了 32×1920。Marshal.SizeOf 必须是 220，代码里有断言。
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string device, int modeNum, ref DEVMODE dm);

    static string DriveTypeName(uint t)
    {
        // GetDriveType：0 未知、1 路径没有根目录、2 可移动、3 本地硬盘、4 网络、5 光盘、6 内存盘。
        // 盘符推不出设备类型（规则 24 第 12 类），只如实报 API 的回答。
        switch (t)
        {
            case 0: return "未知"; case 1: return "路径无效"; case 2: return "可移动盘";
            case 3: return "本地硬盘"; case 4: return "网络盘"; case 5: return "光盘";
            case 6: return "内存盘";
            default: return "未知(" + t + ")";
        }
    }
}