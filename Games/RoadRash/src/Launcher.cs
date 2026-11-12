using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// 暴力摩托 Road Rash 2002 启动器（game_starter.exe），界面规范见仓库
// .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、零图片、三栏布局，
// 右栏键帽表从当前所选 reg 的 Controls 现算。启动行为与 game_starter.bat 一致。
//
// 平台托管契约（Playnite 这类启动器靠它监控游戏，见技能 SKILL.md「平台托管契约」）：
//   1. 游戏是 game_starter.exe 的**直接子进程**（UseShellExecute=false），进程树能认出来；
//   2. 游戏运行期间 game_starter.exe 一直活着，玩家随时 Alt+Tab 回来看操作说明；
//   3. 玩家中途关窗：窗口隐藏到后台（托盘双击唤回），进程继续陪跑，游戏不受影响；
//      游戏退出后 game_starter.exe 才结束，并把游戏的退出码回传给平台；
//      就算玩家把 game_starter.exe 整个杀掉，游戏仍带着 game_starter.exe 的父 pid，平台按树照样能抓到游戏退出码；
//   4. 游戏正常退出：game_starter.exe 把**游戏的退出码原样回传**给平台（崩溃码 0xC0000xxx 才会被认成异常）；
//   5. `game_starter.exe /go` 按当前选择直接启动，给平台做零交互入口。
//
// Build:
//   csc -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001 `
//       -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:game_starter.exe Launcher.cs

class Launcher : Form
{
    // ---------- 设计令牌 ----------
    // 红色全部走渐变：窗口底 #800020→#4A0012，卡片 #9B1B30→#6E0F22，页眉/页脚带 #2A0A0F→#4A0E1A
    static readonly Color BgTop = Color.FromArgb(0x80, 0x00, 0x20);
    static readonly Color BgBottom = Color.FromArgb(0x4A, 0x00, 0x12);
    static readonly Color CardTop = Color.FromArgb(0x9B, 0x1B, 0x30);
    static readonly Color CardBottom = Color.FromArgb(0x6E, 0x0F, 0x22);
    static readonly Color BandTop = Color.FromArgb(0x2A, 0x0A, 0x0F);
    static readonly Color BandBottom = Color.FromArgb(0x4A, 0x0E, 0x1A);
    static readonly Color PanelEdge = Color.FromArgb(0xE0, 0x57, 0x4F);
    static readonly Color Gold = Color.FromArgb(0xFF, 0xD8, 0x5E);
    static readonly Color GoldDim = Color.FromArgb(0xC9, 0xA2, 0x27);
    static readonly Color Ink = Color.FromArgb(0xFF, 0xF3, 0xE6);
    static readonly Color InkDim = Color.FromArgb(0xF0, 0xC3, 0xA8);
    static readonly Color OnGold = Color.FromArgb(0x5C, 0x08, 0x10);
    static readonly Color CapFace = Color.FromArgb(0x2E, 0x2E, 0x34);
    static readonly Color CapTopHi = Color.FromArgb(0x54, 0x54, 0x5E);
    static readonly Color CapSide = Color.FromArgb(0x17, 0x17, 0x1B);
    static readonly Color Ok = Color.FromArgb(0x7B, 0xD8, 0x8F);
    static readonly Color Warn = Color.FromArgb(0xFF, 0xB4, 0x54);

    // ---------- 选项数据（顺序与默认值即界面所见） ----------
    static readonly string[] ModeFiles = { "ddraw.ini", "ddraw-stretch.ini", "ddraw-soft.ini", "ddraw-640.ini" };
    static readonly string[] ModeNames =
    {
        "1  兼容模式 1080p 4:3 黑边",
        "2  1080p 拉伸铺满 16:9",
        "3  1080p（老机器）",
        "4  原版 640×480 全屏"
    };
    static readonly string[] ModeSubNames =
    {
        "兼容模式 1080p 4:3 黑边", "1080p 拉伸铺满 16:9", "1080p 老机器", "原版 640×480 全屏"
    };
    static readonly string[] KeyNames =
    {
        "1  修改版按键（默认）",
        "2  拳皇式 WASD",
        "3  系统默认按键"
    };
    static readonly string[] KeySubNames =
    {
        "修改版按键（默认）", "拳皇式 WASD", "系统默认按键"
    };
    static readonly string[] KeyReg64 = { "road64.reg", "road64-wasd.reg", "road64-system.reg" };
    static readonly string[] KeyReg32 = { "road.reg", "road-wasd.reg", "road-system.reg" };
    const string SaveNoneName = "1  不准备存档（自己进游戏载入）";
    // 游戏的命令行按空格切参数，存档名里不能有空白
    static readonly string[] LevelNames =
    {
        "不自动载入（进游戏菜单 3 自己选）",
        "1 级", "2 级", "3 级", "4 级", "5 级"
    };

    // ---------- launcher.ini 可覆盖 ----------
    string gameTitle = "暴力摩托 Road Rash 2002";
    string gameExe = "Roadrash.exe";
    string gameArgs = "";
    bool autoLoadSave = true;             // launcher.ini AutoLoadSave=0 可关掉命令行载档（自检用假游戏时要关）
    string gameProcess = "Roadrash";
    string layerDll = "ddraw.dll";
    string layerIni = "ddraw.ini";
    string layerBackupDir = "_cnc-ddraw";
    // 启动门禁：只在本进程树里给游戏注入暗号，代理 DLL 校验通过才放行；双击游戏没有暗号就起不来。
    // 值要和 gate\gate.c 里的 GATE_ENV / GATE_VAL 完全一致；GateEnv= 留空表示不做门禁（回滚/自检用）。
    string gateEnv = "YUN_GATE_RR";
    string gateVal = "RR-GATE-OK";
    string intro =
        "《暴力摩托》出自 EA 的 Road Rash 系列，1999 年前后发行，玩家扮飞车党在公路上竞速、互殴、躲警察。\n" +
        "\n" +
        "这份是 PC 中文版，经玩家修改汉化：中文菜单、HD 贴图，附带 8 个选手的 1-5 级进度和秘技说明。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给管理员";
    string entryGuide =
        "按任意键开始，Pause 暂停；Esc 退出\n" +
        "菜单：1 单机 / 2 多人 / 3 读档\n" +
        "选人、买车在「游戏设置」里改";
    string savesDir = "各选手1-5级存档";
    string savePrefix = "存档-";
    string cheats =
        "# 提示 · 秘技（大小写都行）\n" +
        "比赛中先盲打 XYZZY，玩家名变黄后\n" +
        "再打下面任意一条（输入不上屏，\n" +
        "撞车跑回车的空档最好输）：\n" +
        "K'THUNK! 木棒    THWACK! 铁鞭\n" +
        "SPOON! 加速      PLUGH 关闭秘技\n" +
        "BRIBE 贿赂警察   YES,OCCIFER 杀警察";

