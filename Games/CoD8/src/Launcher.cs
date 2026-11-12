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

// 使命召唤8（Call of Duty: Modern Warfare 3 · 单人版 iw5sp.exe）启动器 game_starter.exe。
// 界面规范：仓库 .qoder/skills/legacy-launcher-gui/SKILL.md + references/leather-recipes.md
//（纯 GDI+ 自绘、三栏 900×620、拟物皮革、全窗口 TextRenderer ClearType、键帽走通用库 Keycaps.cs）。
// 本文件以 Games\CoD7\src\Launcher.cs 为底本移植，砍掉了黑色行动专有的东西：
// 存档指针账本 / mis_01 关卡解锁 / SaveAutoFix（CoD8 的通关存档是 save\*.svg 整包，
// 引擎直接按文件读，没有 sv_lastSave* 指针那一套，也不需要解锁键）。
//
// 已实测确认的事实（2026-11-10，取证见 Games\CoD8\evidence\）：
//   主程序 iw5sp.exe（32 位）；渲染是 Direct3D 9，**只做原生一档**（2026-11-10 用户定案：
//   「只要原生启动、自动按桌面分辨率就行了。其他不要乱搞」——DXVK 档整个砍掉，不打兼容层）。
//   ⚠️ 汉化包的 binkw32.dll（356352B，PDB 路径 c:\Share\C\Trojan\binkw32_cod\，连 www.3dmgame.com）
//   兼职汉化字体渲染：换成原版 bink 游戏不死了但**整屏乱码**（2026-11-10 实测教训，用户明令不许动）；
//   它的联网校验失败时会 15~20 秒杀进程（间歇性，跟网络有关）——**已知风险，不动它**。
//   画面设置由引擎读写 players2\config.cfg：r_displayMode 是 "fullscreen" 字符串（不是 CoD7 的
//   r_fullscreen）、r_mode 是 "1920x1080" 这种字符串、r_imageQuality 0=Native 其余档是 720p 放大。
//   玩家在游戏里改的一切必须留着——启动器只做"默认全屏"的规范：
//   每次启动前比对 config.cfg 的 r_displayMode/r_mode 与 display.state 里我们上次写的值，
//   一致 → 按本机实测桌面分辨率刷新成全屏（换显示器也总是全屏跑）；不一致 → 玩家自己改过 → 从此永远不再动。
//
// 启动器替玩家做的两件事（规则 34：玩家只面对一个界面，不许让他猜乱码）：
//   1) 上次没正常退出会在游戏目录留 4 字节标记 __iw5sp，下次启动弹「是否以安全模式執行？」，
//      选「是」会把画面降回 640×480。启动前静默删掉残留标记并写日志（清游戏目录这份，
//      CoD8 是老布局；CoD7 的用户目录布局见 CoD7 自己的启动器），原版行为不动、不打补丁；
//   2) 画面默认值：射击游戏默认就该全屏——首次把档案规范成「全屏 + 本机桌面分辨率 + 跳过开场 +
//      Native 画质」一次，比对账本 display.state：值还是我们上次写的 → 按当前桌面刷新；
//      变了 → 玩家自己改过，永远不再动。
//
// 命令行开关（都不需要玩家碰，给平台和远程排障用）：
//   /go           零交互按当前档直接启动（平台入口）；/mode=1|2 选档；/hideparent 藏掉父控制台
//   /readings     只采现场读数写日志、不启动游戏（换机器先跑这条）
//   /clearmark    只静默删掉 __iw5sp 那个安全模式标记、不启动游戏（收尾自检拿它验这条逻辑）
//   /restore-exe  把 _orig\ 的原版主程序盖回去（字节补丁的回滚路）、不启动游戏
//   /display "<config.cfg>" "<display.state>"  只按账本规范这两份文件的画面默认值、不启动游戏（收尾自检用它验逻辑）
//   /layer=1|2    只按第 N 档自检并修文件、不启动游戏（收尾自检用它把目录恢复到默认档）
//   /wer "<Report.wer>" [结论另存文件]  只解析崩溃报告（结论必须另存 UTF-8 文件再断言，别读 stdout 中文）
//   /selftest <程序> [参数…]            直接拉起该程序并原样回传退出码（不启动游戏，用来验托管契约）
//   /dxlog=LEVEL  临时改 DXVK 的日志级别（默认 error：不在玩家目录留流水日志；排障用 info/debug）
//
// 平台托管契约（规则 8 / 技能「平台托管契约」）：游戏用 CreateProcess 拉起，是 game_starter.exe 的**直接子进程**、
// 工作目录 = 游戏目录；游戏运行期间本进程陪跑；游戏退出后本进程退出并**原样回传游戏退出码**；
// 玩家关窗口只是隐藏到托盘陪跑，不杀游戏。
// 陪跑带看门狗：播片期间关窗可能让 DXVK 全屏卡在退出流程（窗口没了、进程空转）——
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
    string gameTitle = "使命召唤8：现代战争3";
    string gameSubtitle = "Call of Duty: Modern Warfare 3 · 单人战役";
    string gameExe = "iw5sp.exe";
    string gameArgs = "";
    string safeMark = "";              // 空 = 按 "__" + 主程序名推导（CoD8 是 __iw5sp）
    string layerDir = "_dxvk";         // 留底目录
    string[] layerFiles = new string[] { "d3d9.dll", "dxgi.dll" };
    string layerConf = "dxvk.conf";
    string intro =
        "《使命召唤：现代战争3》\n" +
        "是 Infinity Ward 2011 年的\n" +
        "第一人称射击：第三次世界大战，\n" +
        "从纽约到迪拜到巴黎，\n" +
        "一路追着马卡洛夫打。\n" +
        "\n" +
        "本作是简体中文版，\n" +
        "进菜单选「战役」开打。\n" +
        "\n" +
        "玩法一句：跟队友推进，\n" +
        "找掩体再探头，\n" +
        "准星稳了再扣扳机。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给我们";
    string centerNote = "游戏会自动检测画面。";
    string comboHint = "本下拉框的作用是：如果有闪退等异常，换另一种模式，再启动游戏试试。";

    // ---------- 启动方式（2026-11-10 用户定案：只做原生 Direct3D 9 一档，不打 DXVK） ----------
    // 单元素数组：GUI 下拉/右键菜单仍然可用（显示一行），/mode=2 /layer=2 之类参数越界自动无效。
    // 砍 DXVK 的原因：本机 RX 580 原生 D3D9 健康（探针实测），汉化包在 DXVK 下反而出过卡死黑屏，
    // 用户明令「不要打 dxvk 补丁」。_dxvk\ 留底和层自检代码保留（万一以后要恢复双档，git 里有底）。
    static string[] ModeNames = new string[]
    {
        "1  原生 Direct3D 9"
    };
    const int ModeNative = 0;
    const int ModeDxvk = 1;   // 不再出现在 ModeNames，选不到

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int mode = ModeNative;        // 唯一档：原生
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, layerOk = true;
    string status = "已就绪";
    Color statusColor = Ok;
    volatile int exitCode;
    volatile bool finishing;
    NotifyIcon tray;
    bool autoStart;
    static string dxvkLogLevel = "error";   // 只给游戏进程设，不动系统环境变量

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

    // 按键表：照 players2\config.cfg 的真实 bind 逐条抄（2026-11-10 实机 26 条），
    // CoD8 没有 +leanleft/+leanright/+talk/toggleconsole，别照抄 CoD7 的表。
    static readonly KeyRow[] KeyRows = new KeyRow[]
    {
        new KeyRow("pc:W pc:A pc:S pc:D", "前进 / 后退 / 左右移动"),
        new KeyRow("pc:Space", "跳 / 起身"),
        new KeyRow("pc:Shift", "冲刺 / 屏息"),
        new KeyRow("pc:C", "蹲下（切换）"),
        new KeyRow("pc:Ctrl", "卧倒（切换）"),
        new KeyRow("pc:Mouse1", "开火"),
        new KeyRow("pc:Mouse2", "瞄准"),
        new KeyRow("pc:R", "装弹"),
        new KeyRow("pc:V", "近战"),
        new KeyRow("pc:F", "互动：开门 / 上车"),
        new KeyRow("pc:G pc:Mouse3", "投掷手雷"),
        new KeyRow("pc:Q", "烟雾弹"),
        new KeyRow("pc:1 pc:2", "切换武器"),
        new KeyRow("pc:3 pc:4 pc:5", "装备 / 道具"),
        new KeyRow("pc:N", "特殊装备"),
        new KeyRow("pc:Tab", "看战绩"),
        new KeyRow("pc:Esc", "菜单 / 返回"),
        new KeyRow("pc:F5", "跳过"),
        new KeyRow("pc:Pause", "暂停")
    };

    // CoD8 首次规范默认值（2026-11-10 用户定案：只做「原生启动 + 自动按桌面分辨率」，别的都不动）：
    // intro=0 跳过开场视频；**r_imageQuality=0 才是 Native**（其它档含 3 都是 720p 放大，
    // PCGamingWiki / NVIDIA 论坛 / itsmods 一致），不写它桌面分辨率等于白写（画面还是低清放大）。
    // 不写键位（玩家 bind 一律保留）、不写 loc_language（汉化显示归汉化包管，引擎变量乱试会出乱码）、
    // 不写存档指针（CoD8 没有 sv_lastSave*/mis_01）、不动 binkw32.dll（汉化包的 bink 兼管汉化渲染，
    // 换成原版 bink 会整屏乱码——2026-11-10 实测教训）。
    static readonly string[] PerfDefaults = new string[]
    {
        "intro|0", "r_imageQuality|0"
    };

    // ==========================================================================
    // 入口
    // ==========================================================================
    [STAThread]
    static int Main(string[] args)
    {
        // 参数前缀规范（规则 42，2026-11-10 用户定案）：新配置一律用 --go / --readings 这种双横线前缀，
        // 单斜杠 /go 会被 Git Bash(MSYS) 当 Unix 路径改写成 C:/.../go（坑 98）。
        // 这里把 --xxx 归一化成 /xxx，内部逻辑一处不改；已发出的 /go 平台配置继续兼容、不返工。
        // 日志显示原始参数（rawArgs），便于排障时看清平台到底传了什么。
        string[] rawArgs = (string[])args.Clone();
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith("--") && args[i].Length > 2) args[i] = "/" + args[i].Substring(2);
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
            if (a.StartsWith("/layer") && a.IndexOf('=') > 0)
            {
                int which;
                if (int.TryParse(a.Substring(a.IndexOf('=') + 1), out which) &&
                    which >= 1 && which <= ModeNames.Length)
                {
                    string msg;
                    bool ok = l.EnsureLayer(which - 1, out msg);
                    Log("层自检（/layer=" + which + "）：" + ModeNames[which - 1] + " → " + (ok ? "已就绪" : msg));
                    Console.WriteLine(ok ? "LAYER OK" : "LAYER FAIL " + msg);
                    return ok ? 0 : 1;
                }
            }
            if (a.StartsWith("/dxlog") && a.IndexOf('=') > 0)
            {
                string want = a.Substring(a.IndexOf('=') + 1).ToLowerInvariant();
                if (want == "none" || want == "error" || want == "warn" || want == "info" || want == "debug")
                    dxvkLogLevel = want;
                else Log("/dxlog 取值不认识（" + want + "），仍按 " + dxvkLogLevel);
            }
        }
        string parentName = ParentProcessName();
        // 参数自证（规则 41，2026-11-10）：Git Bash / MSYS 会把 /go 这类斜杠开关转成 Windows 路径
        // （/go → C:/.../go），启动器收不到开关就弹菜单等点击，验证会假通过。这里当场记一行警告，
        // 收尾验证断言"日志里没有这行"即可，不必靠人眼盯。
        foreach (string a in args)
        {
            string low = a.ToLowerInvariant().Replace('\\', '/');
            if (low.Contains(":/") && (low.EndsWith("/go") || low.EndsWith("/readings") ||
                low.EndsWith("/wer") || low.EndsWith("/display") || low.EndsWith("/clearmark") ||
                low.EndsWith("/selftest") || low.Contains("/layer") || low.Contains("/dxlog")))
                Log("【警告】参数疑似被 shell 路径转换：" + a +
                    "（开关没收到，零交互不会生效；验证请走 pwsh 的 Start-Process -ArgumentList）");
        }
        Log("启动参数：" + (rawArgs.Length == 0 ? "（无）" : string.Join(" ", rawArgs)) +
            "；父进程：" + (parentName.Length == 0 ? "未知" : parentName) +
            "；零交互：" + (l.autoStart ? "是" : "否") +
            "；档位：" + ModeNames[l.mode]);

        foreach (string a0 in args)
        {
            // /readings 只读数、/wer 只解析崩溃报告、/selftest 只跑指定的小工具：都不启动游戏
            if (a0 == "/readings" || a0 == "-readings") return DumpReadings();
            // /clearmark：只清「上次没正常退出」那个会弹乱码询问框的标记文件，不启动游戏。
            // 加这个开关是为了让收尾自检能真验一次这段逻辑（否则它只在拉起游戏时才跑到）。
            if (a0 == "/clearmark" || a0 == "-clearmark") { l.ClearSafeModeMark(); return 0; }
            // /restore-exe：把 _orig\ 里的原版主程序盖回去（安全模式询问补丁的回滚路），不启动游戏
            if (a0 == "/restore-exe" || a0 == "-restore-exe") return l.RestoreOrigExe() ? 0 : 1;
            // /display <config.cfg> <display.state>：只对这两份路径做画面默认值规范，不启动游戏（收尾自检 3d 用它验）
            if (a0 == "/display" || a0 == "-display")
            {
                if (args.Length < 3 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe /display \"<config.cfg>\" \"<display.state>\"");
                    return 2;
                }
                bool ok = NormalizeOneDisplay(args[1], args[2]);
                return ok ? 0 : 1;
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
    static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")]
    static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
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
        string msg;
        layerOk = EnsureLayer(mode, out msg);
        status = msg;
        statusColor = layerOk ? Ok : Warn;
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

    // 折行兜底（2026-11-10 用户指出左栏「Treyarch」被拆成 Treyarc/h、逗号引号孤行）：
    // 量出能装下的字符数后做两条禁则——不把英文单词从中间切断、不让标点掉到下一行行首。
    // 中文仍按字断（没有词边界），但配合 ini 里写短的硬换行，就不会再出现断词。
    static bool IsWordChar(char c)
    {
        return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
    }
    static bool IsLeadPunct(char c)
    {
        return "，。、；：？！）」』】》%,.;:?!)]}>".IndexOf(c) >= 0;
    }
    int TakeChars(Graphics g, string line, int width, Font f)
    {
        int take = line.Length;
        while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > width) take--;
        if (take < line.Length)
        {
            // 英文/数字单词别断在中间：退到最近的空格后（退太远就放弃，宁可按字断）
            if (IsWordChar(line[take - 1]) && IsWordChar(line[take]))
            {
                int sp = line.LastIndexOf(' ', Math.Max(0, take - 1));
                if (sp > 0 && take - sp < 12) take = sp + 1;
            }
            // 标点不许做下一行行首：并到上一行（标点窄，不会撑破宽度）
            if (take < line.Length && IsLeadPunct(line[take])) take++;
        }
        return Math.Max(1, Math.Min(take, line.Length));
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
                int take = TakeChars(g, line, width, f);
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
                    int take = TakeChars(g, line, width, font);
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
        Log("DXVK 日志级别=" + dxvkLogLevel + "（只对本次启动的游戏进程生效，不写系统环境变量）");
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
    // 兼容层自检 / 自修（规则 34：缺了自己补，修不了只说一句人话，不指挥玩家做任何一步）
    // ==========================================================================
    bool EnsureLayer(int which, out string message)
    {
        message = "已就绪";
        string dir = AppDir();
        if (!File.Exists(Path.Combine(dir, gameExe)))
        {
            message = "找不到游戏程序 " + gameExe;
            Log("层自检失败：" + message);
            return false;
        }
        string bak = Path.Combine(dir, layerDir);
        if (which == ModeNative) return EnsureNativeD3D9(dir, bak, out message);

        // DXVK 档：根目录那两份要和 _dxvk\ 留底同大小，缺了或不一样就从留底复制上来
        foreach (string f in layerFiles)
        {
            string src = Path.Combine(bak, f);
            string root = Path.Combine(dir, f);
            if (!File.Exists(src))
            {
                message = FailLayerText;
                Log("兼容层自检：留底 " + layerDir + "\\" + f + " 不在，补不出来");
                return false;
            }
            long keepLen = new FileInfo(src).Length;
            bool present = File.Exists(root);
            if (present && new FileInfo(root).Length == keepLen)
            {
                Log("兼容层自检：" + f + " 已就位（与 " + layerDir + " 留底同大小 " + keepLen + "）");
                PruneAsideDuplicate(bak, f, src);
                continue;
            }
            Log("兼容层自检：" + f + (present
                    ? " 与留底大小不一致（" + new FileInfo(root).Length + " vs " + keepLen + "），先存进 " + layerDir + "\\禁用\\ 再复制留底"
                    : " 根目录缺失，从 " + layerDir + " 复制上来"));
            if (present && !StashAside(bak, f, root))
            {
                message = FailLayerText;
                return false;
            }
            try
            {
                File.Copy(src, root, true);
                Log("兼容层自检：已复制 " + layerDir + "\\" + f + " → " + f);
                PruneAsideDuplicate(bak, f, src);
            }
            catch (Exception ex)
            {
                message = FailLayerText;
                Log("兼容层自检：复制 " + f + " 失败：" + ex);
                return false;
            }
        }
        EnsureConf(dir, bak);
        return true;
    }

    // 上一轮「原生档」挪进 _dxvk\禁用\ 的那份，如果和留底是同一份字节就是纯重复（7 MB 白占地方），
    // 删掉；只要字节不同就留着不动（那可能是玩家或排障自己放的文件，不能顺手删）
    void PruneAsideDuplicate(string bak, string name, string keepPath)
    {
        try
        {
            string folder = Path.Combine(bak, "禁用");
            string aside = Path.Combine(folder, name);
            if (File.Exists(aside))
            {
                // 内容不同 = 玩家/排障留下的东西，不能顺手删
                if (FileHash(aside) != FileHash(keepPath)) { Log("兼容层自检：" + layerDir + "\\禁用\\" + name + " 与留底不同，留着不动"); return; }
                File.Delete(aside);
                Log("兼容层自检：删掉 " + layerDir + "\\禁用\\" + name + "（与留底同一份，不留重复）");
            }
            // 空目录也别留在交付目录里（玩家会以为少了东西）
            if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
        }
        catch (Exception ex) { Log("清理 " + layerDir + "\\禁用 失败（不影响启动）：" + ex.Message); }
    }

    static string FileHash(string path)
    {
        using (FileStream fs = File.OpenRead(path))
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(fs));
    }

    // dxvk.conf：缺了从留底补；根目录那份被玩家/排障改过就留着不动（只记一行差异）
    void EnsureConf(string dir, string bak)
    {
        string root = Path.Combine(dir, layerConf);
        string keep = Path.Combine(bak, layerConf);
        if (!File.Exists(root))
        {
            if (!File.Exists(keep)) { Log("兼容层自检：" + layerConf + " 根目录与 " + layerDir + " 都没有，按 DXVK 内置默认跑"); return; }
            try
            {
                File.Copy(keep, root, true);
                Log("兼容层自检：" + layerConf + " 已从 " + layerDir + " 复制上来（左上角出 FPS 才说明层挂上了）");
            }
            catch (Exception ex) { Log("兼容层自检：复制 " + layerConf + " 失败（继续启动）：" + ex.Message); }
            return;
        }
        if (File.Exists(keep) && new FileInfo(root).Length != new FileInfo(keep).Length)
            Log("兼容层自检：" + layerConf + " 与 " + layerDir + " 留底大小不同（可能排障时改过），保持根目录那份不动");
        else
            Log("兼容层自检：" + layerConf + " 已就位");
    }

    // 原生 Direct3D 9 档：根目录不许留着 DXVK 那两份 d3d9.dll / dxgi.dll，
    // 移进 _dxvk\禁用\（留底 _dxvk\ 一直都在），下一次选 DXVK 档会自动复制回来
    bool EnsureNativeD3D9(string dir, string bak, out string message)
    {
        message = "已就绪";
        foreach (string f in layerFiles)
        {
            string root = Path.Combine(dir, f);
            if (!File.Exists(root)) { Log("原生画面档：根目录本来就没有 " + f); continue; }
            if (!StashAside(bak, f, root))
            {
                message = FailLayerText;
                return false;
            }
            Log("原生画面档：已把 " + f + " 移进 " + layerDir + "\\禁用\\，这次由系统自己的 d3d9 接手");
        }
        return true;
    }

    bool StashAside(string bak, string name, string rootPath)
    {
        try
        {
            string aside = Path.Combine(bak, "禁用");
            Directory.CreateDirectory(aside);
            string dst = Path.Combine(aside, name);
            if (File.Exists(dst))
            {
                if (FileHash(dst) == FileHash(rootPath)) File.Delete(dst);      // 同一份，直接覆盖
                // 内容不同的那份可能是玩家/排障留下的文件，不许顺手吃掉 → 换个带时间的名字留着
                else dst = Path.Combine(aside, name + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            }
            File.Move(rootPath, dst);
            return true;
        }
        catch (Exception ex)
        {
            Log("挪开 " + name + " 失败：" + ex.Message);
            return false;
        }
    }

    // 上次没正常退出的标记：删掉它，玩家就不会看到「Run In Safe Mode?」询问框。
    // 2026-11-10 实机：CoD8 的标记在**游戏目录** __iw5sp（4 字节 PID），
    // 和 CoD2 一样；CoD7 则在用户目录（两套都清，换机器/换版本都兜得住）。原 CoD7 说明：
    // %LOCALAPPDATA%\Activision\CoD\__BlackOps（4 字节 = 上次进程 PID，异常退出时写，
    // 下次启动比对不上就弹框）——之前一直删游戏目录那份（CoD2 老布局），删错了地方。
    // 两处都清：老布局兼容 + 真位置。只删这个 4 字节运行产物，不碰其他文件。
    void ClearSafeModeMark()
    {
        string mark = safeMark.Length > 0
            ? safeMark
            : "__" + Path.GetFileNameWithoutExtension(gameExe);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] paths = new string[]
        {
            Path.Combine(AppDir(), mark),
            Path.Combine(local, "Activision", "CoD", mark)
        };
        foreach (string p in paths)
        {
            try
            {
                if (!File.Exists(p)) { Log("安全模式标记：不在 " + p); continue; }
                File.SetAttributes(p, FileAttributes.Normal);
                File.Delete(p);
                Log("安全模式标记：已静默删掉 " + p + "（4 字节 PID 标记，上次异常退出留下的）");
            }
            catch (Exception ex) { Log("安全模式标记删不掉（继续启动）：" + p + " " + ex.Message); }
        }
    }

    // 主程序打过「跳过安全模式询问框」的字节补丁，原版留在 _orig\；这是它的回滚路
    bool RestoreOrigExe()
    {
        string src = Path.Combine(AppDir(), "_orig", gameExe);
        string dst = Path.Combine(AppDir(), gameExe);
        try
        {
            if (!File.Exists(src)) { Log("回滚：没有 " + src + "，无从回滚"); return false; }
            File.Copy(src, dst, true);
            Log("回滚：已从 _orig 恢复原版主程序（安全模式询问框恢复原版行为）");
            return true;
        }
        catch (Exception ex) { Log("回滚失败：" + ex.Message); return false; }
    }

    // ==========================================================================
    // 画面默认值：射击游戏默认全屏。CoD2 的 r_mode 是 "1920x1080" 字符串（引擎无 "-1/桌面" 语义，
    // 二进制取证确认），交付档案的初始值是开发机测试留下的窗口档——放到客户机就开窗。
    // 同一功能只允许一条路径（规则 24 第 10 类）：每次启动都跑比对——
    //   display.state 没有记录 → 首次规范成「全屏 + 本机桌面分辨率」；
    //   记录在、config 与记录一致 → 按当前桌面刷新（换显示器照样全屏）；
    //   记录在、config 与记录不同 → 玩家在游戏里自己改过 → 永远不再动（玩家设置必须留着）。
    // ==========================================================================
    static string DisplayStatePath() { return Path.Combine(AppDir(), "display.state"); }

    void NormalizeDisplayDefaults()
    {
        // CoD8（IW5）布局：config 在 players2\config.cfg（单人），不是 CoD7 的 players\、
        // 也不是 CoD2 的 main\players\<名>\。config_mp.cfg 属于联机版，本项目不接反作弊网游，不碰。
        try
        {
            string cfg = Path.Combine(AppDir(), "players2", "config.cfg");
            if (File.Exists(cfg)) NormalizeOneDisplay(cfg, DisplayStatePath());
            else Log("画面默认值：还没有 players2\\config.cfg（引擎首次生成配置本来就是全屏），不动");
        }
        catch (Exception ex) { Log("画面默认值处理失败（继续启动）：" + ex.Message); }
    }

    static bool NormalizeOneDisplay(string cfgPath, string statePath)
    {
        try
        {
            // Latin1 逐字节往返：config.cfg 可能带本地化字节，不许用 UTF-8 解码再写回去吃掉它们（坑 95）
            Encoding raw = Encoding.GetEncoding(28591);
            string txt = raw.GetString(File.ReadAllBytes(cfgPath));
            Match mFs = Regex.Match(txt, "(?m)^(seta[ \t]+r_displayMode[ \t]+)\"([^\"]+)\"");
            Match mMode = Regex.Match(txt, "(?m)^(seta[ \t]+r_mode[ \t]+)\"([^\"]+)\"");
            string own = Path.GetFileName(Path.GetDirectoryName(cfgPath));
            if (!mFs.Success || !mMode.Success)
            {
                Log("画面默认值：" + own + " 的 config 还没有 r_displayMode/r_mode 行，不动");
                return true;
            }
            string wantMode = DesktopModeString();
            // 账本键用**相对游戏目录**的路径（2026-11-10 用户指出：客户机在 X 盘，绝对路径键换盘就失效；
            // 更糟的是键对不上会被误判成「没记录」→ 首次规范覆盖玩家自己改的画面设置）。
            // config 在 AppDir 下 → 相对键（如 players2\config.cfg）；不在（/display 自检传外部路径）→ 原样。
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
            string patched = txt;
            bool first = (last == null);
            string note = "";
            if (first)
            {
                // 首次规范：只写跳过开场 + Native 画质两项（2026-11-10 用户定案：其他一律不动）
                int changed = 0;
                foreach (string pd in PerfDefaults)
                {
                    string[] kv = pd.Split('|');
                    string pat = "(?m)^seta[ \t]+" + Regex.Escape(kv[0]) + "[ \t]+\"[^\"]*\"";
                    string line = "seta " + kv[0] + " \"" + kv[1] + "\"";
                    if (Regex.IsMatch(patched, pat)) patched = Regex.Replace(patched, pat, line);
                    else patched = patched.TrimEnd('\r', '\n') + "\n" + line + "\n";
                    changed++;
                }
                note = "，交付默认 " + changed + " 项已写入（跳过开场/Native 画质）";
            }
            // 画面：全屏 + 本机实测桌面分辨率（规则 40，目标值现场读）
            patched = Regex.Replace(patched, "(?m)^seta[ \t]+r_displayMode[ \t]+\"[^\"]*\"", "seta r_displayMode \"fullscreen\"");
            patched = Regex.Replace(patched, "(?m)^seta[ \t]+r_mode[ \t]+\"[^\"]*\"", "seta r_mode \"" + wantMode + "\"");
            if (patched != txt) File.WriteAllBytes(cfgPath, raw.GetBytes(patched));
            SetStateLine(statePath, key, "fullscreen|" + wantMode);
            Log("画面默认值：" + (first ? "首次规范" : "跟机刷新") + "：" + own +
                " 改为全屏 " + wantMode + "（本机桌面）" + note +
                (patched == txt ? "，其余一字不动只记账" : ""));
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
                    // 兼容旧账本：2026-11-10 前的键是绝对路径（如 d:\yungame\...\players2\config.cfg），
                    // 尾部就是现在的相对键 —— 结尾匹配上视为同一条，读出来后写回时迁移成相对键
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
            // 同键的旧绝对路径行也一并清掉（尾部匹配 = 旧版账本遗留），只留相对键一行
            return k == key || k.EndsWith("\\" + key);
        });
        lines.Add(key + "=" + val);
        File.WriteAllText(statePath, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
    }

    // ==========================================================================
    // 启动游戏
    // ==========================================================================
    void StartGame()
    {
        if (busy) return;
        string message;
        int chosen = mode;
        if (!EnsureLayer(chosen, out message))
        {
            status = message;
            statusColor = Warn;
            layerOk = false;
            Invalidate();
            return;
        }
        layerOk = true;
        string exePath = Path.Combine(AppDir(), gameExe);
        Log("点「启动游戏」：" + ModeNames[chosen] +
            (chosen == ModeDxvk ? "（这档用 _dxvk 留底那份 d3d9.dll，走 Vulkan）"
                                : "（这档根目录不留 DXVK 文件，走系统自己的 Direct3D 9）"));
        SetBusy(true, "游戏运行中…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                LogEnvironment();
                ClearSafeModeMark();
                NormalizeDisplayDefaults();
                // CoD7 布局：config 在根 players\；存档指针单独一本账，每次启动替破解版提交
                DateTime start = DateTime.Now;
                int code = RunGame(exePath, gameArgs);
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

    // 启动方式（2026-11-10 用户两次纠正后定案）：**cmd start /D 钉死工作目录**。
    // 教训链（都在本日实测）：CreateProcess 直连 → 黑屏；explorer 命令行拉起 → 弹「不能加载
    // localization.txt」——explorer 拉进程时工作目录继承 explorer 自己的，不是游戏目录；
    // 而 IW 引擎全靠相对路径找资源（localization.txt / main\ / zone\），工作目录一错就死。
    // 双击为什么正常？资源管理器双击 = ShellExecuteEx(lpDirectory=游戏目录)，工作目录是对的。
    // 所以正解 = cmd start：start 内部就是 ShellExecuteEx（双击同路），/D 显式把工作目录
    // 钉在游戏目录（= 补上双击会做、explorer 命令行不做的那件事），/wait 让 cmd 等游戏退出。
    // 陪跑：按进程名等游戏进程出现，之后按 pid 找窗口（移交/点框/看门狗），退出码取游戏本体。
    int RunGame(string exePath, string launchArgs)
    {
        // start 的第一个引号参数是窗口标题（空 = 不改标题）；/D 必须在 /wait 后、exe 前面
        string cmdLine = "\"cmd.exe\" /d /c start \"\" /wait /D \"" + AppDir() + "\" \"" + exePath + "\"" +
            (launchArgs.Length > 0 ? " " + launchArgs : "");
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        // CREATE_NOWINDOW：cmd 只是跳板，不许给它弹黑控制台窗（2026-11-10 用户明令）
        bool started = CreateProcess(Path.Combine(Environment.SystemDirectory, "cmd.exe"), cmdLine,
            IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero, AppDir(), ref si, out pi);
        if (!started)
        {
            // 兜底：ShellExecute 且显式设工作目录（.NET 会把它传给 ShellExecuteEx 的 lpDirectory，与双击等价）
            Log("CreateProcess(cmd) 失败（err=" + Marshal.GetLastWin32Error() + "），退回 ShellExecute（工作目录=游戏目录）");
            ProcessStartInfo psi = new ProcessStartInfo(exePath, launchArgs);
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = true;
            using (Process g = Process.Start(psi)) { g.WaitForExit(); Log("游戏退出码：" + g.ExitCode); return g.ExitCode; }
        }
        Log("已启动游戏（cmd start /D，工作目录钉在 " + AppDir() + "）：cmd pid=" + pi.dwProcessId);
        // start 已把游戏交出去，cmd 在 /wait：按进程名等到游戏本体（最多 20s），之后按 pid 陪跑
        Process gp = null;
        for (int i = 0; i < 40 && gp == null; i++)
        {
            Thread.Sleep(500);
            Process[] ps = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(s_gameExe));
            if (ps.Length > 0) { gp = ps[0]; break; }
        }
        if (gp == null)
        {
            uint c0;
            bool gotCode = GetExitCodeProcess(pi.hProcess, out c0);
            Log("20s 内没等到游戏进程（cmd 退出码=" + (gotCode ? ((int)c0).ToString() : "?") + "），放弃陪跑，按 cmd 退出码回传");
            CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
            return gotCode ? (int)c0 : 0;
        }
        Log("游戏进程已出现，pid=" + gp.Id + "，开始陪跑");
        AllowSetForegroundWindow(0xFFFFFFFFu);   // ASFW_ANY：把前台让给游戏
        // 看门狗（2026-11-09 实测）：播开场视频期间关窗，全屏可能卡在退出流程——
        // 窗口已销毁、进程空转、启动器无限等待 = 平台挂死。每秒一查：
        // 开局 60s 宽限后，游戏连续 45s 没有任何可见/最小化窗口 → 判卡死：先补送 WM_CLOSE，
        // 10s 仍不退再按进程名结束游戏，全程写日志。玩家正常玩时窗口一直在，不会误杀。
        DateTime t0 = DateTime.Now;
        DateTime lastVisible = t0;
        IntPtr fgGiven = IntPtr.Zero;
        while (true)
        {
            gp.Refresh();
            if (gp.HasExited) break;
            Thread.Sleep(1000);
            gp.Refresh();
            if (gp.HasExited) break;
            if (GameHasWindow(gp.Id)) lastVisible = DateTime.Now;
            // 前台移交：只在前 30s 内做，避免玩家切走后被打扰
            if ((DateTime.Now - t0).TotalSeconds < 30)
            {
                // 只移交一次（fgGiven == Zero 时做一次就封手）：游戏全屏初始化会反复重建/最小化窗口，
                // 若每见新窗口就抢前台，就和游戏自己的全屏切换打架 = 画面「进→切出→又进」来回跳
                //（2026-11-10 用户实测反馈，CoD8 特有）。一次移交足够把玩家带进游戏。
                if (fgGiven == IntPtr.Zero)
                {
                    IntPtr gw = FirstGameWindow(gp.Id);
                    if (gw != IntPtr.Zero)
                    {
                        fgGiven = gw;
                        if (IsIconic(gw)) ShowWindow(gw, 9);   // SW_RESTORE
                        SetForegroundWindow(gw);
                        Log("游戏窗口已移交给前台（仅此一次，之后不再抢）");
                    }
                }
            }
            // 启动自检提示框兜底（CoD8：硬件改变/推荐设置/安全模式等）：标记文件已删，万一别的原因仍弹（窗口标题含 Safe Mode），
            // 自动点「否」按正常模式继续——玩家不该被这种技术性提问卡住（2026-11-10 用户要求）
            if ((DateTime.Now - t0).TotalSeconds < 60)
            {
                IntPtr ask = FindSafeModeDialog(gp.Id);
                if (ask != IntPtr.Zero)
                {
                    if (ClickSafeModeNo(ask)) Log("启动提示框：已自动点掉（硬件/安全模式等可选询问，不打扰玩家）");
                }
            }
            else if ((DateTime.Now - t0).TotalSeconds > 60 && (DateTime.Now - lastVisible).TotalSeconds > 45)
            {
                Log("看门狗：游戏进程 45s 没有任何窗口（多半卡在退出流程），补送 WM_CLOSE");
                PostCloseToAny(gp.Id);
                gp.WaitForExit(10000);
                gp.Refresh();
                if (!gp.HasExited)
                {
                    Log("看门狗：仍不退，按托管契约结束游戏进程（画面设置已由引擎落盘，不留孤儿进程）");
                    try { gp.Kill(); } catch { }
                    gp.WaitForExit(10000);
                }
                break;
            }
        }
        int exitCode = 0;
        try { exitCode = gp.ExitCode; } catch { }
        Log("游戏退出码：" + exitCode);
        // 收尾：等 cmd（/wait 会随游戏一起退）退出并释放句柄，不留孤儿
        WaitForSingleObject(pi.hProcess, 10000);
        CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
        return exitCode;
    }

    // 窗口归属判定（2026-11-10 改造）：游戏经 cmd start 拉起后不再是 direct child，
    // 不能按 pid 认窗口 —— 改按「窗口所属进程的可执行名 == 游戏 exe 名」认。
    // pid → 进程名做缓存（每秒枚举一轮，别反复跨进程查询）。
    static string s_gameExe = "iw5sp.exe";               // LoadIni 后同步成 ini 里的 GameExe
    static readonly Dictionary<int, string> s_pidName = new Dictionary<int, string>();

    static string PidImageName(int pid)
    {
        string n;
        if (s_pidName.TryGetValue(pid, out n)) return n;
        try { using (Process p = Process.GetProcessById(pid)) n = p.ProcessName; }
        catch { n = null; }   // 进程已退出：缓存 null，别反复抛异常
        s_pidName[pid] = n;
        return n;
    }

    static bool WindowBelongsToGame(IntPtr hwnd)
    {
        uint p;
        GetWindowThreadProcessId(hwnd, out p);
        if (p == 0) return false;
        string n = PidImageName((int)p);
        return n != null && n.Equals(Path.GetFileNameWithoutExtension(s_gameExe), StringComparison.OrdinalIgnoreCase);
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

    // 前台移交用：该 pid 的第一个可见顶层窗口
    static IntPtr FirstGameWindow(int pid)
    {
        IntPtr[] h = new IntPtr[] { IntPtr.Zero };
        uint want = (uint)pid;
        EnumWindows(delegate(IntPtr wh, IntPtr lp)
        {
            uint wp;
            GetWindowThreadProcessId(wh, out wp);
            if (wp == want && IsWindowVisible(wh)) { h[0] = wh; return false; }
            return true;
        }, IntPtr.Zero);
        return h[0];
    }

    // 看门狗收尾：按进程名结束游戏（cmd 不能 Terminate——杀了它游戏变孤儿）
    static void TerminateGameProcesses()
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(s_gameExe)))
                try { p.Kill(); } catch { }
        }
        catch { }
    }

    // 安全模式询问框兜底：该 pid 的可见对话框，标题含 Safe Mode（引擎的问框，主 exe 里没有这个字符串，
    // 标题来自引擎资源，所以按标题认而不是按类名）
    // 启动自检提示框白名单（localization.txt 里的 WIN_* 标题，2026-11-10 CoD8 实机）：
    // 硬件改变 / 推荐设置更新 / 上次没正常退出 / 低内存 / 色深 / 写文件失败。
    // 这些都是「问你要不要改设置」的可选提示，不是致命错误——一律替玩家点掉，不打扰。
    static readonly string[] StartupDialogTitles = new string[]
    {
        "Safe Mode", "Set Optimal Settings", "Recommended Settings Updated",
        "Low Memory", "Low Desktop Color Depth", "File Write Error"
    };
    static IntPtr FindSafeModeDialog(int pid)
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
                GetClassName(wh, cls, 64);
                StringBuilder sb = new StringBuilder(128);
                GetWindowText(wh, sb, 128);
                string title = sb.ToString();
                if (cls.ToString() == "#32770")
                    foreach (string t in StartupDialogTitles)
                        if (title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) { h[0] = wh; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return h[0];
    }

    // 优先点「否 / No / 取消 / Cancel」（绝不点「是」，那会真改玩家设置/进安全模式）；
    // 只有单个「确定 / OK」的提示框才点它。按系统语言匹配按钮文字。
    static bool ClickSafeModeNo(IntPtr dlg)
    {
        List<IntPtr> buttons = new List<IntPtr>();
        List<string> texts = new List<string>();
        EnumChildWindows(dlg, delegate(IntPtr ch, IntPtr lp)
        {
            StringBuilder cls = new StringBuilder(64);
            GetClassName(ch, cls, 64);
            if (cls.ToString() == "Button" && IsWindowVisible(ch))
            {
                StringBuilder txt = new StringBuilder(64);
                GetWindowText(ch, txt, 64);
                buttons.Add(ch); texts.Add(txt.ToString().Trim());
            }
            return true;
        }, IntPtr.Zero);
        // 第一轮：否 / 取消
        for (int i = 0; i < buttons.Count; i++)
        {
            string t = texts[i];
            if (t.StartsWith("否") || t.StartsWith("取消") ||
                t.StartsWith("No") || t.StartsWith("Cancel"))
            { SendMessageW(buttons[i], 0x00F5, IntPtr.Zero, IntPtr.Zero); return true; }
        }
        // 第二轮：只有「确定 / OK」的框（如 Recommended Settings Updated），点掉即可
        if (buttons.Count == 1)
        {
            string t = texts[0];
            if (t.StartsWith("确定") || t.StartsWith("OK") || t.StartsWith("是"))
            { SendMessageW(buttons[0], 0x00F5, IntPtr.Zero, IntPtr.Zero); return true; }
        }
        return false;
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
                case "gameexe": gameExe = val; s_gameExe = val; break;   // 窗口归属按进程名认，跟 GameExe 同步
                case "gameargs": gameArgs = val; break;
                case "safemark": safeMark = val; break;
                case "layerdir": layerDir = val; break;
                case "layerfiles":
                    List<string> fs = new List<string>();
                    foreach (string s in val.Split(',')) if (s.Trim().Length > 0) fs.Add(s.Trim());
                    if (fs.Count > 0) layerFiles = fs.ToArray();
                    break;
                case "layerconf": layerConf = val; break;
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
