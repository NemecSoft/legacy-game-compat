using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// 使命召唤5：战争世界（Call of Duty: World at War · Treyarch 2008）启动器 game_starter.exe。
// 界面规范：仓库 .qoder/skills/legacy-launcher-gui/SKILL.md + references/leather-recipes.md
//（纯 GDI+ 自绘、三栏 900×620、拟物皮革、全窗口 TextRenderer ClearType、键帽走通用库 Keycaps.cs）。
// 本文件以 Games\CoD2\src\Launcher.cs 为底本移植，砍掉了 CoD2 专有的东西：
// DXVK 兼容层档位自检（CoD5 用户明令不打 DX 补丁，只做原生 Direct3D 9 单档）、
// 安全模式标记/字节补丁回滚（CoDWaW.exe 未打补丁，没有这个机制）、/dxlog、/layer、/restore-exe、/clearmark。
//
// 已实测确认的事实（2026-11-10，取证见 Games\CoD5\evidence\）：
//   主程序 CoDWaW.exe（32 位，DX9）。存档/配置**不在游戏目录也不在注册表**：
//   默认落在 %LOCALAPPDATA%\Activision\CoDWaW\players\profiles\<玩家名>\（玩家名本机为 $$$，
//   active.txt 指向当前 profile；config.cfg 含 sv_lastSaveGame 存档指针 + cheat_items_set1/cheat_points
//   关卡解锁位图；save\*.svg 是关卡存档）。这正是「通关存档拿到别的电脑不行」的根源：
//   新电脑 AppData 没有这套 profile，游戏启动就要求重建档案，存档自然读不到（社区方案一致，
//   PCGamingWiki / GameFAQs：整份 players 目录拷过去 + active.txt 指对名字）。注册表不需要（实测未导入即可运行）。
//
//   绿色解法（2026-11-10 定案，PCGamingWiki 记载的引擎开关）：启动参数带
//     +set fs_localAppData "<游戏目录>" +set fs_BaseGame "<游戏目录>"
//   引擎即把 players 目录重定向进游戏目录——存档跟着游戏目录走，换电脑自动带过去，AppData 零写入
//   （fs-repro.ps1 实测：第二次启动起游戏目录 players\profiles\<名>\config.cfg/mpdata 完整生成，
//   AppData 时间戳不再变化）。档案名统一 YunGame（用户硬规矩）：预置目录、active.txt 指针、
//   config.cfg 内 sv_lastSave* 存档路径引用三处一致。
//
//   存档两层保险（EnsureProfile，每次启动跑，幂等）：
//     ① 指针自愈：档案目录在、active.txt 丢/空/指错 → 写回 YunGame（引擎异常退出有概率重置指针）；
//     ② 存档自动落地：档案目录整个不在（players 被杀毒清了/新电脑目录不全）→ 从交付留底
//        _yungame\ 整树恢复（active.txt + config.cfg + 通关存档 ber2/ber3.svg），玩家无感不弹创建档案。
//
//   画面设置由引擎读写 players\profiles\<名>\config.cfg（r_mode 是 "1920x1080" 这种字符串，
//   r_fullscreen 是 "1"/"0"，正则与 CoD2 同构）。玩家在游戏里改的分辨率、全屏、按键必须留着——
//   启动器只做一个"默认全屏"的规范：每次启动前比对 config.cfg 的 r_fullscreen/r_mode 与
//   display.state 里我们上次写的值，一致 → 按本机实测桌面分辨率刷新（换显示器也总是全屏跑）；
//   不一致 → 玩家自己改过 → 从此永远不再动。CoDWaWmp.exe 是联机版，本项目不接反作弊网游，只认单人版。
//
// 启动器替玩家做的两件默认值（规则 34：玩家只面对一个界面，不许让他猜乱码；2026-11-10 用户定案）：
//   1) 画面默认值：射击游戏默认就该全屏——启动前把初始档案规范成「全屏 + 本机桌面分辨率」，
//      比对账本 display.state：值还是我们上次写的 → 按当前桌面刷新；变了 → 玩家自己改过，永远不再动。
//   2) 字幕默认值：cg_subtitles 默认开启（账本键 @subs），同款账本语义——玩家在游戏里关掉就永远尊重。
//
// 命令行开关（都不需要玩家碰，给平台和远程排障用）：
//   /go           零交互按当前档直接启动（平台入口）；/mode=1 选档；/hideparent 藏掉父控制台
//   /readings     只采现场读数写日志、不启动游戏（换机器先跑这条）
//   /clearmark    只静默删掉安全模式标记（__CoDWaW，不删下次启动弹「以安全模式运行吗？」）、不启动游戏
//   /display "<config.cfg>" "<display.state>"  只按账本规范这两份文件的画面默认值、不启动游戏（收尾自检用它验逻辑）
//   /wer "<Report.wer>" [结论另存文件]  只解析崩溃报告（结论必须另存 UTF-8 文件再断言，别读 stdout 中文）
//   /selftest <程序> [参数…]            直接拉起该程序并原样回传退出码（不启动游戏，用来验托管契约）
//
// 平台托管契约（规则 8 / 技能「平台托管契约」）：游戏用 CreateProcess 拉起，是 game_starter.exe 的**直接子进程**、
// 工作目录 = 游戏目录；游戏运行期间本进程陪跑；游戏退出后本进程退出并**原样回传游戏退出码**；
// 玩家关窗口只是隐藏到托盘陪跑，不杀游戏。
// 陪跑带看门狗：播片期间关窗可能让全屏卡在退出流程（窗口没了、进程空转）——
// 游戏连续 45s 无任何可见/最小化窗口即判卡死，补送 WM_CLOSE → 10s 不退再结束进程并写日志，平台永不挂死。

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

    // 修不了层文件时给玩家看的那一句人话（规则 34：不许出现任何 bat 名字、不许出现「管理员」）
    const string FailLayerText = "显示文件不完整，请把运行日志发给我们";

    // ---------- launcher.ini 可覆盖（换同类游戏不改代码） ----------
    string gameTitle = "使命召唤5：战争世界";
    string gameSubtitle = "Call of Duty: World at War · 单人战役";
    string gameExe = "CoDWaW.exe";
    string gameArgs = "";              // launcher.ini 里的追加参数；fs_localAppData 重定向由启动器运行时动态拼
    string intro =
        "《使命召唤5：战争世界》\n" +
        "是 Treyarch 2008 年的二战\n" +
        "第一人称射击：太平洋战场\n" +
        "与东线柏林两条战线，\n" +
        "美军打日军、苏军攻柏林。\n" +
        "\n" +
        "本作是简体中文版，\n" +
        "通关存档已随游戏目录预置，\n" +
        "进菜单选「任务」选关开打。\n" +
        "\n" +
        "玩法一句：掩体推进，\n" +
        "近战拼刀要果断，\n" +
        "手雷先炸机枪点。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给我们";
    string centerNote = "游戏会自动检测画面。";
    string comboHint = "启动方式固定为原生 Direct3D 9；如果游戏有异常，点「打开运行日志」把内容发给我们。";

    // ---------- 启动方式（单档：用户明令不打 DX 补丁，只有原生 Direct3D 9 一条路） ----------
    static string[] ModeNames = new string[]
    {
        "1  原生 Direct3D 9"
    };

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int mode = 0;                 // 单档，恒 0
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, layerOk = true;
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
    Rectangle rcLabelMode, rcComboHint, rcComboMode, rcStart, rcLog;
    SelectPanel selMode;
    ContextMenuStrip modeMenu;

    class KeyRow
    {
        public string Caps;     // 通用键帽库的 id 串（templates\Keycaps.cs）
        public string Note;
        public KeyRow(string caps, string note) { Caps = caps; Note = note; }
    }

    // 按键表：照 WaW 引擎默认（打包在 main fastfile 内，社区公认默认）逐条抄，不自编、不改键。
    static readonly KeyRow[] KeyRows = new KeyRow[]
    {
        new KeyRow("pc:W pc:A pc:S pc:D", "前进 / 后退 / 左右移动"),
        new KeyRow("pc:Shift", "屏息 / 稳住准星"),
        new KeyRow("pc:Mouse1", "开火"),
        new KeyRow("pc:Mouse2", "瞄准（按住）"),
        new KeyRow("pc:1 pc:2", "切换武器"),
        new KeyRow("pc:G pc:Mouse3", "投掷手雷"),
        new KeyRow("pc:4", "燃烧弹 / 特殊手雷"),
        new KeyRow("pc:V", "近战（枪托 / 拼刀）"),
        new KeyRow("pc:F", "互动：换枪炮 / 开门"),
        new KeyRow("pc:R", "装弹"),
        new KeyRow("pc:Space", "跳跃 / 站起"),
        new KeyRow("pc:Ctrl", "卧倒"),
        new KeyRow("pc:C", "蹲下"),
        new KeyRow("pc:Tab", "看目标 / 战绩"),
        new KeyRow("pc:Esc", "菜单 / 返回"),
        new KeyRow("pc:~", "控制台")
    };

    // ==========================================================================
    // 入口
    // ==========================================================================
    [STAThread]
    static int Main(string[] args)
    {
        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();
        Application.EnableVisualStyles();

        // 构造函数里 LoadIni 可能替换 ModeNames，所以参数解析必须排在它后面
        Launcher l = new Launcher();
        foreach (string a in args)
        {
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;
            int picked;
            if (a.StartsWith("/mode") && a.IndexOf('=') > 0 &&
                int.TryParse(a.Substring(a.IndexOf('=') + 1), out picked) &&
                picked >= 1 && picked <= ModeNames.Length) l.mode = picked - 1;
        }
        string parentName = ParentProcessName();
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；父进程：" + (parentName.Length == 0 ? "未知" : parentName) +
            "；零交互：" + (l.autoStart ? "是" : "否") +
            "；档位：" + ModeNames[l.mode]);

        foreach (string a0 in args)
        {
            // /readings 只读数、/wer 只解析崩溃报告、/selftest 只跑指定的小工具：都不启动游戏
            if (a0 == "/readings" || a0 == "-readings") return DumpReadings();
            // /clearmark：只清安全模式标记（上次没正常退出会弹「以安全模式运行吗？」的那个），不启动游戏。
            // 加这个开关是为了让收尾自检能真验一次这段逻辑（否则它只在拉起游戏时才跑到）。
            if (a0 == "/clearmark" || a0 == "-clearmark") { l.ClearSafeMark(); return 0; }
            // /display <config.cfg> <display.state>：只对这两份路径做画面默认值规范，不启动游戏（收尾自检 3d 用它验）
            if (a0 == "/display" || a0 == "-display")
            {
                if (args.Length < 3 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe /display \"<config.cfg>\" \"<display.state>\"");
                    return 2;
                }
                // 字幕默认值同一份账本（键加 @subs 后缀），先于画面跑（各自独立记账互不影响）
                NormalizeSubtitles(args[1], args[2]);
                return NormalizeOneDisplay(args[1], args[2]) ? 0 : 1;
            }
            if (a0 == "/selftest" || a0 == "-selftest") return SelfTest(Tail(args));
            if (a0 == "/wer")
            {
                if (args.Length < 2 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe /wer \"<Report.wer 路径>\" [结论另存的 UTF-8 文件]");
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
    // 父进程 / 藏控制台（/hideparent：只对 cmd / powershell / pwsh 动手）
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
    [DllImport("user32.dll")]
    static extern bool EnumChildWindows(IntPtr p, EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassNameW(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")]
    static extern int GetWindowLongW(IntPtr h, int i);
    [DllImport("user32.dll")]
    static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);

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
        RebuildSel();
        LayoutRects();
        // 单档原生启动，没有兼容层文件要自检：状态恒「已就绪」（规则 37：环境异常由启动器自己处理）
        layerOk = true;
        status = "已就绪";
        statusColor = Ok;
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
        int hintH = MeasureWrappedHeight(comboHint, innerW, fBody);
        int comboH = S(48);      // 框高按两行字留：最长那项「2  原生 Direct3D 9（没有 Vulkan 的机器）」287px > 框内 230px，
                                // 会折成两行；40px 会把第二行裁掉（实测证据 evidence\launcher-mode2.png），48px 才装得下
        // 中栏一叠：标题 → 一句说明 → 启动选项标签 → 用途说明 → 下拉框 → 启动 → 日志，
        // 整叠垂直居中，说明和控件之间不许拉出空洞（leather-recipes.md §4、AGENTS 规则 20）
        int stackH = headH + S(8) + noteH + S(18)
                   + headH + S(4) + hintH + S(6) + comboH
                   + S(16) + S(44) + S(10) + S(34);
        int stackTop = colCenter.Y + S(12);
        int avail = colCenter.Bottom - S(14) - stackTop;
        int y = stackTop + Math.Max(0, (avail - stackH) / 2);

        rcCenterHead = new Rectangle(inner, y, innerW, headH);
        rcCenterNote = new Rectangle(inner, rcCenterHead.Bottom + S(8), innerW, noteH);
        rcLabelMode = new Rectangle(inner, rcCenterNote.Bottom + S(18), innerW, headH);
        rcComboHint = new Rectangle(inner, rcLabelMode.Bottom + S(4), innerW, hintH);
        rcComboMode = new Rectangle(inner, rcComboHint.Bottom + S(6), innerW, comboH);
        rcStart = new Rectangle(inner, rcComboMode.Bottom + S(16), innerW, S(44));
        rcLog = new Rectangle(inner, rcStart.Bottom + S(10), innerW, S(34));
        if (selMode != null) selMode.SetBounds(rcComboMode.X, rcComboMode.Y, rcComboMode.Width, rcComboMode.Height);
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

        TextRenderer.DrawText(g, "启动选项", fHead, new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);
        // 下拉框用途说明：固定贴在框上方（硬约束 18，前缀「本下拉框的作用是：」不许省）
        DrawWrapped(g, comboHint, rcComboHint.X, rcComboHint.Y, rcComboHint.Width, fBody, InkDim);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy && layerOk,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcLog, "打开运行日志", false, true, hover == 1 || focus == 1, focus == 1);
        if (focus == 3 && selMode != null) FocusRing(g, selMode.Bounds);
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

    // 右栏：只有按键操作说明（本游戏纯键盘 + 鼠标，键帽一律金色 = 玩家真要按的键）
    void DrawRight(Graphics g)
    {
        Card(g, colRight);
        int top = colRight.Y + S(6);
        int bottom = colRight.Bottom - S(6);
        int startY = colRight.Y + S(10) - rightScroll;
        int endY = DrawRightContent(g, startY, top, bottom);
        int contentH = endY - startY;
        int viewport = colRight.Height - S(14);
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
        Log("右栏按键表自检：行数=" + KeyRows.Length + " 内容高=" + contentH +
            " 视口高=" + viewport + " 需要滚动=" + (contentH > viewport));
    }

    int DrawRightContent(Graphics g, int y, int top, int bottom)
    {
        int x = colRight.X + S(14), w = colRight.Width - S(28);
        int headH = TextRenderer.MeasureText(g, "国", fHead).Height;
        // 表的看法写在右栏自己头上（一行就够；不写翻页提示）
        string head = "操作说明 · 键帽即按键";
        if (InViewport(y, headH, top, bottom))
            TextRenderer.DrawText(g, head, fHead, new Point(x, y), Gold, Color.Transparent);
        y += headH + S(4);
        return DrawKeyRows(g, x, w, y, top, bottom);
    }

    int DrawKeyRows(Graphics g, int x, int w, int y, int top, int bottom)
    {
        int capH = S(20);
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(1);
        int gap = S(1);
        foreach (KeyRow r in KeyRows)
        {
            int capsW = Keycaps.GroupWidth(g, r.Caps, capH);
            int noteX = x + capsW + S(6);
            int noteW = TextRenderer.MeasureText(g, r.Note, fBody).Width;
            bool wrap = noteX + noteW > x + w;
            int rowH = wrap ? capH + lh : capH;
            if (InViewport(y, rowH, top, bottom))
            {
                Keycaps.DrawGroup(g, r.Caps, x, y, capH, true);
                if (wrap)
                {
                    TextRenderer.DrawText(g, r.Note, fBody, new Point(x, y + capH), Ink, Color.Transparent);
                }
                else
                {
                    TextRenderer.DrawText(g, r.Note, fBody,
                        new Rectangle(noteX, y, x + w - noteX, capH), Ink, Color.Transparent,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            y += rowH + gap;
        }
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
    // 自绘下拉（皮革主题默认做法：双缓冲 Panel + ContextMenuStrip）
    // ==========================================================================
    void RebuildSel()
    {
        if (selMode != null) { Controls.Remove(selMode); selMode.Dispose(); selMode = null; }
        selMode = new SelectPanel();
        selMode.BackColor = Color.Transparent;
        selMode.ForeColor = Ink;
        selMode.Font = fRowSel;
        selMode.Text = ModeNames[mode];
        selMode.Cursor = Cursors.Hand;
        selMode.Paint += PaintModeSelect;
        selMode.Click += delegate { ShowModeMenu(); };
        Controls.Add(selMode);
        selMode.BringToFront();
    }

    void PaintModeSelect(object sender, PaintEventArgs e)
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
        Rectangle tr = new Rectangle(S(12), 0, p.Width - S(34), p.Height);
        TextRenderer.DrawText(e.Graphics, p.Text, fRowSel, tr,
            p.Enabled ? Ink : Color.FromArgb(0xB8, 0xA5, 0x8E), Color.Transparent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
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
        if (modeMenu == null) BuildModeMenu();
        for (int i = 0; i < modeMenu.Items.Count; i++)
            modeMenu.Items[i].Text = (i == mode ? "\u2713  " : "    ") + ModeNames[i];
        FitMenuSize(modeMenu, ModeNames);
        modeMenu.Show(selMode, new Point(0, selMode.Height + S(2)));
        int itemH = modeMenu.Items[0].Height;
        Log("下拉高度自检：项数=" + ModeNames.Length + " 行高=" + itemH +
            "，弹层高=" + modeMenu.Height +
            "，能装下=" + (modeMenu.Height >= ModeNames.Length * itemH));
        // 框里那一行也要看全：最长一项在框内折成两行后仍然要放得下，量一次记进日志
        int widest = 0, lineH = 0;
        using (Graphics g = CreateGraphics())
        {
            for (int i = 0; i < ModeNames.Length; i++)
            {
                int w = TextRenderer.MeasureText(g, ModeNames[i], fRowSel).Width;
                if (w > widest) widest = w;
            }
            lineH = TextRenderer.MeasureText(g, "国", fRowSel).Height + S(2);
        }
        int boxText = Math.Max(1, selMode.Width - S(34));
        int lines = Math.Max(1, (widest + boxText - 1) / boxText);
        Log("选择框文字自检：框宽=" + selMode.Width + " 文字可用=" + boxText +
            " 最长项=" + widest + "，预计占 " + lines + " 行，框高=" + selMode.Height +
            "，装得下=" + (selMode.Height >= lines * lineH));
    }

    void StepMode(int delta)
    {
        int n = ModeNames.Length;
        mode = ((mode + delta) % n + n) % n;
        selMode.Text = ModeNames[mode];
        selMode.Invalidate();
        Log("选择启动方式：" + ModeNames[mode]);
    }

    void BuildModeMenu()
    {
        modeMenu = new ContextMenuStrip();
        modeMenu.ShowImageMargin = false;
        modeMenu.ShowCheckMargin = false;
        modeMenu.BackColor = LeatherMenuRenderer.MenuBack;
        modeMenu.ForeColor = Ink;
        modeMenu.Font = fRowSel;
        modeMenu.Padding = new Padding(S(4));
        modeMenu.Renderer = new LeatherMenuRenderer();
        for (int i = 0; i < ModeNames.Length; i++)
        {
            int idx = i;
            ToolStripMenuItem item = new ToolStripMenuItem(ModeNames[i]);
            item.ForeColor = Ink;
            item.Padding = new Padding(S(10), S(4), S(10), S(4));
            item.Click += delegate
            {
                mode = idx;
                selMode.Text = ModeNames[idx];
                selMode.Invalidate();
                Log("选择启动方式：" + ModeNames[idx]);
            };
            modeMenu.Items.Add(item);
        }
    }

    // 框本身宽度不动（布局照旧），只把弹层加宽到装得下最长的一项（硬约束 / 规则 20）
    void FitMenuSize(ContextMenuStrip menu, string[] items)
    {
        int widest = 0;
        try
        {
            using (Graphics g = CreateGraphics())
                for (int i = 0; i < items.Length; i++)
                {
                    int w = TextRenderer.MeasureText(g, items[i], menu.Font).Width;
                    if (w > widest) widest = w;
                }
        }
        catch (Exception ex) { Log("量下拉宽度失败：" + ex.Message); return; }
        if (widest <= 0) return;
        int want = widest + S(46);
        int cap = Screen.FromControl(selMode).WorkingArea.Width - S(24);
        menu.MinimumSize = new Size(Math.Max(selMode.Width, Math.Min(want, cap)), 0);
        Log("下拉宽度自检：框宽=" + selMode.Width + "，最长项文字宽=" + widest +
            "，弹层宽=" + menu.MinimumSize.Width +
            "，能装下=" + (menu.MinimumSize.Width >= widest + S(24)));
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

    // ==========================================================================
    // 输入：鼠标 + 键盘（Tab 走 下拉框 → 启动 → 日志；框上 ↑↓ 直接换项）
    // ==========================================================================
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcLog.Contains(pt)) return 1;
        if (!busy && selMode != null && selMode.Bounds.Contains(pt)) return 3;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcLog);
            if (selMode != null) r = Rectangle.Union(r, selMode.Bounds);
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
            case Keys.Down:
                if (!busy && focus == 3) { StepMode(1); return true; }
                break;
            case Keys.Up:
                if (!busy && focus == 3) { StepMode(-1); return true; }
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
        int[] order = busy ? new int[] { 1 } : new int[] { 3, 0, 1 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
        if (i == 3) { ShowModeMenu(); return; }
        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenLog(); return; }
    }

    // ==========================================================================
    // 现场读数（规则 37：取不到的值一律换成安全值并写日志，绝不当 0 用）
    // 老游戏直接拿 Win32 环境值当除数；读数还必须是「同一条路径」，不许"正常就跳过"。
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
            // 盘类型先问：老游戏对「映射盘 / 网络盘 / 内存盘」的几何读数最容易变成 0 除数
            e.driveType = DriveTypeName(GetDriveTypeW(e.root));
            try { e.diskOk = GetDiskFreeSpaceA(e.root, out e.spc, out e.bps, out e.freeC, out e.totalC); }
            catch (Exception ex) { Log("取磁盘参数失败：" + ex.Message); }
        }
        return e;
    }

    // 断言 + 兜底：每个会被当除数的值都要有区间，越界就换安全值并记下用的是什么
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

    // /readings：只读数 + 写日志，不启动游戏（换机器先跑这一条；收尾自检也跑它）
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

    // ---------- 只用得到的读数写日志，不改系统、不还原（CoD2 没有 60Hz 这类需求） ----------
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
    // 画面默认值：射击游戏默认全屏。CoD5 的 r_mode 是 "1920x1080" 字符串（fs_localAppData 重定向后
    // 在游戏目录 players\profiles\<名>\config.cfg，r_fullscreen 是 "1"/"0"，与 CoD2 正则同构）。
    // 同一功能只允许一条路径（规则 24 第 10 类）：每次启动都跑比对——
    //   display.state 没有记录 → 首次规范成「全屏 + 本机桌面分辨率」；
    //   记录在、config 与记录一致 → 按当前桌面刷新（换显示器照样全屏）；
    //   记录在、config 与记录不同 → 玩家在游戏里自己改过 → 永远不再动（玩家设置必须留着）。
    // ==========================================================================
    static string DisplayStatePath() { return Path.Combine(AppDir(), "display.state"); }

    // 安全模式标记清理：WaW 每次启动在真实 %LOCALAPPDATA%\Activision\CoDWaW\ 写 4 字节标记 __CoDWaW
    // （名 = "__" + 主程序名去扩展名，exe 字符串 %s__%s / WIN_IMPROPER_QUIT_TITLE 取证），正常退出时删；
    // 异常退出（强杀/崩溃/断电）残留 → 下次启动弹「以安全模式运行吗？」，选「是」还会 exec safemode.cfg
    // 把画面降回 640×480。**这个标记不受 fs_localAppData 重定向影响**（实测：重定向开着，标记照样落 AppData）。
    // 启动器每次启动前静默删一次兜底；游戏目录同名的也扫一遍（不同版本引擎可能写 fs 根）。幂等、删前记日志。
    void ClearSafeMark()
    {
        try
        {
            string exeBase = Path.GetFileNameWithoutExtension(gameExe);
            string markName = "__" + exeBase;
            string home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = new string[]
            {
                Path.Combine(home, "Activision", "CoDWaW", markName),
                Path.Combine(home, "Activision", exeBase, markName),
                Path.Combine(AppDir(), markName)
            };
            foreach (string m in candidates)
            {
                if (!File.Exists(m)) continue;
                File.Delete(m);
                Log("安全模式标记：已删 " + m + "（上次没正常退出留下的，不删下次启动会弹安全模式询问）");
            }
        }
        catch (Exception ex) { Log("安全模式标记清理失败（继续启动）：" + ex.Message); }
    }

    // 档案自愈 + 存档自动落地：WaW 新电脑首次启动若找不到档案会弹「创建新档案」询问（不是注册表、
    // 不是账号系统，是引擎自己的 profile 机制）。两层保险，全程游戏目录内、零注册表、幂等：
    //   第一层（指针自愈）：档案目录 players\profiles\YunGame 在、active.txt 丢/空/指错 → 写回 "YunGame"
    //     （统一档案名，用户硬规矩）。引擎异常退出有概率重置指针，这是最常见形态。
    //   第二层（存档自动落地）：档案目录整个不在（players 被杀毒清了/分发漏了/新目录）→
    //     从交付留底 _yungame\ 整树恢复（留底镜像 players\ 真实布局：
    //     _yungame\profiles\active.txt + _yungame\profiles\YunGame\{config.cfg, save\*.svg}，
    //     rel 相对 _yungame，目标 = players\<rel>），恢复完照样走画面规范——客户机桌面分辨率不同
    //     会由 NormalizeDisplayDefaults 首次规范兜住。留底也没有（不该发生）→ 不拦，让引擎走创建流程。
    const string ProfileName = "YunGame";

    void EnsureProfile()
    {
        try
        {
            string profiles = Path.Combine(AppDir(), "players", "profiles");
            string profDir = Path.Combine(profiles, ProfileName);
            if (!Directory.Exists(profDir))
            {
                string backup = Path.Combine(AppDir(), "_yungame");
                if (!Directory.Exists(backup))
                {
                    Log("存档落地：游戏目录没有 players\\profiles\\" + ProfileName + " 也没有 _yungame 留底（不该发生）——引擎会弹创建档案，不拦");
                    return;
                }
                // 留底镜像 players\ 布局：rel 相对 _yungame，目标 = players\<rel>
                string playersDir = Path.Combine(AppDir(), "players");
                foreach (string src in Directory.GetFiles(backup, "*", SearchOption.AllDirectories))
                {
                    string rel = src.Substring(backup.Length + 1);
                    string dst = Path.Combine(playersDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    Log("存档落地：恢复 " + rel);
                }
                Log("存档落地：档案目录不在，已从 _yungame 留底整树恢复（通关存档随目录走，玩家无感）");
            }
            string active = Path.Combine(profiles, "active.txt");
            string cur = File.Exists(active) ? File.ReadAllText(active).Trim() : "";
            if (cur == ProfileName)
            {
                Log("档案自愈：active.txt → " + ProfileName + "，就位不动");
                return;
            }
            File.WriteAllText(active, ProfileName, new System.Text.UTF8Encoding(false));
            Log("档案自愈：active.txt 原值[" + cur + "] → 写回 " + ProfileName + "（玩家档案在、指针丢了，自动补回，不弹创建档案）");
        }
        catch (Exception ex) { Log("档案自愈失败（继续启动）：" + ex.Message); }
    }

    // 垫真实 AppData 档案（2026-11-10 实测新增）：WaW 引擎启动极早期（fs_localAppData 重定向
    // 生效前后都有读真实 AppData 的路径），发现真实 AppData 里没有有效档案时会把玩家当"首次
    // 安装"——自建 $$$ 假档案并弹「设置最优设置？」。封法：每次启动前把真实 AppData 侧也垫好
    // （active.txt=YunGame + YunGame\config.cfg 从游戏目录拷一份）。重定向生效后玩家数据照旧
    // 100% 走游戏目录，真实 AppData 这份只是垫底免弹框；换电脑后指纹不匹配最多弹一次，
    // 由点框逻辑替玩家点「否」，引擎随后把新指纹写回 config，之后不再弹。
    void PadRealAppData()
    {
        try
        {
            string real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Activision", "CoDWaW", "players", "profiles");
            Directory.CreateDirectory(real);
            string active = Path.Combine(real, "active.txt");
            string profDir = Path.Combine(real, ProfileName);
            string srcCfg = Path.Combine(AppDir(), "players", "profiles", ProfileName, "config.cfg");
            bool changed = false;
            if (!File.Exists(active) || File.ReadAllText(active).Trim() != ProfileName)
            {
                File.WriteAllText(active, ProfileName, new System.Text.UTF8Encoding(false));
                changed = true;
            }
            if (File.Exists(srcCfg))
            {
                string dstCfg = Path.Combine(profDir, "config.cfg");
                if (!Directory.Exists(profDir) || !File.Exists(dstCfg))
                {
                    Directory.CreateDirectory(profDir);
                    File.Copy(srcCfg, dstCfg, true);
                    changed = true;
                }
            }
            Log(changed
                ? "AppData 垫底：真实 AppData 已垫 " + ProfileName + " 档案（防引擎当首次安装自建假档案弹框）"
                : "AppData 垫底：真实 AppData 档案已在位，不动");
        }
        catch (Exception ex) { Log("AppData 垫底失败（继续启动）：" + ex.Message); }
    }

    void NormalizeDisplayDefaults()
    {
        try
        {
            // fs_localAppData 重定向后，配置在游戏目录 players\profiles\<名>\config.cfg
            string profiles = Path.Combine(AppDir(), "players", "profiles");
            if (!Directory.Exists(profiles))
            {
                Log("画面默认值：还没有 players\\profiles（引擎首次生成配置本来就是全屏），不动");
                return;
            }
            string[] dirs = Directory.GetDirectories(profiles);
            Array.Sort(dirs);
            int n = 0;
            foreach (string d in dirs)
            {
                string cfg = Path.Combine(d, "config.cfg");
                if (File.Exists(cfg)) { NormalizeSubtitles(cfg, DisplayStatePath()); NormalizeOneDisplay(cfg, DisplayStatePath()); n++; }
            }
            if (n == 0) Log("画面默认值：profiles 下没有 config.cfg，不需要处理");
        }
        catch (Exception ex) { Log("画面默认值处理失败（继续启动）：" + ex.Message); }
    }

    // 字幕默认值（2026-11-10 用户定案：「默认开启字幕」，与画面默认值同款账本语义）：
    //   cvar = cg_subtitles（config.cfg 实证，0=关 1=开）。
    //   账本无记录（首次）→ 写 1 并记账，玩家从此有字幕；
    //   记录是 1、config 还是 1 → 一致，只记账；
    //   记录是 1、config 变 0 → 玩家在游戏里自己关了 → 永远尊重，不再动。
    // 账本键 = 画面键 + "@subs"（StateLine 的键匹配是全等/目录前缀，不会互相误中）。
    static bool NormalizeSubtitles(string cfgPath, string statePath)
    {
        try
        {
            Encoding raw = Encoding.GetEncoding(28591);
            string txt = raw.GetString(File.ReadAllBytes(cfgPath));
            string own = Path.GetFileName(Path.GetDirectoryName(cfgPath));
            Match mSub = Regex.Match(txt, "(?m)^seta[ \t]+cg_subtitles[ \t]+\"([01])\"");
            string full = Path.GetFullPath(cfgPath).ToLowerInvariant();
            string app = Path.GetFullPath(AppDir()).ToLowerInvariant();
            if (!app.EndsWith("\\")) app += "\\";
            string key = full.StartsWith(app) ? full.Substring(app.Length) : full;
            string last = StateLine(statePath, key + "@subs");
            if (!mSub.Success)
            {
                // config 还没有字幕行（引擎首次生成的极早期形态）：追加一行开启，用文件自己的行尾风格
                string nl = txt.Contains("\r\n") ? "\r\n" : "\n";
                string tail = txt.EndsWith("\n") ? "" : nl;
                txt += tail + "seta cg_subtitles \"1\"" + nl;
                File.WriteAllBytes(cfgPath, raw.GetBytes(txt));
                SetStateLine(statePath, key + "@subs", "1");
                Log("字幕默认值：" + own + " 的 config 没有 cg_subtitles 行，已追加开启并记账");
                return true;
            }
            string cur = mSub.Groups[1].Value;
            if (last == "1" && cur == "0")
            {
                Log("字幕默认值：" + own + " 玩家自己关了字幕（我们上次记的是开启）→ 不再动，尊重玩家设置");
                return true;
            }
            if (cur == "1")
            {
                SetStateLine(statePath, key + "@subs", "1");
                Log("字幕默认值：" + own + " 字幕已是开启，一字不动，只记账");
                return true;
            }
            // cur == "0" 且我们没有记录：分不清是不是玩家关的，但用户硬规矩是「默认开启」——
            // 按交付初始状态处理，改成 1 并记账；此后玩家再关就有记录，永远尊重
            string patched = Regex.Replace(txt, "(?m)^seta[ \t]+cg_subtitles[ \t]+\"[01]\"", "seta cg_subtitles \"1\"");
            File.WriteAllBytes(cfgPath, raw.GetBytes(patched));
            SetStateLine(statePath, key + "@subs", "1");
            Log("字幕默认值：" + own + " 的 cg_subtitles 是 0 且无我们的记录 → 按交付默认改为开启并记账");
            return true;
        }
        catch (Exception ex) { Log("字幕默认值失败（" + cfgPath + "）：" + ex.Message); return false; }
    }

    static bool NormalizeOneDisplay(string cfgPath, string statePath)
    {
        try
        {
            // Latin1 逐字节往返：config.cfg 可能带繁体注释字节，不许用 UTF-8 解码再写回去吃掉它们
            Encoding raw = Encoding.GetEncoding(28591);
            string txt = raw.GetString(File.ReadAllBytes(cfgPath));
            Match mFs = Regex.Match(txt, "(?m)^(seta[ \t]+r_fullscreen[ \t]+)\"([01])\"");
            Match mMode = Regex.Match(txt, "(?m)^(seta[ \t]+r_mode[ \t]+)\"([^\"]+)\"");
            string own = Path.GetFileName(Path.GetDirectoryName(cfgPath));
            if (!mFs.Success || !mMode.Success)
            {
                Log("画面默认值：" + own + " 的 config 还没有 r_fullscreen/r_mode 行，不动（引擎默认本来就是全屏）");
                return true;
            }
            string wantMode = DesktopModeString();
            // 账本键用**相对游戏目录**的路径（坑 108，2026-11-10 用户要求绿色：客户机任意盘符任意路径，
            // 绝对路径键换盘就失效，还会误判「玩家改过」为「没记录」而覆盖玩家设置）
            string full = Path.GetFullPath(cfgPath).ToLowerInvariant();
            string app = Path.GetFullPath(AppDir()).ToLowerInvariant();
            if (!app.EndsWith("\\")) app += "\\";
            string key = full.StartsWith(app) ? full.Substring(app.Length) : full;
            string last = StateLine(statePath, key);
            string cur = mFs.Groups[2].Value + "|" + mMode.Groups[2].Value;
            if (last != null && cur != last)
            {
                Log("画面默认值：" + own + " 玩家自己改成 " + cur + "（我们上次记的是 " + last + "）→ 不再动，尊重玩家设置");
                return true;
            }
            if (cur == "1|" + wantMode)
            {
                SetStateLine(statePath, key, cur);
                Log("画面默认值：" + own + " 已是全屏 " + wantMode + "，一字不动，只记账");
                return true;
            }
            string patched = Regex.Replace(txt, "(?m)^seta[ \t]+r_fullscreen[ \t]+\"[01]\"", "seta r_fullscreen \"1\"");
            patched = Regex.Replace(patched, "(?m)^seta[ \t]+r_mode[ \t]+\"[^\"]+\"", "seta r_mode \"" + wantMode + "\"");
            File.WriteAllBytes(cfgPath, raw.GetBytes(patched));
            SetStateLine(statePath, key, "1|" + wantMode);
            Log("画面默认值：" + (last == null ? "首次规范" : "跟机刷新") + "：" + own +
                " 的 config 改为全屏 " + wantMode + "（本机桌面），其余行逐字节没动");
            return true;
        }
        catch (Exception ex) { Log("画面默认值失败（" + cfgPath + "）：" + ex.Message); return false; }
    }

    // 目标分辨率 = 本机实测桌面读数（带区间断言和兜底，规则 37）；实在取不到用 1280x720，绝不留空
    static string DesktopModeString()
    {
        EnvRead e = ReadEnvironment();
        AssertReadings(e);
        if (e.cx >= 320 && e.cy >= 240 && e.cx <= 16384 && e.cy <= 16384) return e.cx + "x" + e.cy;
        Log("画面默认值：桌面分辨率取不到（" + e.cx + "×" + e.cy + "），退回 1280x720");
        return "1280x720";
    }

    static string StateLine(string statePath, string key)
    {
        try
        {
            if (!File.Exists(statePath)) return null;
            foreach (string line in File.ReadAllLines(statePath, Encoding.UTF8))
            {
                int i = line.IndexOf('=');
                if (i > 0)
                {
                    string k = line.Substring(0, i).ToLowerInvariant();
                    // 兼容旧账本的绝对路径键（尾部就是相对键），读到后写回自动迁移
                    if (k == key || k.EndsWith("\\" + key)) return line.Substring(i + 1);
                }
            }
        }
        catch (Exception ex) { Log("画面默认值：读账本失败（" + ex.Message + "），按没记录处理"); }
        return null;
    }

    static void SetStateLine(string statePath, string key, string val)
    {
        List<string> lines = new List<string>();
        try { if (File.Exists(statePath)) lines.AddRange(File.ReadAllLines(statePath, Encoding.UTF8)); }
        catch { }
        lines.RemoveAll(delegate (string s)
        {
            int i = s.IndexOf('=');
            if (i <= 0) return false;
            string k = s.Substring(0, i).ToLowerInvariant();
            // 同键的旧绝对路径行一并清掉，账本里不许留盘符（坑 108）
            return k == key || k.EndsWith("\\" + key);
        });
        lines.Add(key + "=" + val);
        File.WriteAllText(statePath, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
    }

    // ==========================================================================
    // 启动游戏
    // ==========================================================================
    // 存档重定向参数（绿色规则 43.1：路径运行时按本机游戏目录动态拼，交付文件里零绝对路径）。
    // fs_localAppData 把 players 存档目录重定向进游戏目录——存档跟着目录走，换电脑自动带过去
    //（fs-repro.ps1 实测：第二次启动起 AppData 零写入，见文件头）。
    string RedirectArgs()
    {
        string d = AppDir();
        return "+set fs_localAppData \"" + d + "\" +set fs_BaseGame \"" + d + "\"";
    }

    // 启动前只读检查：通关存档三件套是否随游戏目录就位（只记日志，不自动恢复——玩家进度优先）
    void CheckSaveData()
    {
        try
        {
            string prof = Path.Combine(AppDir(), "players", "profiles");
            string active = Path.Combine(prof, "active.txt");
            if (!File.Exists(active)) { Log("存档检查：游戏目录没有 players\\profiles\\active.txt（首次启动引擎会自建空白档案）"); return; }
            string cur = File.ReadAllText(active).Trim();
            string saveDir = Path.Combine(prof, cur, "save");
            int n = Directory.Exists(saveDir) ? Directory.GetFiles(saveDir, "*.svg").Length : 0;
            Log("存档检查：当前档案 " + cur + "，游戏目录存档 " + n + " 份（存在 players\\profiles\\ 下，随目录走）");
            if (n == 0) Log("存档检查：save 目录没有 .svg（可能只有设置档案）；通关存档应以 ber*.svg 形式随交付预置");
        }
        catch (Exception ex) { Log("存档检查失败（继续启动）：" + ex.Message); }
    }

    void StartGame()
    {
        if (busy) return;
        layerOk = true;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe;
            statusColor = Warn;
            layerOk = false;
            Invalidate();
            return;
        }
        Log("点「启动游戏」：" + ModeNames[mode] + "（走系统自己的 Direct3D 9，不打兼容层）");
        SetBusy(true, "游戏运行中…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                LogEnvironment();
                ClearSafeMark();
                EnsureProfile();
                PadRealAppData();
                CheckSaveData();
                NormalizeDisplayDefaults();
                string launchArgs = RedirectArgs();
                if (gameArgs.Length > 0) launchArgs += " " + gameArgs;
                Log("启动参数：" + launchArgs);
                DateTime start = DateTime.Now;
                int code = RunGame(exePath, launchArgs);
                int secs = (int)(DateTime.Now - start).TotalSeconds;
                bool abnormal = (uint)code >= 0xC0000000;
                Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                    (abnormal ? "（系统级异常）" : ""));
                if (abnormal) CaptureWerReport(gameExe);
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

    // 游戏必须是本进程的直接子进程：CreateProcess + 工作目录 = 游戏目录，等它退出并原样回传退出码。
    // 不做「包一层再去找实际进程」，少一层少一处出错。
    int RunGame(string exePath, string launchArgs)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        string cmd = "\"" + exePath + "\"" + (launchArgs.Length > 0 ? " " + launchArgs : "");
        bool started = CreateProcess(exePath, cmd, IntPtr.Zero, IntPtr.Zero, false,
            0, IntPtr.Zero, AppDir(), ref si, out pi);
        if (!started)
        {
            int err = Marshal.GetLastWin32Error();
            Log("CreateProcess 失败（err=" + err + "），改用 Process.Start");
            ProcessStartInfo psi = new ProcessStartInfo(exePath, launchArgs);
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = false;
            using (Process game = Process.Start(psi))
            {
                game.WaitForExit();
                Log("游戏退出码：" + game.ExitCode);
                return game.ExitCode;
            }
        }
        Log("已启动游戏 " + Path.GetFileName(exePath) + "，pid=" + pi.dwProcessId + "（本进程的 direct child）");
        // 窗口零干预（铁律 18，2026-11-10 定案）：全屏游戏自己会进前台，启动器对游戏窗口的任何
        // 操作（SetForegroundWindow / ShowWindow / AllowSetForegroundWindow / 移交）都在跟引擎的
        // 全屏切换打架——CoD5 两版实测：每见新窗口就抢 = 画面反复进出（坑 109）；只抢一次 = 游戏
        // 被顶成最小化（坑 114）。正确姿势：启动器拉起后什么都不碰，只做看门狗观察。
        // 启动询问框自动点掉（2026-11-10 三轮实测新增）：WaW 启动期会弹「设置最优设置？」
        // （config 缺硬件指纹）/「建议设置更新」（硬件指纹不匹配，换电脑/驱动更新必弹），
        // 实验证明"抹指纹当第一次运行"封不住框——首次运行它也弹。唯一可靠封法是替玩家点
        // 「否」（按钮 IDNO=7，不改任何设置），点掉后引擎把新指纹写回 config，同机之后不再弹。
        // 限启动后 90s 内、只认该 pid 的 #32770 对话框。与规则 44 边界：44 禁的是对游戏主窗口
        // 的前台/显示干预；这里响应引擎自己的启动询问框，不碰前台不碰显示状态（CoD2 同款先例）。
        // 看门狗（2026-11-09 实测）：播开场视频期间关窗，全屏可能卡在退出流程——
        // 窗口已销毁、进程空转、启动器无限等待 = 平台挂死。等游戏时每 5s 一查：
        // 开局 60s 宽限后，游戏连续 45s 没有任何可见/最小化窗口 → 判卡死：先补送 WM_CLOSE，
        // 10s 仍不退再 TerminateProcess 收尾，全程写日志。玩家正常玩时窗口一直在，不会误杀。
        DateTime t0 = DateTime.Now;
        DateTime lastVisible = t0;
        while (WaitForSingleObject(pi.hProcess, 1000) != 0)
        {
            if ((DateTime.Now - t0).TotalSeconds < 90)
            {
                IntPtr dlg = FindAskDialog(pi.dwProcessId);
                if (dlg != IntPtr.Zero && ClickDialogNo(dlg))
                    Log("启动询问框：已自动替玩家点「否」（设置最优设置/建议设置更新/安全模式，不改任何设置）");
            }
            if (GameHasWindow(pi.dwProcessId)) lastVisible = DateTime.Now;
            if ((DateTime.Now - t0).TotalSeconds > 60 && (DateTime.Now - lastVisible).TotalSeconds > 45)
            {
                Log("看门狗：游戏进程 45s 没有任何窗口（多半卡在退出流程），补送 WM_CLOSE");
                PostCloseToAny(pi.dwProcessId);
                if (WaitForSingleObject(pi.hProcess, 10000) != 0)
                {
                    Log("看门狗：仍不退，按托管契约结束游戏进程（画面设置已由引擎落盘，不留孤儿进程）");
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

    // ---------- 启动询问框自动点掉（2026-11-10，CoD2 已验证框架移植） ----------
    // 弹框是标准 MessageBox 结构，按钮 ID 固定（IDYES=6 / IDNO=7 / IDCANCEL=2 / IDOK=1，低 16 位），
    // 按 ID 匹配跨语言跨码页稳定（3dm 汉化版标题乱码也不怕）。
    const int GWL_ID = -12;
    const uint BM_CLICK = 0x00F5;
    const uint IDYES = 6, IDNO = 7, IDCANCEL = 2, IDOK = 1;

    // 该 pid 的可见 #32770 模态对话框（启动询问框都是这个类）
    static IntPtr FindAskDialog(int pid)
    {
        IntPtr[] h = new IntPtr[] { IntPtr.Zero };
        uint want = (uint)pid;
        EnumWindows(delegate(IntPtr wh, IntPtr lp)
        {
            uint wp;
            GetWindowThreadProcessId(wh, out wp);
            if (wp == want && IsWindowVisible(wh))
            {
                StringBuilder cls = new StringBuilder(64);
                GetClassNameW(wh, cls, 64);
                if (cls.ToString() == "#32770") { h[0] = wh; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return h[0];
    }

    // 点掉询问框：优先「否」（IDNO，不改设置），其次「取消」（IDCANCEL），
    // 只有单按钮「确定」的提示框才点确定。返回 true = 点了。
    static bool ClickDialogNo(IntPtr dlg)
    {
        IntPtr no = IntPtr.Zero, cancel = IntPtr.Zero, ok = IntPtr.Zero;
        int buttons = 0;
        EnumChildWindows(dlg, delegate(IntPtr ch, IntPtr lp)
        {
            StringBuilder cls = new StringBuilder(64);
            GetClassNameW(ch, cls, 64);
            if (cls.ToString() != "Button" || !IsWindowVisible(ch)) return true;
            uint id = (uint)(GetWindowLongW(ch, GWL_ID) & 0xFFFF);
            buttons++;
            if (id == IDNO) no = ch;
            else if (id == IDCANCEL) cancel = ch;
            else if (id == IDOK) ok = ch;
            return true;
        }, IntPtr.Zero);
        IntPtr target = no != IntPtr.Zero ? no
                      : cancel != IntPtr.Zero ? cancel
                      : (buttons == 1 && ok != IntPtr.Zero ? ok : IntPtr.Zero);
        if (target == IntPtr.Zero) return false;
        SendMessageW(target, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        return true;
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
        if (selMode != null) selMode.Enabled = !value;
        if (text != null) status = text;
        if (!value) { status = layerOk ? "已就绪" : FailLayerText; statusColor = layerOk ? Ok : Warn; }
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
    // /selftest <程序> [参数…]：拉一个小程序并原样回传它的退出码。
    // 不启动游戏就能验证「退出码回传」这条托管契约（收尾自检用 cmd.exe /c exit 7 跑一次）。
    // ==========================================================================
    static int SelfTest(string[] args)
    {
        try
        {
            if (args.Length < 1) { Console.Error.WriteLine("用法：game_starter.exe /selftest <程序> [参数…]"); return 2; }
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
    // launcher.ini（UTF-8；值里的 \n 换成换行，左栏文案 / 档位名字都能在外面改）
    // ==========================================================================
    void LoadIni()
    {
        string path = Path.Combine(AppDir(), "launcher.ini");
        if (!File.Exists(path)) return;
        List<string> modes = new List<string>();
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim().Replace("\\n", "\r\n");
            if (key.StartsWith("mode") && key.Length > 4)
            {
                int n;
                if (int.TryParse(key.Substring(4), out n) && n >= 1 && n <= 9)
                {
                    while (modes.Count < n) modes.Add("");
                    modes[n - 1] = val;
                }
                continue;
            }
            switch (key)
            {
                case "title": gameTitle = val; break;
                case "subtitle": gameSubtitle = val; break;
                case "gameexe": gameExe = val; break;
                case "gameargs": gameArgs = val; break;
                case "intro": intro = val; break;
                case "centernote": centerNote = val; break;
                case "combohint": comboHint = val; break;   // 下拉框上方那行用途说明（硬约束 18）
            }
        }
        if (modes.Count >= 2)
        {
            ModeNames = modes.ToArray();
            Log("launcher.ini 覆盖了 " + ModeNames.Length + " 个启动方式名字");
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