    // ---------- 运行状态 ----------
    const int NoFocus = -99;            // 焦点在下拉框里（由 ActiveControl 管）或没有焦点
    int mode, keymap, savePick, saveLevel = 5, hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    string autoSaveFile;                  // 本次启动要交给游戏命令行的存档名（不带空格）
    string[] saveFolders = new string[0];      // 各选手存档目录
    string[] saveLabels = new string[0];       // 下拉框显示文字
    string[] saveShort = new string[0];        // 用于文件名的短名
    bool busy, layerOk;
    string status = "已就绪";
    Color statusColor = Ok;
    LegendItem[][] legend = new LegendItem[0][];
    LegendItem[][] cheatKeys = new LegendItem[0][];
    volatile int exitCode;              // 回传给启动平台的游戏退出码
    volatile bool finishing;            // 游戏已退出，正在真关窗
    NotifyIcon tray;                    // 游戏运行期间关窗 = 隐藏陪跑
    bool autoStart;                     // game_starter.exe /go：零交互直接开

    // ---------- 门店广告（黄金版开、钻石版关）----------
    // 开关走命令行参数：/ad 开、/noad 关（默认关）。网址放 launcher.ini 的 AdUrl，改网址不用重编。
    // /ad=<网址> 可临时覆盖 ini 里的网址，方便门店快捷方式直接写死一个落地页。
    bool adEnabled;                     // 本次点「启动游戏」是否先弹广告网页
    string adUrlOverride;               // /ad=<网址> 带来的覆盖网址，null 表示用 ini
    string adUrl = "";                  // launcher.ini 的 AdUrl
    int adDelaySec = 0;                 // 广告打开后停留秒数再启动游戏（默认 0，ini 可配）

    // ---------- 字体（按 DPI 建一次） ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;

    // ---------- 布局 ----------
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcComboMode, rcComboKey, rcComboSave, rcComboLevel,
              rcLabelMode, rcLabelKey, rcLabelSave, rcLabelLevel;
    Rectangle rcStart, rcFolder, rcExit;
    ComboBox cbMode, cbKey, cbSave, cbLevel;

