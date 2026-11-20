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

// 血战上海滩（南京祖龙 2003，Angelica 引擎 · D3D8）启动器 game_starter.exe。
// 界面规范：仓库 .qoder/skills/legacy-launcher-gui/SKILL.md + references/leather-recipes.md
//（纯 GDI+ 自绘、三栏 900×620、拟物皮革、全窗口 TextRenderer ClearType、键帽走通用库 Keycaps.cs）。
// 本文件以 Games\CoD5\src\Launcher.cs 为底本移植：砍掉 CoD5 专有的存档档案/画面账本/安全模式标记
//（上海滩的存档和配置全在游戏目录 ini\ 下，不碰注册表、不碰 AppData），环境读数与崩溃取证
// 拆成独立 Env.cs / Crash.cs（与 Launcher.cs 一起编译），新增上海滩专有的切桌面铺满与兼容层自检。
//
// 已实测确认的事实（2026-11-15，取证见 Games\Shanghai\evidence\）：
//   主程序 shanghai.exe（32 位，D3D8，Angelica 引擎）。**不带 -windows 直接崩**：
//   游戏自己切显示模式，Win11 上 A3DDevice::Init 报 Can not change display mode!（实机取证）。
//   带 -windows 后窗口 800×600 缩在左上角（说明(必看).txt 的社区老办法就是 -windows + 桌面降
//   800×600，和实机表现一致）。引擎固定创建 800×600 设备（A3D.log 实锤 Create device 800x600x16），
//   窗口大小由引擎 SetWindow 决定；游戏内画面设置（说明.txt 提到可设 1024×768）存在加密的
//   ini\options.ini 里，启动器不碰。三条 D3D8 路径本机探针全通：原生 SYSTEM32\d3d8.dll、
//   DXVK 2.5.3（Vulkan）、dgVoodoo2（输出走 D3D11，进程模块实测）。
//
//   启动方案（2026-11-15 用户定案）：默认档「铺满」= 启动器临时把桌面切到 800×600@60（Env.Apply，
//   目标模式取不到就退最近的 4:3），游戏窗口 800×600 正好铺满，游戏退出/崩溃/关窗后 Env.Restore
//   自动还原（try/finally 兜底，玩家永远不用自己碰显示设置）。
//   备用档「画面异常时」= 桌面一动不动 + -windows（窗口 800×600，引擎固定行为）。
//   两档文案用仓库规范标准：「游戏自动检测（推荐）」/「画面异常时（固定桌面分辨率）」。
//
//   【2026-11-15 用户定案：兼容层整个移除】玩家实测挂层后游戏卡死（画面停、声音在），且目标
//   玩家是不懂电脑的老年人——层在任何环节出问题都是灾难。定案：走系统原生 D3D8（探针实测
//   本机原生通，游戏首次自跑 A3D.log 也证实原生能建设备），游戏目录不放任何层文件；
//   启动器若检测到目录里有层残留会自动清掉（幂等自愈，防旧版升级上来带病运行）。
//   铺满效果不依赖层：靠启动器临时切桌面 800×600 实现，已实机验证可靠。
//
//   游戏内画面设置在 ini\options.ini，控制台秘籍（~ 键）抄自游戏自带说明(必看).txt：god_on /
//   god_off / haveallweapon / addammo / ammonolimit。右栏键位表只写有出处的（游戏自带说明 +
//   游戏公知操作），不自编键位。
//
// 命令行开关（都不需要玩家碰，给平台和远程排障用）：
//   /go           零交互按当前档直接启动（平台入口）；/mode=1 选档；/hideparent 藏掉父控制台
//   /readings     只采现场读数写日志、不启动游戏（换机器先跑这条）
//   /selftest <程序> [参数…]            直接拉起该程序并原样回传退出码（不启动游戏，用来验托管契约）
//   /wer "<Report.wer>" [结论另存文件]  只解析崩溃报告（结论必须另存 UTF-8 文件再断言，别读 stdout 中文）
//   --restore=w,h,rate,pid   恢复进程模式（内部用，玩家永远碰不到）：等 pid 退出后把桌面还原到
//                            w×h@rate 再自杀。启动器切桌面前会拉起一个，就算启动器自己被任务
//                            管理器强杀，桌面也能被这条独立的命救回来（老年玩家双保险）。
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
    string gameTitle = "血战上海滩";
    string gameSubtitle = "抗日：血战上海滩 · 单人战役";
    string gameExe = "shanghai.exe";
    string gameArgs = "-windows";      // 引擎不做模式切换（Can not change display mode!），-windows 必带；ini 可追加
    string intro =
        "《血战上海滩》\n" +
        "是南京祖龙 2003 年的\n" +
        "抗日射击游戏：淞沪会战，\n" +
        "一人一枪打回上海滩，\n" +
        "是国产游戏的老经典。\n" +
        "\n" +
        "本作是简体中文版，\n" +
        "存档和设置都在\n" +
        "游戏目录 ini\\ 里，\n" +
        "拷走整个目录都带着。\n" +
        "\n" +
        "玩法一句：鼠标瞄准，\n" +
        "见鬼子就打，\n" +
        "手雷留给机枪点。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给我们";
    string centerNote = "游戏会自动检测画面。";
    string comboHint = "启动方式固定为窗口铺满；如果游戏有异常，换第二档再启动试试。";

    // ---------- 启动方式（两档，文案用仓库规范标准；ini 可覆盖名字） ----------
    static string[] ModeNames = new string[]
    {
        "游戏自动检测（推荐）",
        "画面异常时（固定桌面分辨率）"
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
    bool autoStart;
    Cheats.Cheat pendingCheat;      // --cheat=名：单独用=控制台直接发；配 --go=游戏起来后自动发
    int[] restoreArgs;              // --restore=w,h,rate,pid：恢复进程模式参数（Main 里消费）

    // ---------- 字体 / 布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcCenterHead, rcCenterNote;
    Rectangle rcLabelMode, rcComboHint, rcComboMode, rcStart, rcLog;
    Rectangle rcCheatHead;                       // 秘籍区小标题
    Rectangle[] rcCheats = new Rectangle[Cheats.All.Length];   // 秘籍按钮（两行 3+3）
    // 秘籍按钮的 hover/focus 索引从 4 开始（0=启动 1=日志 3=下拉），不与现有冲突
    const int CheatIndexBase = 4;
    SelectPanel selMode;
    ContextMenuStrip modeMenu;

    class KeyRow
    {
        public string Caps;     // 通用键帽库的 id 串（templates\Keycaps.cs）
        public string Note;
        public KeyRow(string caps, string note) { Caps = caps; Note = note; }
    }

    // 按键/秘籍表：出处 = 游戏自带说明(必看).txt（用户 2026-11-15 逐条确认），逐条照抄，不自编键位。
    static readonly KeyRow[] KeyRows = new KeyRow[]
    {
        new KeyRow("pc:Mouse1", "开火"),
        new KeyRow("pc:Mouse2", "换弹夹"),
        new KeyRow("pc:1 pc:2 pc:3 pc:4 pc:5 pc:6 pc:7", "切换武器"),
        new KeyRow("—", "1手枪 2步枪 3手榴弹 4冲锋枪"),
        new KeyRow("—", "5轻机枪 6巴祖卡 7重机枪"),
        new KeyRow("pc:P", "暂停"),
        new KeyRow("pc:H", "提示"),
        new KeyRow("pc:~", "控制台：再按一次 ~ 关闭"),
        new KeyRow("—", "秘籍（先按 ~ 打开控制台）："),
        new KeyRow("—", "god_on 无敌 / god_off 解除"),
        new KeyRow("—", "haveallweapon 全部武器"),
        new KeyRow("—", "addammo 补满弹药"),
        new KeyRow("—", "ammonolimit 子弹无限")
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
            // 档位参数兼容 /mode=N、--mode=N、-mode=N（平台入口写法不统一，别让它静默回落到默认档）
            string modePrefix = null;
            if (a.StartsWith("/mode=")) modePrefix = "/mode=";
            else if (a.StartsWith("--mode=")) modePrefix = "--mode=";
            else if (a.StartsWith("-mode=")) modePrefix = "-mode=";
            if (modePrefix != null &&
                int.TryParse(a.Substring(modePrefix.Length), out picked) &&
                picked >= 1 && picked <= ModeNames.Length) l.mode = picked - 1;
            // 秘籍参数兼容 /cheat=名、--cheat=名、-cheat=名：
            //   单独用 = 立即对运行中的游戏发（测试取证通道）；
            //   配 --go = 游戏窗口就绪后自动发（一键开挂链路）。
            string cheatPrefix = null;
            if (a.StartsWith("/cheat=")) cheatPrefix = "/cheat=";
            else if (a.StartsWith("--cheat=")) cheatPrefix = "--cheat=";
            else if (a.StartsWith("-cheat=")) cheatPrefix = "-cheat=";
            if (cheatPrefix != null)
            {
                string id = a.Substring(cheatPrefix.Length).Trim().ToLowerInvariant();
                foreach (Cheats.Cheat c in Cheats.All)
                    if (c.Id == id) { l.pendingCheat = c; break; }
            }
            // 恢复进程参数：--restore=w,h,rate,pid（三前缀兼容）
            string restPrefix = null;
            if (a.StartsWith("/restore=")) restPrefix = "/restore=";
            else if (a.StartsWith("--restore=")) restPrefix = "--restore=";
            else if (a.StartsWith("-restore=")) restPrefix = "-restore=";
            if (restPrefix != null)
            {
                try
                {
                    string[] parts = a.Substring(restPrefix.Length).Split(',');
                    int[] v = new int[4];
                    for (int i = 0; i < 4; i++) v[i] = int.Parse(parts[i].Trim());
                    l.restoreArgs = v;
                }
                catch { Console.WriteLine("restore 参数格式错误：" + a); }
            }
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
            if (a0 == "/selftest" || a0 == "-selftest") return SelfTest(Tail(args));
            if (a0 == "/wer")
            {
                if (args.Length < 2 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe /wer \"<Report.wer 路径>\" [结论另存的 UTF-8 文件]");
                    return 2;
                }
                string line = "崩溃捕获：" + Crash.Parse(args[1]);
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

        // /cheat=名（不带 --go）：控制台通道，对运行中的游戏直接发一份秘籍就退出。
        // 退出码 0=发出去了 / 3=游戏没在跑。给收尾自检与远程排障用，不开界面。
        // 带 --go 时不走这里：秘籍延后到游戏窗口就绪，由 StartGame 线程自动发。
        if (l.pendingCheat != null && !l.autoStart)
        {
            Cheats.Cheat c = l.pendingCheat;
            Console.WriteLine("秘籍通道：「" + c.Label + "」=" + string.Join(" / ", c.Lines));
            if (Cheats.FindGameWindow() == IntPtr.Zero) { Console.WriteLine("没找到正在运行的游戏窗口"); return 3; }
            string err = Cheats.Send(c, delegate (string s) { Console.WriteLine(s); });
            Console.WriteLine(err == null ? "秘籍已发送" : ("发送失败：" + err));
            return err == null ? 0 : 1;
        }

        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        // 恢复进程模式（--restore=w,h,rate,pid）：独立于主启动器的一条命。
        // 场景：玩家用任务管理器把启动器整个强杀了（老年玩家真干得出来），桌面还停在 800×600——
        // 这条进程活着，等游戏 pid 退出后就还原桌面再自杀。启动器正常退出时它检查到桌面
        // 已还原也会安静退出，不留垃圾进程。
        int[] rp = l.restoreArgs;
        if (rp != null)
        {
            int w = rp[0], h = rp[1], rate = rp[2], pid = rp[3];
            Log("恢复进程启动：目标 " + w + "×" + h + "@" + rate + "，游戏 pid=" + pid);
            if (pid > 0)
            {
                DateTime deadline = DateTime.Now.AddHours(24);   // 24h 上限防 pid 复用傻等
                bool alive = true;
                while (alive && DateTime.Now < deadline)
                {
                    alive = false;
                    try { Process p = Process.GetProcessById(pid); alive = p != null && !p.HasExited; }
                    catch { alive = false; }
                    if (alive) Thread.Sleep(2000);
                }
                Thread.Sleep(3500);                              // 让主启动器的 finally 先还原
            }
            int[] cur = Env.CurrentMode();
            if (cur != null && cur[0] == w && cur[1] == h && cur[2] == rate)
            {
                Log("恢复进程：桌面已是目标模式，无需还原，退出");
                return 0;
            }
            Env.RestoreByMode(w, h, rate, Log);
            return 0;
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
        // 层已移除（2026-11-15 定案），状态恒「已就绪」；目录里若有旧版残留层文件，启动时统一清掉
        layerOk = true;
        status = layerOk ? "已就绪" : "画面文件不齐，异常时换第二档";
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
        // 中栏一叠：标题 → 一句说明 → 启动选项标签 → 用途说明 → 下拉框 → 启动 → 日志
        // → 秘籍标题 → 两行秘籍按钮（3+3），
        // 整叠垂直居中，说明和控件之间不许拉出空洞（leather-recipes.md §4、AGENTS 规则 20）
        int cheatH = S(20) + S(2) + S(28) + S(6) + S(28);   // 标题 + 行1 + 行2
        int stackH = headH + S(8) + noteH + S(18)
                   + headH + S(4) + hintH + S(6) + comboH
                   + S(16) + S(44) + S(10) + S(34)
                   + S(14) + cheatH;
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

        // 秘籍区：小标题 + 两行按钮，每行 3 个等宽（一键全开/无敌开/无敌关 | 全武器/补弹药/无限弹药）
        rcCheatHead = new Rectangle(inner, rcLog.Bottom + S(14), innerW, S(20));
        int cw = (innerW - S(6) * 2) / 3, ch = S(28);
        int row2 = rcCheatHead.Bottom + S(2);
        int row3 = row2 + ch + S(6);
        for (int i = 0; i < rcCheats.Length; i++)
        {
            int row = i / 3, col = i % 3;
            int ry = row == 0 ? row2 : row3;
            rcCheats[i] = new Rectangle(inner + col * (cw + S(6)), ry, cw, ch);
        }

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

        // 秘籍区：游戏中点击自动输入（游戏没跑也会给状态栏提示），标签下带一句玩法说明
        TextRenderer.DrawText(g, "游戏秘籍（游戏运行中点击，自动输入）", fHead,
            new Point(rcCheatHead.X, rcCheatHead.Y), Gold, Color.Transparent);
        for (int i = 0; i < Cheats.All.Length; i++)
        {
            int idx = CheatIndexBase + i;
            DrawButton(g, rcCheats[i], Cheats.All[i].Label, false, true,
                hover == idx || focus == idx, focus == idx);
        }
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
        for (int i = 0; i < rcCheats.Length; i++)
            if (rcCheats[i].Contains(pt)) return CheatIndexBase + i;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcLog);
            if (selMode != null) r = Rectangle.Union(r, selMode.Bounds);
            if (rcCheatHead != Rectangle.Empty)
            {
                r = Rectangle.Union(r, rcCheatHead);
                foreach (Rectangle c in rcCheats) r = Rectangle.Union(r, c);
            }
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
        // Tab 顺序：下拉框 → 启动 → 日志 → 六个秘籍按钮；busy 时启动/下拉不可进，秘籍反而最常用
        int[] order;
        if (busy) order = new int[] { 1, 4, 5, 6, 7, 8, 9 };
        else order = new int[] { 3, 0, 1, 4, 5, 6, 7, 8, 9 };
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
        if (i >= CheatIndexBase && i < CheatIndexBase + Cheats.All.Length) { SendCheat(i - CheatIndexBase); return; }
    }

    // ==========================================================================
    // 上海滩专有：一键秘籍（Cheats.cs 引擎，扫描码 SendInput，老 DirectInput 引擎只认扫描码）
    // 游戏没跑只提示不报错；发送在后台线程（节奏 60-80ms/键，不能占 UI 线程）。
    // ==========================================================================
    bool cheatBusy;                                     // 防连点：发送中再点直接忽略
    int gamePid;                                        // 游戏进程 pid（玩家关窗时收走游戏用）

    void SendCheat(int idx)
    {
        if (cheatBusy) { SetStatus("秘籍正在发送中…", InkDim); return; }
        Cheats.Cheat c = Cheats.All[idx];
        if (Cheats.FindGameWindow() == IntPtr.Zero)
        {
            SetStatus("游戏还没在运行：先点「启动游戏」，进游戏后再点秘籍", Warn);
            Log("点秘籍「" + c.Label + "」：游戏进程不在，未发送");
            return;
        }
        cheatBusy = true;
        SetStatus("正在发送秘籍「" + c.Label + "」…", InkDim);
        Log("点秘籍「" + c.Label + "」：" + string.Join(" / ", c.Lines));
        Thread t = new Thread(delegate ()
        {
            try
            {
                string err = Cheats.Send(c, Log);
                if (err != null) SetStatus("秘籍没发出去：" + err, Warn);
                else SetStatus("秘籍「" + c.Label + "」已发送，回游戏看效果", Ok);
            }
            catch (Exception ex) { SetStatus("秘籍发送出错：" + ex.Message, Warn); Log("秘籍异常：" + ex); }
            finally { cheatBusy = false; }
        });
        t.IsBackground = true;
        t.Start();
    }

    // ==========================================================================
    // ==========================================================================
    // 现场读数（规则 37）：读数与断言全部在 Env.cs（独立文件，与 Launcher.cs 一起编译）。
    // 老游戏直接拿 Win32 环境值当除数；读数必须是同一条路径，不许"正常就跳过"。
    // ==========================================================================
    static int DumpReadings()
    {
        try
        {
            Env.Readings r = Env.Read(Log, AppDir());
            Console.WriteLine("现场读数已写入运行日志：桌面 " + r.Width + "×" + r.Height +
                              " @" + r.Refresh + "Hz DPI=" + r.Dpi);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("读数失败：" + ex.Message); return 1; }
    }

    void LogEnvironment()
    {
        try { Env.Read(Log, AppDir()); }
        catch (Exception ex) { Log("采现场读数失败：" + ex); }
    }

    // ==========================================================================
    // 上海滩专有：灾备机制（2026-11-15 定案，目标玩家是不懂电脑的老年人）
    // ① 启动时清掉旧版残留的兼容层文件（幂等自愈，只动我们自己部署过的那三个文件名）；
    // ② 切桌面前拉起独立恢复进程（--restore）：就算启动器被任务管理器强杀，游戏一退出
    //    桌面也会被救回来——玩家永远不需要自己碰显示设置；
    // ③ 看门狗加「未响应」检测：窗口还在但画面卡死（用户实遇）连续 60s → 自动收走游戏进程，
    //    桌面随 finally 还原。玩家全程不用碰任务管理器。
    // ==========================================================================
    static readonly string[] StaleLayerFiles = { "D3D8.dll", "D3D9.dll", "dgVoodoo.conf" };

    void CleanupStaleLayers()
    {
        foreach (string f in StaleLayerFiles)
        {
            string p = Path.Combine(AppDir(), f);
            if (!File.Exists(p)) continue;
            try { File.Delete(p); Log("已清理旧版兼容层残留：" + f); }
            catch (Exception ex) { Log("清理 " + f + " 失败（不拦启动）：" + ex.Message); }
        }
    }

    // 切桌面前拉起独立恢复进程：等游戏 pid 退出后按 savedW×savedH@savedRate 还原（见 Main 的 --restore）
    int savedW, savedH, savedRate = 60;     // 原桌面模式（恢复进程还原参数）
    bool restorerArmed;                     // 默认档切了桌面才武装（备用档桌面没动，不需要恢复进程）
    int restorerPid;                        // 当前恢复进程 pid（游戏快速崩溃重拉前先收走它）
    void SpawnRestorer(int gamePid)
    {
        try
        {
            // 游戏快速崩溃重拉场景：先收走上一条恢复进程（它还钉着旧 pid，不收会抢着还原桌面）
            if (restorerPid > 0)
            {
                try { Process.GetProcessById(restorerPid).Kill(); } catch { }
                Log("已收走上一条恢复进程 pid=" + restorerPid);
                restorerPid = 0;
            }
            string args = "--restore=" + savedW + "," + savedH + "," + savedRate + "," + gamePid;
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, args);
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = false;
            Process newRp = Process.Start(psi);
            restorerPid = newRp != null ? newRp.Id : 0;
            Log("已拉起独立恢复进程（守护桌面还原）：还原目标 " + savedW + "×" + savedH + "@" + savedRate + "，游戏 pid=" + gamePid);
        }
        catch (Exception ex) { Log("拉起恢复进程失败（不影响本次启动，还原仍由本进程 finally 负责）：" + ex.Message); }
    }

    void StartGame()
    {
        if (busy) return;
        layerOk = true;
        CleanupStaleLayers();
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe;
            statusColor = Warn;
            layerOk = false;
            Invalidate();
            return;
        }
        Log("点「启动游戏」：" + ModeNames[mode] + (mode == 0
            ? "（画面铺满：临时把桌面调到 800×600，游戏退出后自动还原）"
            : "（桌面分辨率不动，游戏以窗口模式运行）"));
        SetBusy(true, "游戏运行中…");
        Thread t = new Thread(delegate ()
        {
            // 桌面还原是硬义务：声明在 try 外，正常退出/崩溃/看门狗杀进程都必须走 finally
            Env.DEVMODEW savedMode = new Env.DEVMODEW();
            bool modeChanged = false;
            // 默认档会把桌面缩到 800×600，900 宽的启动器窗口会超出屏幕——正常最小化到任务栏
            //（2026-11-15 用户定案：不藏托盘、不搞花样，就是普通的窗口最小化，任务栏点一下就回来；
            // 游戏退出、桌面还原后窗口自动弹回）
            BeginInvoke(new Action(delegate
            {
                Log("窗口最小化到任务栏：桌面即将切到游戏分辨率，点任务栏图标随时回到启动器");
                WindowState = FormWindowState.Minimized;
            }));
            try
            {
                string launchArgs = "-windows";           // 引擎自己切不动显示模式，窗口模式必带
                if (gameArgs.Length > 0) launchArgs += " " + gameArgs;
                if (mode == 0)
                {
                    // 默认档：铺满。目标 800×600@60（枚举不到就退最近的 4:3，Env.PickMode 内置兜底）
                    LogEnvironment();
                    int w, h;
                    Env.PickMode(800, 600, Log, out w, out h);
                    savedMode = Env.Apply(w, h, Log);
                    modeChanged = true;
                    // 恢复进程的还原参数 = 原桌面模式（savedMode 是 Apply 返回的 before），
                    // 千万不能存目标模式 800×600——那会把玩家桌面钉死在游戏分辨率（演练抓过这个 bug）
                    savedW = (int)savedMode.dmPelsWidth;
                    savedH = (int)savedMode.dmPelsHeight;
                    savedRate = (int)savedMode.dmDisplayFrequency;
                    restorerArmed = true;
                }
                else
                {
                    // 备用档：桌面一动不动（引擎窗口上限 1024×768，吃多少算多少）
                    Env.Readings snap = Env.Read(Log, AppDir());
                }
                Log("启动参数：" + launchArgs);
                // --cheat=名 配 --go：游戏窗口就绪后自动发一份秘籍（一键开挂链路，也是实测取证通道）。
                // 独立后台线程轮询窗口，最多等 60s；主线程继续 RunGame 看门狗，互不阻塞。
                if (pendingCheat != null)
                {
                    Cheats.Cheat c = pendingCheat;
                    Thread ct = new Thread(delegate ()
                    {
                        try
                        {
                            for (int i = 0; i < 120; i++)
                            {
                                Thread.Sleep(500);
                                if (Cheats.FindGameWindow() != IntPtr.Zero) break;
                            }
                            if (Cheats.FindGameWindow() == IntPtr.Zero)
                            { Log("自动秘籍：60s 内没等到游戏窗口，放弃「" + c.Label + "」"); return; }
                            Thread.Sleep(1500);          // 再让引擎把输入栈准备好
                            Log("自动秘籍：游戏窗口就绪，发送「" + c.Label + "」");
                            string err = Cheats.Send(c, Log);
                            Log(err == null ? "自动秘籍：已发送「" + c.Label + "」"
                                            : "自动秘籍：发送失败——" + err);
                        }
                        catch (Exception ex) { Log("自动秘籍异常：" + ex.Message); }
                    });
                    ct.IsBackground = true;
                    ct.Start();
                }
                gameStarted = DateTime.Now;
                int code = RunGame(exePath, launchArgs);
                int secs = (int)(DateTime.Now - gameStarted).TotalSeconds;
                bool abnormal = (uint)code >= 0xC0000000;
                Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                    (abnormal ? "（系统级异常）" : ""));
                // 快速崩溃自愈（2026-11-15 实测：层移除后首启弹「设置最优设置？」→ 点掉后引擎在
                // 原生路径崩溃 0xC0000005，崩前指纹已写回，第二发稳定）。15s 内崩 = 按此处理，
                // 自动重拉一次（桌面还停在 800×600，无需再切）。最多一次，连崩两次就如实收场。
                if (abnormal && secs < 15 && (uint)code == 0xC0000005u)
                {
                    Log("自愈：启动即崩（多为环境指纹变更首启），自动重拉一次");
                    code = RunGame(exePath, launchArgs);
                    secs = (int)(DateTime.Now - gameStarted).TotalSeconds;
                    abnormal = (uint)code >= 0xC0000000;
                    Log("重拉后退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                        (abnormal ? "（系统级异常）" : ""));
                }
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
                        Show();
                        WindowState = FormWindowState.Normal;
                    }));
                }
                catch { }
                SetStatus("启动失败，请把运行日志发给我们", Warn);
            }
            finally
            {
                if (modeChanged) Env.Restore(savedMode, Log);   // 游戏退出即还原桌面，一条路、不分支
                SetBusy(false, null);
                // 桌面已还原，弹回窗口（进程都要退出了就不折腾）
                if (!finishing)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            Show();
                            WindowState = FormWindowState.Normal;
                            BringToFront();
                            Activate();
                        }));
                    }
                    catch { }
                }
            }
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
                gamePid = game.Id;
                if (restorerArmed) SpawnRestorer(gamePid);
                game.WaitForExit();
                Log("游戏退出码：" + game.ExitCode);
                return game.ExitCode;
            }
        }
        gamePid = pi.dwProcessId;
        Log("已启动游戏 " + Path.GetFileName(exePath) + "，pid=" + gamePid + "（本进程的 direct child）");
        if (restorerArmed) SpawnRestorer(gamePid);
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
        DateTime? hungSince = null;             // 窗口未响应起始时刻（null=响应正常）
        while (WaitForSingleObject(pi.hProcess, 1000) != 0)
        {
        // 启动询问框自动点掉（通用防御，CoD2/CoD5 已验证框架）：老游戏启动期会弹各种询问框
        // （设置/安全模式/未响应）。血战上海滩探针实测不弹，但机制留着——真弹了也能被自动点掉，
        // 「否」按钮不改任何设置，符合窗口零干预铁律。
        // 限启动后 90s 内、只认该 pid 的 #32770 对话框。不碰前台不碰显示状态。
        if ((DateTime.Now - t0).TotalSeconds < 90)
        {
            IntPtr dlg = FindAskDialog(pi.dwProcessId);
            if (dlg != IntPtr.Zero && ClickDialogNo(dlg))
                Log("启动询问框：已自动替玩家点「否」（不改任何设置）");
        }
            if (GameHasWindow(pi.dwProcessId)) lastVisible = DateTime.Now;
            // 「画面卡住、声音还在」型卡死（用户实遇 2026-11-15）：窗口在但引擎不处理消息。
            // IsHungAppWindow 连续 60s 为真 → 补 WM_CLOSE → 10s 不退强杀，桌面随 finally 还原。
            // 60s 阈值给读盘/切关留足余量，正常玩不会误杀。
            IntPtr mainWnd = GameMainWindow(pi.dwProcessId);
            bool hung = mainWnd != IntPtr.Zero && IsHungAppWindow(mainWnd);
            if (hung && hungSince == null)
            {
                hungSince = DateTime.Now;
                Log("看门狗：游戏窗口未响应（画面卡死型），开始 60s 观察");
            }
            else if (!hung) hungSince = null;
            if (hungSince != null && (DateTime.Now - hungSince.Value).TotalSeconds > 60)
            {
                Log("看门狗：窗口连续 60s 未响应，补送 WM_CLOSE");
                PostCloseToAny(pi.dwProcessId);
                if (WaitForSingleObject(pi.hProcess, 10000) != 0)
                {
                    Log("看门狗：仍不退，结束游戏进程保护桌面还原（玩家不需要碰任务管理器）");
                    TerminateProcess(pi.hProcess, 1);
                    WaitForSingleObject(pi.hProcess, 10000);
                }
                hungSince = null;
                break;
            }
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

    [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr h);

    // 看门狗用：该 pid 的可见主窗口（游戏只有一个，取第一个可见的），查未响应用
    static IntPtr GameMainWindow(int pid)
    {
        IntPtr[] hit = new IntPtr[] { IntPtr.Zero };
        uint want = (uint)pid;
        EnumWindows(delegate(IntPtr h, IntPtr lp)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if (p == want && IsWindowVisible(h)) { hit[0] = h; return false; }
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
            BeginInvoke(new Action(delegate { Close(); }));
        }
        catch { }
    }

    // 2026-11-15 用户定案：不要托盘、不要陪跑——启动就启动，关闭就关闭。
    // 游戏运行中玩家关窗 = 真关闭：收走游戏进程，桌面还原由恢复进程兜底（独立一条命），
    // 玩家永远不需要碰任务管理器，也不会有任何看不见的后台残留。
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy && !finishing && gamePid > 0)
        {
            Log("玩家关窗：同时结束游戏进程（关闭就关闭，不留后台）");
            try { Process.GetProcessById(gamePid).Kill(); } catch { }
        }
        base.OnFormClosing(e);
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
    // ==========================================================================
    // 崩溃取证（规则 35）：读系统自动生成的 WER 报告，抄成一行进同一份日志，本体存 崩溃转储\。
    // 已判死不许再试：WER LocalDumps 注册表（要动系统）、调试器拉起游戏（实测窗口出不来）。
    // 解析与抓取实现在 Crash.cs（独立文件，与 Launcher.cs 一起编译）。
    // ==========================================================================
    DateTime gameStarted;

    void CaptureWerReport(string exeName)
    {
        Crash.Capture(AppDir(), exeName, gameStarted, Log);
    }

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

}