    class LegendItem { public string Caption; public string[] Caps; }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "/selftest" || args[0] == "-selftest"))
            return SelfTest(Tail(args));
        if (args.Length > 0 && (args[0] == "/keymap" || args[0] == "-keymap"))
            return KeymapCli(Tail(args));       // 命令行落地按键方案，不开窗口（切键-*.bat 用）
        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();   // bat 包装入口：不留黑框
        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        foreach (string a in args)
        {
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;
            // 门店广告开关：/ad 开（用 ini 的 AdUrl），/ad=<网址> 开并覆盖网址，/noad 关。默认关。
            if (a == "/ad" || a == "-ad" || a == "--ad") { l.adEnabled = true; }
            else if (a.StartsWith("/ad=", StringComparison.OrdinalIgnoreCase) ||
                     a.StartsWith("-ad=", StringComparison.OrdinalIgnoreCase) ||
                     a.StartsWith("--ad=", StringComparison.OrdinalIgnoreCase))
            {
                int eq = a.IndexOf('=');
                if (eq >= 0 && a.Length > eq + 1) { l.adEnabled = true; l.adUrlOverride = a.Substring(eq + 1); }
            }
            else if (a == "/noad" || a == "-noad" || a == "--noad") { l.adEnabled = false; }
        }
        string parentName = ParentProcessName();
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；父进程：" + (parentName.Length == 0 ? "未知" : parentName) +
            "；零交互：" + (l.autoStart ? "是" : "否") +
            "；广告：" + (l.adEnabled ? "开" : "关"));
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        Application.Run(l);
        return l.exitCode;
    }

    // ---------- 按键方案落地（原来单独一个 keymap.exe，合并进来：交付只带一个 exe） ----------
    const string SubKey64 = @"SOFTWARE\Wow6432Node\Electronic Arts\RoadRash 95";
    const string SubKey32 = @"SOFTWARE\Electronic Arts\RoadRash 95";

    static string SubKey
    {
        get { return Environment.Is64BitOperatingSystem ? SubKey64 : SubKey32; }
    }

    // game_starter.exe /keymap 1|2|3   退出码：0 已生效 1 参数/文件错 2 无权限 3 校验失败
    static int KeymapCli(string[] args)
    {
        if (args.Length < 1) return 1;
        int n;
        if (!int.TryParse(args[0], out n) || n < 1 || n > 3) return 1;
        bool elevated = args.Length > 1 && args[1] == "--elevated";

        string[] files = Environment.Is64BitOperatingSystem ? KeyReg64 : KeyReg32;
        string file = Path.Combine(AppDir(), files[n - 1]);
        byte[] want = ReadControls(file);
        if (want == null) return 1;

        int rc = ApplyControls(want, file);
        Log("按键方案 " + n + " → " + (rc == 0 ? "已生效" : "失败 rc=" + rc) + (elevated ? "（提权进程）" : "") + "｜文件 " + Path.GetFileName(file));
        if (rc == 0) WriteKeymapMark(n);
        if (rc == 2 && !elevated)        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(
                    Application.ExecutablePath, "/keymap " + n + " --elevated");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = AppDir();
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch { return 2; }
        }
        return rc;
    }

    static int ApplyControls(byte[] want, string regFile)
    {
        byte[] cur;
        bool has = HasControls(out cur);
        if (has && Same(cur, want)) return 0;
        try
        {
            if (!has)
            {
                if (!RunRegImport(regFile)) return 2;
            }
            else
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(SubKey, true))
                {
                    if (k == null) return 2;
                    k.SetValue("Controls", want, RegistryValueKind.Binary);
                }
            }
        }
        catch (SecurityException) { return 2; }
        catch (UnauthorizedAccessException) { return 2; }
        catch (IOException) { return 2; }
        byte[] after;
        return HasControls(out after) && Same(after, want) ? 0 : 3;
    }

    static bool HasControls(out byte[] current)
    {
        current = null;
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(SubKey))
            {
                if (k == null) return false;
                current = k.GetValue("Controls") as byte[];
                return current != null;
            }
        }
        catch { return false; }
    }

    static bool RunRegImport(string file)
    {
        if (!File.Exists(file)) return false;
        ProcessStartInfo psi = new ProcessStartInfo("reg.exe", "import \"" + file + "\"");
        psi.WorkingDirectory = AppDir();
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        try
        {
            using (Process p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return false; }
                return p.ExitCode == 0;
            }
        }
        catch { return false; }
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static void WriteKeymapMark(int n)
    {
        try { File.WriteAllText(Path.Combine(AppDir(), "keymap.txt"), n.ToString() + "\r\n", Encoding.ASCII); }
        catch { }
    }

    // ---------- 藏掉父进程（cmd 包装）的控制台窗口 ----------
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESSENTRY32
    {
        public int dwSize;
        public int cntUsage;
        public int th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public int th32ModuleID;
        public int cntThreads;
        public int th32ParentProcessID;
        public int pcPriClassBase;
        public int dwFlags;
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
        IntPtr snap = CreateToolhelp32Snapshot(2, 0);      // TH32CS_SNAPPROCESS
        if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) return 0;
        PROCESSENTRY32 e = new PROCESSENTRY32();
        e.dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32));
        int parent = 0;
        if (Process32First(snap, ref e))
        {
            do { if (e.th32ProcessID == pid) { parent = e.th32ParentProcessID; break; } }
            while (Process32Next(snap, ref e));
        }
        CloseHandle(snap);
        return parent;
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
                if (pid == target && IsWindowVisible(h)) ShowWindow(h, 0);   // SW_HIDE
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    static string[] Tail(string[] a)
    {
        string[] r = new string[a.Length - 1];
        Array.Copy(a, 1, r, 0, r.Length);
        return r;
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
        BackColor = BgBottom;
        ClientSize = new Size(900, 620);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using (Graphics g = CreateGraphics()) { dpi = g.DpiX / 96f; }
        MakeFonts();
        ClientSize = new Size(S(900), S(620));
        keymap = ReadKeymapChoice();
        savePick = ReadSavePick();
        saveLevel = ReadSaveLevel();
        RebuildLegend();
        ScanSaves();
        if (cbMode == null)
        {
            cbMode = MakeCombo(ModeNames, mode, delegate { mode = cbMode.SelectedIndex; });
            cbKey = MakeCombo(KeyNames, keymap, delegate
            {
                keymap = cbKey.SelectedIndex;
                RebuildLegend();
                Invalidate(colRight);
            });
            cbSave = MakeCombo(saveLabels, savePick, delegate
            {
                savePick = cbSave.SelectedIndex;
                WriteSmall("savepick.txt", savePick.ToString());
            });
            cbLevel = MakeCombo(LevelNames, saveLevel, delegate
            {
                saveLevel = cbLevel.SelectedIndex;
                WriteSmall("savelevel.txt", saveLevel.ToString());
            });
            cbMode.TabIndex = 0;
            cbKey.TabIndex = 1;
            cbSave.TabIndex = 2;
            cbLevel.TabIndex = 3;
        }
        LayoutRects();
        string msg;
        layerOk = EnsureLayer(out msg);
        status = msg;
        statusColor = layerOk ? Ok : Warn;
        LoadGameIcon();
        Log("启动 " + gameTitle + "｜画面=" + ModeSubNames[mode] + " 按键=" + KeySubNames[keymap] +
            " 存档项=" + savePick + " 载入级别=" + saveLevel + "｜" + status + (autoStart ? "｜/go 零交互" : ""));
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
        int c1 = S(252), c2 = S(296), c3 = S(280);
        int top = S(88), bottom = S(552);
        colLeft = new Rectangle(pad, top, c1, bottom - top);
        colCenter = new Rectangle(pad + c1 + gut, top, c2, bottom - top);
        colRight = new Rectangle(pad + c1 + gut + c2 + gut, top, c3, bottom - top);

        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        // 一张卡装下四组「标签 + 下拉框」和按钮，整叠在卡里垂直居中：
        // 卡变高只是上下留白对称，不会中间空出一大块
        int rowH = S(22), capH = S(30), gap = S(18), gapBtn = S(26);
        int stack = 4 * (rowH + capH) + 3 * gap + gapBtn + S(44) + S(10) + S(34);
        int y = colCenter.Y + (colCenter.Height - stack) / 2;
        rcLabelMode = new Rectangle(inner, y, innerW, rowH);
        rcComboMode = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gap;
        rcLabelKey = new Rectangle(inner, y, innerW, rowH);
        rcComboKey = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gap;
        rcLabelSave = new Rectangle(inner, y, innerW, rowH);
        rcComboSave = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gap;
        rcLabelLevel = new Rectangle(inner, y, innerW, rowH);
        rcComboLevel = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gapBtn;

        rcStart = new Rectangle(inner, y, innerW, S(44));
        rcFolder = new Rectangle(inner, y + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y, innerW - (rcFolder.Right + S(8) - inner), S(34));

        PlaceCombo(cbMode, rcComboMode);
        PlaceCombo(cbKey, rcComboKey);
        PlaceCombo(cbSave, rcComboSave);
        PlaceCombo(cbLevel, rcComboLevel);
    }

    static void PlaceCombo(ComboBox cb, Rectangle slot)
    {
        if (cb == null) return;
        cb.SetBounds(slot.X, slot.Y + (slot.Height - cb.Height) / 2, slot.Width, 0);
    }

    // 下拉框就用标准控件（保留模式，画交给框架），不 OwnerDraw、不子类化、不改 FlatStyle：
    // 之前的闪烁就是自绘条目招来的，标准控件自己不会闪。
    ComboBox MakeCombo(string[] items, int selected, EventHandler changed)
    {
        ComboBox cb = new ComboBox();
        cb.DropDownStyle = ComboBoxStyle.DropDownList;
        cb.BackColor = Color.FromArgb(0x4A, 0x00, 0x12);
        cb.ForeColor = Ink;
        cb.Font = fRowSel;                        // 加粗：框里那行就是当前选择
        cb.Items.AddRange(items);
        cb.SelectedIndex = selected;
        cb.SelectedIndexChanged += changed;
        Controls.Add(cb);
        return cb;
    }

    // ---------- 绘制 ----------
    protected override void OnPaint(PaintEventArgs e)    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        using (LinearGradientBrush bg = new LinearGradientBrush(all, BgTop, BgBottom, LinearGradientMode.Vertical))
            g.FillRectangle(bg, all);
        Rectangle head = new Rectangle(0, 0, ClientSize.Width, S(76));
        using (LinearGradientBrush hb = new LinearGradientBrush(head, BandTop, BandBottom, LinearGradientMode.Horizontal))
            g.FillRectangle(hb, head);
        int footY = S(560);
        Rectangle foot = new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY);
        using (LinearGradientBrush fb = new LinearGradientBrush(foot, BandTop, BandBottom, LinearGradientMode.Horizontal))
            g.FillRectangle(fb, foot);

        DrawHeader(g);
        DrawLeft(g);
        DrawCenter(g);
        DrawRight(g);
        DrawFooter(g);
    }

    void DrawHeader(Graphics g)
    {
        TextRenderer.DrawText(g, gameTitle, fTitle, new Point(colLeft.X, S(16)), Gold, Color.Transparent);
        TextRenderer.DrawText(g, "EA · Road Rash · 玩家汉化修改版",
            fMono, new Point(colLeft.X, S(48)), InkDim, Color.Transparent);

        using (Pen p = new Pen(GoldDim, 1f))
            g.DrawLine(p, colLeft.X, S(76), ClientSize.Width - S(20), S(76));
    }

    void DrawLeft(Graphics g)
    {
        Card(g, colLeft);
        int x = colLeft.X + S(16), y = colLeft.Y + S(14);
        TextRenderer.DrawText(g, "关于这款游戏", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(28);
        y += S(10);
        DrawWrapped(g, intro, x, y, colLeft.Width - S(32), fBody, Ink);
    }

    void DrawCenter(Graphics g)
    {
        Card(g, colCenter);
        TextRenderer.DrawText(g, "画面模式", fHead, new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);
        TextRenderer.DrawText(g, "按键方案", fHead, new Point(rcLabelKey.X, rcLabelKey.Y), Gold, Color.Transparent);
        TextRenderer.DrawText(g, "存档（选手 1-5 级）", fHead, new Point(rcLabelSave.X, rcLabelSave.Y), Gold, Color.Transparent);
        TextRenderer.DrawText(g, "自动载入的级别", fHead, new Point(rcLabelLevel.X, rcLabelLevel.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy && layerOk,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true, hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
    }

    // ---------- 存档：把所选选手的 1-5 级存档摆到游戏根目录 ----------
    void ScanSaves()
    {
        List<string> folders = new List<string>();
        List<string> shorts = new List<string>();
        List<string> labels = new List<string>();
        labels.Add(SaveNoneName);
        string root = Path.Combine(AppDir(), savesDir);
        if (Directory.Exists(root))
        {
            string[] dirs = Directory.GetDirectories(root);
            Array.Sort(dirs, StringComparer.Ordinal);
            foreach (string d in dirs)
            {
                string[] rr = Directory.GetFiles(d, "*.rrs");
                if (rr.Length == 0) continue;
                string name = ShortRiderName(Path.GetFileName(d));
                folders.Add(d);
                shorts.Add(name);
                labels.Add((folders.Count + 1).ToString() + "  " + name + "（" + rr.Length + " 个级别）");
            }
        }
        saveFolders = folders.ToArray();
        saveShort = shorts.ToArray();
        saveLabels = labels.ToArray();
        if (savePick >= saveLabels.Length) savePick = 0;
    }

    // 存档文件名会直接进游戏命令行，所以不能带空格（实测 "Milwaukee Jon(荘臣)" 这种带空格的
    // 名字游戏会当两个参数丢掉，退回新游戏）。取括号里的中文名，没有括号就抹掉空格。
    static string ShortRiderName(string folderName)
    {
        string s = folderName;
        int cut = s.IndexOf("1-5级存档", StringComparison.Ordinal);
        if (cut > 0) s = s.Substring(0, cut);
        s = s.Replace(".rar", "").Trim();
        int a = s.IndexOf('('), b = s.LastIndexOf(')');
        if (a > 0 && b > a + 1) s = s.Substring(a + 1, b - a - 1);
        return s.Replace(" ", "");
    }

    static string LevelOf(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path), d = "";
        foreach (char c in name) if (c >= '0' && c <= '9') d += c;
        return d.Length == 0 ? name : d + "级";
    }

    // 返回给状态栏的提示。选中选手的存档文件**不无脑覆盖**：玩家在里面存过进度就留着，
    // 只有缺失或与母本完全一致时才重新摆一份（自动载入会把文件名交给游戏）。
    string StageSaves()
    {
        autoSaveFile = null;
        try
        {
            string keep = (savePick > 0 && savePick - 1 < saveShort.Length)
                ? savePrefix + saveShort[savePick - 1] + "-" : null;
            foreach (string f in Directory.GetFiles(AppDir(), savePrefix + "*.rrs"))
                if (keep == null || !Path.GetFileName(f).StartsWith(keep, StringComparison.Ordinal))
                    try { File.Delete(f); } catch { }
            if (savePick <= 0 || savePick - 1 >= saveFolders.Length) return null;

            string[] files = Directory.GetFiles(saveFolders[savePick - 1], "*.rrs");
            Array.Sort(files, StringComparer.Ordinal);
            int n = 0, kept = 0;
            foreach (string src in files)
            {
                string dst = Path.Combine(AppDir(), savePrefix + saveShort[savePick - 1] + "-" + LevelOf(src) + ".rrs");
                if (File.Exists(dst) && !SameFileBytes(dst, src)) kept++;      // 玩家存过，别冲掉
                else File.Copy(src, dst, true);
                n++;
            }
            if (saveLevel >= 1)
            {
                string want = savePrefix + saveShort[savePick - 1] + "-" + saveLevel + "级.rrs";
                if (File.Exists(Path.Combine(AppDir(), want))) autoSaveFile = want;
            }
            string how = autoSaveFile != null ? "启动时自动载入 " + autoSaveFile
                                              : "进游戏按菜单 3，选 " + savePrefix + saveShort[savePick - 1] + "-…";
            return "已备好 " + n + " 个存档" + (kept > 0 ? "（保留 " + kept + " 个已有进度）" : "") + "，" + how;
        }
        catch (Exception ex) { return "存档准备失败：" + ex.Message; }
    }

    static bool SameFileBytes(string a, string b)
    {
        try
        {
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
        catch { return false; }
    }

    void DrawRight(Graphics g)
    {
        Card(g, colRight);
        int top = colRight.Y + S(6);
        int bottom = colRight.Bottom - S(6);
        int startY = colRight.Y + S(14) - rightScroll;
        int endY = DrawRightContent(g, startY, top, bottom);
        int contentH = endY - startY;
        int viewport = colRight.Height - S(14) - S(12);
        rightScrollMax = Math.Max(0, contentH - viewport);
        int clamped = Math.Max(0, Math.Min(rightScroll, rightScrollMax));
        if (clamped != rightScroll) { rightScroll = clamped; Invalidate(colRight); }
        DrawRightScroll(g, viewport, contentH);
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

    int DrawRightContent(Graphics g, int y, int top, int bottom)
    {
        int x = colRight.X + S(16), w = colRight.Width - S(32);
        int h = TextRenderer.MeasureText(g, "国", fHead).Height;
        if (InViewport(y, h, top, bottom))
            TextRenderer.DrawText(g, "进游戏说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(28);
        y = DrawWrappedVisible(g, entryGuide, x, y, w, fBody, Ink, top, bottom);
        y += S(16);
        if (InViewport(y, h, top, bottom))
            TextRenderer.DrawText(g, "操作说明 · " + KeySubNames[keymap], fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(30);
        if (legend.Length == 0)
        {
            int mh = TextRenderer.MeasureText(g, "国", fBody).Height;
            if (InViewport(y, mh, top, bottom))
                TextRenderer.DrawText(g, "读不到按键表（缺 reg 文件）", fBody, new Point(x, y), InkDim, Color.Transparent);
            y += S(24);
        }
        else
        {
            y = DrawLegendRows(g, legend, x, y, top, bottom);
        }
        y += S(10);
        if (InViewport(y, 2, top, bottom))
        {
            using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, x, y, x + w, y);
        }
        y += S(12);
        y = DrawWrappedVisible(g, cheats, x, y, w, fBody, Ink, top, bottom);
        y += S(8);
        return DrawLegendRows(g, cheatKeys, x, y, top, bottom);
    }

    int DrawWrappedVisible(Graphics g, string text, int x, int y, int width, Font font, Color color, int top, int bottom)
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
                while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > width) take--;
                if (InViewport(y, lh, top, bottom))
                    TextRenderer.DrawText(g, line.Substring(0, take), f, new Point(x, y), c, Color.Transparent);
                y += lh;
                line = line.Substring(take);
            }
        }
        return y;
    }

    int DrawLegendRows(Graphics g, LegendItem[][] rows, int x, int y, int top, int bottom)
    {
        int capH = S(24);
        foreach (LegendItem[] row in rows)
        {
            if (InViewport(y, capH, top, bottom))
            {
                int cx = x;
                int rightEdge = colRight.Right - S(16);
                foreach (LegendItem item in row)
                {
                    Size cs = TextRenderer.MeasureText(g, item.Caption, fBody);
                    int itemW = cs.Width + S(6);
                    foreach (string cap in item.Caps) itemW += CapWidth(g, cap) + S(4);
                    if (cx + itemW > rightEdge && cx > x) break;   // 行内不换行；放不下就停止该行，绝不叠字
                    int ty = y + (capH - cs.Height) / 2;
                    TextRenderer.DrawText(g, item.Caption, fBody, new Point(cx, ty), Ink, Color.Transparent);
                    cx += cs.Width + S(6);
                    foreach (string cap in item.Caps)
                    {
                        int cw = CapWidth(g, cap);
                        Keycap(g, cap, cx, y, cw, capH);
                        cx += cw + S(4);
                    }
                    cx += S(8);
                }
            }
            y += capH + S(12);
        }
        return y;
    }

    void DrawRightScroll(Graphics g, int viewport, int contentH)
    {
        if (rightScrollMax <= 0) return;
        int trackX = colRight.Right - S(7);
        int trackY = colRight.Y + S(10);
        int trackH = colRight.Height - S(20);
        int thumbH = Math.Max(S(24), (int)((long)trackH * viewport / Math.Max(1, contentH)));
        int thumbY = trackY + (int)((long)(trackH - thumbH) * rightScroll / Math.Max(1, rightScrollMax));
        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, Gold)))
            g.FillRectangle(b, trackX, trackY, S(3), trackH);
        using (GraphicsPath p = Round(new Rectangle(trackX - S(1), thumbY, S(5), thumbH), S(2)))
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
        int y = S(578);
        using (Pen p = new Pen(GoldDim, 1f))
            g.DrawLine(p, colLeft.X, S(566), ClientSize.Width - S(20), S(566));
        StatusDot(g, status, statusColor, colLeft.X, y);
        string hint = busy ? "关窗只是隐藏，游戏继续跑" : "游戏退出后本窗口自动关闭";
        Size sz = TextRenderer.MeasureText(g, hint, fMono);
        TextRenderer.DrawText(g, hint, fMono,
            new Point(ClientSize.Width - S(20) - sz.Width, y), InkDim, Color.Transparent);
        Size brand = TextRenderer.MeasureText(g, BrandGuard.Text, fBrand);
        TextRenderer.DrawText(g, BrandGuard.Text, fBrand,
            new Point((ClientSize.Width - brand.Width) / 2, y), Gold, Color.Transparent);
    }

    void Card(Graphics g, Rectangle r)
    {
        using (GraphicsPath p = Round(r, S(10)))
        using (LinearGradientBrush b = new LinearGradientBrush(
                   new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)),
                   CardTop, CardBottom, LinearGradientMode.Vertical))
            g.FillPath(b, p);
        using (GraphicsPath p = Round(Rectangle.Inflate(r, -1, -1), S(10)))
        using (Pen pen = new Pen(PanelEdge, 1f)) g.DrawPath(pen, p);
        using (Pen hi = new Pen(Color.FromArgb(46, Gold), 1f))
            g.DrawLine(hi, r.X + S(14), r.Y + 1, r.Right - S(14), r.Y + 1);
    }

    void DrawButton(Graphics g, Rectangle r, string text, bool primary, bool enabled, bool hot, bool focused)
    {
        Color back = !enabled ? Color.FromArgb(0x6E, 0x6E, 0x6E)
                   : primary ? (hot ? Color.FromArgb(0xFF, 0xE6, 0x8C) : Gold)
                   : (hot ? Color.FromArgb(70, Gold) : Color.Transparent);
        using (GraphicsPath p = Round(r, S(6)))
        {
            if (back != Color.Transparent)
                using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, p);
            if (!primary || !enabled)
                using (Pen pen = new Pen(GoldDim, 1f)) g.DrawPath(pen, p);
        }
        Color fore = !enabled ? Color.FromArgb(0xC8, 0xC8, 0xC8) : primary ? OnGold : Ink;
        TextRenderer.DrawText(g, text, primary ? fRowSel : fRow, r, fore, Color.Transparent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (focused) FocusRing(g, r);
    }

    void Keycap(Graphics g, string label, int x, int y, int w, int h)
    {
        Rectangle r = new Rectangle(x, y, w, h);
        using (GraphicsPath p = Round(r, S(4)))
        using (SolidBrush b = new SolidBrush(CapFace)) g.FillPath(b, p);
        using (GraphicsPath hi = Round(new Rectangle(r.X + S(2), r.Y + S(1), r.Width - S(4), S(3)), S(2)))
        using (SolidBrush tb = new SolidBrush(CapTopHi)) g.FillPath(tb, hi);
        using (SolidBrush sb = new SolidBrush(CapSide))
            g.FillRectangle(sb, r.X + S(3), r.Bottom - S(3), r.Width - S(6), S(2));
        Font f = CapFont(label);
        TextRenderer.DrawText(g, label, f,
            new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), Color.White, Color.Transparent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    Font CapFont(string label)
    {
        foreach (char c in label) if (c >= 0x3000) return fCapZh;
        return fCap;
    }

    int CapWidth(Graphics g, string label)
    {
        Size sz = TextRenderer.MeasureText(g, label, CapFont(label));
        return Math.Max(S(22), sz.Width + S(12));
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
        using (Pen pen = new Pen(Gold, 2f)) g.DrawPath(pen, p);
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

    // 按 \n 断行绘制；行内过长时按字符折行（中文没有空格可断）；"# " 开头是栏内小标题
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

    // ---------- 键帽表：从 reg 的 Controls 现算 ----------
    void RebuildLegend()
    {
        string[] files = Environment.Is64BitOperatingSystem ? KeyReg64 : KeyReg32;
        byte[] c = ReadControls(Path.Combine(AppDir(), files[keymap]));
        if (c == null || c.Length < 22) { legend = new LegendItem[0][]; return; }
        // 槽序：前进 刹车 转向左 倾斜左 加速 拳打 脚踢 重击 下车 转向右 倾斜右
        legend = new LegendItem[][]
        {
            new[] { Item("前进", c[1]), Item("刹车", c[3]) },
            new[] { Item("转向", c[5], c[19]) },
            new[] { Item("倾斜", c[7], c[21]) },
            new[] { Item("加速", c[9]), Item("下车", c[17]) },
            new[] { Item("拳打", c[11]), Item("脚踢", c[13]) },
            new[] { Item("重击", c[15]) }
        };
        cheatKeys = new LegendItem[][]
        {
            new[] { CapItem("看版本", "V"), CapItem("收放仪表盘", "F10") },
            new[] { CapItem("背景色（小键盘）", "+", "-") }
        };
    }

    static LegendItem CapItem(string caption, params string[] caps)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        it.Caps = caps;
        return it;
    }

    static LegendItem Item(string caption, params byte[] codes)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        List<string> caps = new List<string>();
        foreach (byte b in codes) caps.Add(CapLabel(b));
        it.Caps = caps.ToArray();
        return it;
    }

    static string CapLabel(byte code)
    {
        if (code >= 0x61 && code <= 0x7A) return ((char)(code - 0x20)).ToString();
        if (code >= 0x30 && code <= 0x39) return ((char)code).ToString();
        switch (code)
        {
            case 0x90: return "Shift";
            case 0x91: return "Ctrl";
            case 0x92: return "Alt";
            case 0x93: return "←";
            case 0x94: return "↑";
            case 0x95: return "→";
            case 0x96: return "↓";
            case 0x81: return "Page Up";
            case 0x82: return "End";
            case 0x83: return "Page Down";
            case 0x84: return "Home";
            case 0x85: return "Insert";
            case 0x86: return "Delete";
            case 0x0D: return "Enter";
            case 0x20: return "Space";
            case 0x1B: return "Esc";
            case 0x09: return "Tab";
            case 0x08: return "Bksp";
        }
        return "?" + code.ToString("X2");
    }

    static byte[] ReadControls(string path)
    {
        if (!File.Exists(path)) return null;
        const string marker = "\"Controls\"=hex:";
        string text = File.ReadAllText(path, Encoding.Default);
        int i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += marker.Length;
        List<byte> bytes = new List<byte>();
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == ',' || c == '\\') { i++; continue; }
            if (i + 1 < text.Length && IsHex(c) && IsHex(text[i + 1]))
            {
                bytes.Add(Convert.ToByte(text.Substring(i, 2), 16));
                i += 2;
                continue;
            }
            break;
        }
        return bytes.Count >= 22 ? bytes.ToArray() : null;
    }

    static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    // ---------- 输入 ----------
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcFolder.Contains(pt)) return 1;
        if (rcExit.Contains(pt)) return 2;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get { return Rectangle.Union(Rectangle.Union(rcStart, rcFolder), rcExit); }
    }

    // 只刷按钮，别整窗 Invalidate（会把三个下拉框一起重画 → 闪）
    void RepaintControls()
    {
        Invalidate(ButtonsRect);
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
        if (colRight.Contains(e.Location)) { ActiveControl = null; Focus(); }
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

    // 焦点顺序：画面 → 按键 → 存档 → 级别 → 启动 → 日志 → 退出（游戏运行中只剩后两个）
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
            case Keys.Escape: Close(); return true;
            case Keys.Enter:
                if (busy) { Close(); return true; }               // 关启动器（隐藏陪跑），游戏不动
                if (focus >= 0) { Activate(focus); return true; }
                break;                                           // 焦点在下拉框：Enter 交给它自己
            case Keys.Space:
                if (!busy && focus >= 0) { Activate(focus); return true; }
                break;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    int FocusTag()
    {
        if (ActiveControl == cbMode) return -3;
        if (ActiveControl == cbKey) return -2;
        if (ActiveControl == cbSave) return -1;
        if (ActiveControl == cbLevel) return -4;
        return focus;
    }

    void SetFocusTag(int t)
    {
        focus = NoFocus;
        ActiveControl = null;
        if (t == -3) ActiveControl = cbMode;
        else if (t == -2) ActiveControl = cbKey;
        else if (t == -1) ActiveControl = cbSave;
        else if (t == -4) ActiveControl = cbLevel;
        else if (t >= 0) focus = t;
        if (cbMode != null) { cbMode.Invalidate(); cbKey.Invalidate(); cbSave.Invalidate(); cbLevel.Invalidate(); }
        RepaintControls();
    }

    void CycleFocus(int delta)
    {
        int[] order = busy ? new int[] { 1, 2 } : new int[] { -3, -2, -1, -4, 0, 1, 2 };
        int idx = Array.IndexOf(order, FocusTag());
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        SetFocusTag(order[((idx + delta) % order.Length + order.Length) % order.Length]);
    }

    void Activate(int i)
    {
        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenLog(); return; }
        if (i == 2) { Close(); }
    }

    // ---------- 启动逻辑（与 game_starter.bat 一致） ----------
    void StartGame()
    {
        if (busy) return;
        string message;
        if (!EnsureLayer(out message))
        {
            status = message;
            statusColor = Warn;
            layerOk = false;
            Invalidate();
            return;
        }
        layerOk = true;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe;
            statusColor = Warn;
            Invalidate();
            return;
        }
        if (!ApplyMode(mode, out message))
        {
            status = message;
            statusColor = Warn;
            Invalidate();
            return;
        }

        int km = keymap;
        int sv = savePick;
        Log("点「启动游戏」：画面=" + ModeSubNames[mode] + " 按键=" + KeySubNames[km] + " 存档项=" + sv + " 载入级别=" + saveLevel);
        SetBusy(true, "启动中：画面模式 " + (mode + 1) + " · 按键方案 " + (km + 1));
        Thread t = new Thread(delegate ()
        {
            string warning = null;
            try
            {
                string saveMsg = null;
                try { saveMsg = StageSaves(); } catch { }
                ApplyKeymap(km, out warning);
                string note = warning != null ? warning : saveMsg;
                SetStatus(note != null ? note : "游戏运行中…", warning != null ? Warn : Ok);
                // 游戏自带命令行载入：Roadrash.exe <存档文件>（名字里不能有空格）
                string args = (autoSaveFile == null || !autoLoadSave) ? gameArgs
                    : (gameArgs.Length == 0 ? autoSaveFile : gameArgs + " " + autoSaveFile);
                Log("交给游戏的启动参数：" + (args.Length == 0 ? "（无）" : args));
                Log("启动门禁：" + (gateEnv.Length > 0 ? "已注入暗号 " + gateEnv : "未启用（GateEnv 为空）"));
                if (adEnabled) ShowAd();   // 黄金版：先弹广告网页、停留几秒，再启动游戏
                int code = RunAndWait(AppDir(), exePath, args, gateEnv, gateVal);
                Log("游戏进程退出，退出码 0x" + ((uint)code).ToString("X8") + "，启动器随之退出");
                RestoreDefaultMode();
                // 游戏一退出就跟着退出，并把游戏的退出码原样交回启动平台（崩溃码要能被认出来）
                FinishWith(code);
            }
            catch (Exception ex)
            {
                Log("启动失败：" + ex);
                RestoreDefaultMode();
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
            }
            finally
            {
                SetBusy(false, null);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    // 门店广告：用系统默认浏览器打开广告网页，停留 AdDelaySec 秒再让游戏启动。
    // 这是给黄金版门店看的；钻石版不带 /ad 参数就不走这里。
    // 广告打不开（没网址、没浏览器、用户关掉）都不能挡住游戏启动，所以全程吞异常。
    void ShowAd()
    {
        string url = (adUrlOverride != null && adUrlOverride.Length > 0) ? adUrlOverride : adUrl;
        if (url == null || url.Trim().Length == 0)
        {
            Log("广告：已开（/ad）但没配网址，跳过");
            return;
        }
        url = url.Trim();
        try
        {
            // UseShellExecute=true 交给系统默认程序打开网址；走这条不会碰 EnvironmentVariables，
            // 避开本机 HTTPS_PROXY/https_proxy 大小写重复导致 .NET 字典抛异常的坑。
            ProcessStartInfo psi = new ProcessStartInfo(url);
            psi.UseShellExecute = true;
            Process.Start(psi);
            Log("广告：已打开网页 " + url + "，停留 " + adDelaySec + " 秒后启动游戏");
        }
        catch (Exception ex)
        {
            Log("广告：打开失败（不影响游戏）：" + ex.Message);
            return;
        }
        if (adDelaySec > 0) Thread.Sleep(adDelaySec * 1000);
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

    // 游戏还在跑的时候关窗：不退出进程，隐藏到后台继续陪跑，
    // 等游戏退出后由 FinishWith 带着游戏的退出码结束（启动平台才拿得到崩溃码）。
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
            tray.Icon = Icon;               // 与窗口/任务栏同一个图标（游戏自己的）
            tray.Text = gameTitle + " · 游戏运行中";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };
        }
        Hide();
    }

    void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    // 图标不自绘、不嵌资源：运行时取游戏 exe 自带的图标，和平台里显示的游戏图标一致
    void LoadGameIcon()
    {
        try
        {
            Icon own = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (own != null) { Icon = own; return; }
        }
        catch (Exception ex) { Log("取 exe 图标失败：" + ex.Message); }
        // 取不到只退回系统默认图标；绝不从游戏 exe 抽图标（平台/任务管理器会把启动器当成游戏进程）
        Icon = SystemIcons.Application;
    }

    bool ApplyMode(int index, out string message)
    {
        string src = Path.Combine(AppDir(), layerBackupDir, ModeFiles[index]);
        string dst = Path.Combine(AppDir(), layerIni);
        if (!File.Exists(src))
        {
            message = "缺少画面模式配置 " + Path.Combine(layerBackupDir, ModeFiles[index]);
            return false;
        }
        try
        {
            File.Copy(src, dst, true);
            message = ModeNames[index];
            return true;
        }
        catch (Exception ex) { message = "切换画面模式失败：" + ex.Message; return false; }
    }

    void RestoreDefaultMode()
    {
        try
        {
            string bak = Path.Combine(AppDir(), layerBackupDir, layerIni);
            if (File.Exists(bak)) File.Copy(bak, Path.Combine(AppDir(), layerIni), true);
        }
        catch { }
    }

    void ApplyKeymap(int index, out string message)
    {
        int rc;
        try { rc = KeymapCli(new string[] { (index + 1).ToString() }); }
        catch (Exception ex) { rc = 9; message = "按键方案写入异常：" + ex.Message; Log(message); return; }
        Log("按键方案 " + (index + 1) + "（" + KeySubNames[index] + "）写入结果 rc=" + rc);
        if (rc == 0) { message = null; return; }
        message = rc == 2
            ? "按键方案没写入（需要管理员权限），本次沿用旧按键"
            : "按键方案写入失败（错误 " + rc + "），本次沿用旧按键";
    }

    bool EnsureLayer(out string message)
    {
        string dir = AppDir();
        string dll = Path.Combine(dir, layerDll);
        string ini = Path.Combine(dir, layerIni);
        string bak = Path.Combine(dir, layerBackupDir);
        if (!File.Exists(dll) && File.Exists(Path.Combine(bak, layerDll)))
        {
            try
            {
                File.Copy(Path.Combine(bak, layerDll), dll, true);
                message = "已从 " + layerBackupDir + " 恢复兼容层";
                return true;
            }
            catch (Exception ex) { message = "恢复运行文件失败：" + ex.Message; return false; }
        }
        if (!File.Exists(dll))
        {
            message = "缺少 " + layerDll + "，请先双击 兼容修复.bat";
            return false;
        }
        string bakIni = Path.Combine(bak, layerIni);
        if (File.Exists(bakIni)) { try { File.Copy(bakIni, ini, true); } catch { } }
        message = "已就绪";
        return true;
    }

    static int RunAndWait(string dir, string file, string args, string gateEnvName = null, string gateValValue = null)
    {
        ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false;      // 必须：游戏要挂在 game_starter.exe 名下，平台按进程树认
        // 启动门禁暗号：只在启动真游戏时传进来。
        // 不用 psi.EnvironmentVariables：本机同时存在 HTTPS_PROXY / https_proxy（仅大小写不同），
        // .NET Framework 构造那个字典时会抛「已添加项」，导致游戏根本起不来。
        // 改成临时写本进程环境变量 -> Process.Start 时子进程继承 -> 立刻恢复原值，try/finally 兜底。
        // 暗号只活在这条进程树里，游戏一退就没了，不写系统、不影响玩家双击。
        bool gateOn = gateEnvName != null && gateEnvName.Length > 0 && gateValValue != null && gateValValue.Length > 0;
        string oldVal = gateOn ? Environment.GetEnvironmentVariable(gateEnvName) : null;
        if (gateOn) Environment.SetEnvironmentVariable(gateEnvName, gateValValue);
        try
        {
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        finally
        {
            if (gateOn) Environment.SetEnvironmentVariable(gateEnvName, oldVal);
        }
    }

    // 自检：game_starter.exe /selftest <程序> [参数…]  按当前配置启动它并把它的退出码原样回传
    // 用来证明「平台拿到的退出码 == 游戏退出码」这条链路是通的（不需要真开游戏）。
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
        if (cbMode != null)
        {
            cbMode.Enabled = !value;
            cbKey.Enabled = !value;
            cbSave.Enabled = !value;
            cbLevel.Enabled = !value;
            if (value) focus = NoFocus;
        }
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

    // ---------- 运行日志：玩家出问题直接把「运行日志.txt」发给管理员 ----------
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
                    File.WriteAllText(p, "运行日志：出问题时把这个文件发给管理员\r\n", Encoding.UTF8);
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

    void WriteSmall(string file, string value)
    {
        try { File.WriteAllText(Path.Combine(AppDir(), file), value + "\r\n", Encoding.ASCII); }
        catch { }
    }

    int ReadSaveLevel()
    {
        try
        {
            string path = Path.Combine(AppDir(), "savelevel.txt");
            if (!File.Exists(path)) return 5;
            int n;
            if (int.TryParse(File.ReadAllText(path).Trim(), out n) && n >= 0 && n < LevelNames.Length) return n;
        }
        catch { }
        return 5;
    }

    int ReadSavePick()
    {
        try
        {
            string path = Path.Combine(AppDir(), "savepick.txt");
            if (!File.Exists(path)) return 0;
            int n;
            if (int.TryParse(File.ReadAllText(path).Trim(), out n) && n >= 0) return n;
        }
        catch { }
        return 0;
    }

    int ReadKeymapChoice()
    {
        try
        {
            string path = Path.Combine(AppDir(), "keymap.txt");
            if (!File.Exists(path)) return 0;
            int n;
            if (int.TryParse(File.ReadAllText(path).Trim(), out n) && n >= 1 && n <= KeyNames.Length) return n - 1;
        }
        catch { }
        return 0;
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
                case "autoloadsave": autoLoadSave = val != "0"; break;
                case "gameprocess": gameProcess = val; break;
                case "layerdll": layerDll = val; break;
                case "layerini": layerIni = val; break;
                case "layerbackupdir": layerBackupDir = val; break;
                case "savesdir": savesDir = val; break;
                case "saveprefix": savePrefix = val; break;
                case "gateenv": gateEnv = val; break;
                case "gateval": gateVal = val; break;
                case "adurl": adUrl = val; break;
                case "addelay": int d; if (int.TryParse(val, out d) && d >= 0 && d <= 600) adDelaySec = d; break;
                case "intro": intro = val; break;
                case "cheats": cheats = val; break;
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
        base.OnFormClosed(e);
    }
}
