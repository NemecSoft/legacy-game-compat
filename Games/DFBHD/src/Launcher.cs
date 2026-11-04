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

// 三角洲特种部队：黑鹰坠落 - 军刀部队 启动器（game_starter.exe），界面规范见仓库
// .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、零图片、三栏布局。
//
// 启动链说明（汉化外挂限制）：
//   1. 直接子进程是「汉化外挂.exe」；它拉起 dfbhd.dat（中文版游戏，游戏进程名就是 dfbhd.dat），
//      游戏是外挂的子进程，不是 game_starter.exe 的直接子进程；
//   2. 外挂会陪跑到游戏退出；启动器按进程名等到 dfbhd.dat，等它退出并回传它的退出码；
//   3. 玩家中途关窗：窗口隐藏到托盘，进程继续陪跑，游戏不受影响；
//   4. game_starter.exe /go 按默认分辨率零交互启动。
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
    // dfv.cfg 的 game_res 档位：8=1600x1200，7=1280x1024，6=1024x768，5=800x600，4=640x480
    static readonly string[] HudNames =
    {
        "1  1600×1200 全屏（默认）",
        "2  1280×1024 全屏",
        "3  1024×768 全屏",
        "4  800×600 全屏",
        "5  640×480 全屏"
    };
    static readonly string[] HudSubNames = { "1600×1200 全屏", "1280×1024 全屏", "1024×768 全屏", "800×600 全屏", "640×480 全屏" };
    static readonly int[] HudGameRes = { 8, 7, 6, 5, 4 };

    // ---------- launcher.ini 可覆盖 ----------
    string gameTitle = "三角洲特种部队：黑鹰坠落 - 军刀部队";
    string gameExe = "汉化外挂.exe";
    string gameArgs = "";
    string gameProcess = "dfbhd.dat";
    string gameIconExe = "DFBHD.EXE";
    string layerDll = "d3d8.dll";
    string layerIni = "dxvk.conf";
    string layerBackupDir = "_dxvk";
    string intro =
        "《三角洲特种部队：黑鹰坠落》\n" +
        "NovaLogic 的战术射击，\n" +
        "跟着小队在索马里打巷战。\n" +
        "\n" +
        "这份还带「军刀部队」资料片，\n" +
        "简体中文界面，\n" +
        "单机战役和局域网都在，\n" +
        "直接进游戏即可。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给管理员";
    string entryGuide =
        "启动后进主菜单\n" +
        "单人游戏：选战役 / 任务\n" +
        "联网游戏：局域网 / NovaWorld\n" +
        "中文由汉化外挂加载，直接进即可";
    string tips =
        "# 提示\n" +
        "游戏里「选项 → 控制」可以看/改全部按键\n" +
        "分辨率用启动器选，进游戏后不用再改\n" +
        "第一次玩：单人游戏 → 战役 → 选任务";

    // ---------- 运行状态 ----------
    const int NoFocus = -99;            // 焦点在下拉框里（由 ActiveControl 管）或没有焦点
    int hud, hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, layerOk;
    string status = "已就绪";
    Color statusColor = Ok;
    LegendItem[][] legend = new LegendItem[0][];
    volatile int exitCode;              // 回传给启动平台的游戏退出码
    volatile bool finishing;            // 游戏已退出，正在真关窗
    NotifyIcon tray;                    // 游戏运行期间关窗 = 隐藏陪跑
    bool autoStart;                     // game_starter.exe /go：零交互直接开

    // ---------- 字体（按 DPI 建一次） ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;

    // ---------- 布局 ----------
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcComboHud, rcLabelHud, rcStart, rcFolder, rcExit;
    ComboBox cbHud;

    class LegendItem { public string Caption; public string[] Caps; public string Note; }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "/selftest" || args[0] == "-selftest"))
            return SelfTest(Tail(args));
        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();   // bat 包装入口：不留黑框
        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        foreach (string a in args)
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;
        string parentName = ParentProcessName();
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；父进程：" + (parentName.Length == 0 ? "未知" : parentName) +
            "；零交互：" + (l.autoStart ? "是" : "否"));
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        Application.Run(l);
        return l.exitCode;
    }

    // ---------- 父控制台窗口隐藏（game_starter.bat 薄包装用） ----------
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
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
        IntPtr snap = CreateToolhelp32Snapshot(2, 0);      // TH32CS_SNAPPROCESS
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
        RebuildLegend();
        if (cbHud == null)
        {
            cbHud = MakeCombo(HudNames, hud, delegate
            {
                hud = cbHud.SelectedIndex;
            });
            cbHud.TabIndex = 0;
        }
        LayoutRects();
        string msg;
        layerOk = EnsureLayer(out msg);
        status = msg;
        statusColor = layerOk ? Ok : Warn;
        LoadGameIcon();
        Log("启动 " + gameTitle + "｜分辨率=" + HudSubNames[hud] + "｜" + status + (autoStart ? "｜/go 零交互" : ""));
        if (autoStart) BeginInvoke(new Action(delegate { StartGame(); }));
    }

    void MakeFonts()
    {
        // 界面字体优先微软雅黑，缺了按顺序退到黑体/宋体/系统 UI 字体
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

    // 全窗口统一用 GDI+ 灰度抗锯齿：避免 ClearType 彩色描边/重影，左中右字形一致

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

    void LayoutRects()
    {
        int pad = S(20), gut = S(16);
        int c1 = S(252), c2 = S(296), c3 = S(280);
        int top = S(88), bottom = S(552);
        colLeft = new Rectangle(pad, top, c1, bottom - top);
        colCenter = new Rectangle(pad + c1 + gut, top, c2, bottom - top);
        colRight = new Rectangle(pad + c1 + gut + c2 + gut, top, c3, bottom - top);

        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        // 一张卡装下「标签 + 下拉框」和按钮，整叠在卡里垂直居中
        int rowH = S(22), capH = S(30), gap = S(26), gapBtn = S(26);
        int stack = (rowH + capH) + gap + S(44) + S(10) + S(34);
        int y = colCenter.Y + (colCenter.Height - stack) / 2;
        rcLabelHud = new Rectangle(inner, y, innerW, rowH);
        rcComboHud = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gapBtn;

        rcStart = new Rectangle(inner, y, innerW, S(44));
        rcFolder = new Rectangle(inner, y + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y, innerW - (rcFolder.Right + S(8) - inner), S(34));

        PlaceCombo(cbHud, rcComboHud);
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
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        using (LinearGradientBrush bg = new LinearGradientBrush(all, BgTop, BgBottom, LinearGradientMode.Vertical))
            g.FillRectangle(bg, all);
        // 页眉/页脚用深色渐变带，和中间的红色渐变拉开层次
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
        TextRenderer.DrawText(g, "NovaLogic · 2003 · 简体中文硬盘版（含军刀部队）",
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
        TextRenderer.DrawText(g, "画面 / 分辨率", fHead, new Point(rcLabelHud.X, rcLabelHud.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy && layerOk,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true, hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
    }

    // 右栏用和左/中栏完全相同的 TextRenderer（ClearType）；滚动只画视口内的整行，不做位图/裁剪
    static void RightText(Graphics g, string s, Font f, int x, int y, Color c)
    {
        TextRenderer.DrawText(g, s, f, new Point(x, y), c, Color.Transparent);
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
        if (InViewport(y, h, top, bottom)) RightText(g, "进游戏说明", fHead, x, y, Gold);
        y += S(28);
        y += S(10);
        y = DrawWrappedVisible(g, entryGuide, x, y, w, fBody, Ink, top, bottom);
        y += S(16);
        if (InViewport(y, h, top, bottom)) RightText(g, "操作说明", fHead, x, y, Gold);
        y += S(30);
        y = DrawLegendRows(g, legend, x, y, top, bottom);
        y += S(10);
        if (InViewport(y, 2, top, bottom))
        {
            using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, x, y, x + w, y);
        }
        y += S(12);
        return DrawWrappedVisible(g, tips, x, y, w, fBody, Ink, top, bottom);
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
                    if (item.Note != null) itemW += TextRenderer.MeasureText(g, item.Note, fBody).Width + S(4);
                    if (cx + itemW > rightEdge && cx > x) break;   // 行内不换行；放不下就停止该行，绝不叠字
                    int ty = y + (capH - cs.Height) / 2;
                    RightText(g, item.Caption, fBody, cx, ty, Ink);
                    cx += cs.Width + S(6);
                    if (item.Note != null)
                    {
                        Size ns = TextRenderer.MeasureText(g, item.Note, fBody);
                        RightText(g, item.Note, fBody, cx, y + (capH - ns.Height) / 2, Ink);
                        cx += ns.Width + S(4);
                    }
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
        return Math.Max(S(22), TextRenderer.MeasureText(g, label, CapFont(label)).Width + S(12));
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

    // ---------- 键帽表（游戏按键固定，直接写表；鼠标/手柄用普通文字） ----------
    void RebuildLegend()
    {
        legend = new LegendItem[][]
        {
            new[] { Item("移动", "W", "A", "S", "D") },
            new[] { PlainItem("视角", "鼠标") },
            new[] { PlainItem("开火", "鼠标左键"), PlainItem("瞄准", "鼠标右键") },
            new[] { Item("跳跃", "空格"), Item("蹲下", "X") },
            new[] { Item("卧倒", "Z"), Item("站立", "C") },
            new[] { Item("换弹", "R"), Item("换武器", "1", "2", "3") },
            new[] { Item("手雷", "5"), Item("望远镜", "B") },
            new[] { Item("夜视", "N"), Item("侧身", "Q", "E") },
            new[] { Item("战绩", "Tab"), Item("控制台", "~") }
        };
    }

    static LegendItem Item(string caption, params string[] caps)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        it.Caps = caps;
        return it;
    }

    // 鼠标/手柄这类不是键盘键的输入用普通文字，不画键帽
    static LegendItem PlainItem(string caption, string note)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        it.Caps = new string[0];
        it.Note = note;
        return it;
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

    // 只刷按钮，别整窗 Invalidate（会把下拉框一起重画 → 闪）
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

    // 焦点顺序：显示 → 启动 → 日志 → 退出（游戏运行中只剩后两个）
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
        if (ActiveControl == cbHud) return -1;
        return focus;
    }

    void SetFocusTag(int t)
    {
        focus = NoFocus;
        ActiveControl = null;
        if (t == -1) ActiveControl = cbHud;
        else if (t >= 0) focus = t;
        if (cbHud != null) cbHud.Invalidate();
        RepaintControls();
    }

    void CycleFocus(int delta)
    {
        int[] order = busy ? new int[] { 1, 2 } : new int[] { -1, 0, 1, 2 };
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

    // ---------- 启动逻辑 ----------
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
        if (!ApplyResolution(hud, out message))
        {
            status = message;
            statusColor = Warn;
            Invalidate();
            return;
        }

        int display = hud;
        Log("点「启动游戏」：分辨率=" + HudSubNames[display]);
        SetBusy(true, "启动中：" + HudSubNames[display]);
        Thread t = new Thread(delegate ()
        {
            try
            {
                SetStatus("游戏运行中…", Ok);
                Log("启动汉化外挂：" + gameExe + (gameArgs.Length == 0 ? "" : " " + gameArgs));
                int code = RunLoaderAndWait(exePath);
                Log("游戏进程退出，退出码 0x" + ((uint)code).ToString("X8") + "，启动器随之退出");
                // 游戏一退出就跟着退出，并把游戏的退出码原样交回启动平台（崩溃码要能被认出来）
                FinishWith(code);
            }
            catch (Exception ex)
            {
                Log("启动失败：" + ex);
                LogDxvkTail();
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

    // 分辨率写 dfv.cfg：game_res 档位 + 全屏 + 打开对应 hw3d_res 开关
    bool ApplyResolution(int index, out string message)
    {
        try
        {
            string path = Path.Combine(AppDir(), "dfv.cfg");
            if (!File.Exists(path))
            {
                message = "缺少 dfv.cfg";
                return false;
            }
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"(?m)^(\s*game_res\s*=\s*)\d+", "${1}" + HudGameRes[index]);
            text = Regex.Replace(text, @"(?m)^(\s*windowed\s*=\s*)\d+", "${1}0");
            foreach (string key in new[] { "hw3d_res640x480", "hw3d_res800x600", "hw3d_res1024x768", "hw3d_res1280x1024", "hw3d_res1600x1200" })
                text = Regex.Replace(text, @"(?m)^(\s*" + Regex.Escape(key) + @"\s*=\s*)\d+", "${1}1");
            File.WriteAllText(path, text, Encoding.Default);
            message = HudSubNames[index];
            return true;
        }
        catch (Exception ex)
        {
            message = "写分辨率失败：" + ex.Message;
            return false;
        }
    }

    // 启动失败时把 DXVK 日志最后 20 行并进运行日志，玩家只需发一个文件
    static void LogDxvkTail()
    {
        try
        {
            string p = Path.Combine(AppDir(), "dfbhd.dat_d3d9.log");
            if (!File.Exists(p)) return;
            string[] lines = File.ReadAllLines(p);
            int n = Math.Min(20, lines.Length);
            Log("DXVK 日志最后 " + n + " 行：");
            for (int i = lines.Length - n; i < lines.Length; i++) Log("  " + lines[i]);
        }
        catch { }
    }

    bool EnsureLayer(out string message)
    {
        string dir = AppDir();
        string bak = Path.Combine(dir, layerBackupDir);
        foreach (string f in new[] { "d3d8.dll", "d3d9.dll" })
        {
            string p = Path.Combine(dir, f);
            if (!File.Exists(p) && File.Exists(Path.Combine(bak, f)))
            {
                try { File.Copy(Path.Combine(bak, f), p, true); }
                catch (Exception ex) { message = "恢复运行文件失败：" + ex.Message; return false; }
            }
            if (!File.Exists(p))
            {
                message = "缺少兼容文件，请先双击 兼容修复.bat";
                return false;
            }
        }
        string ini = Path.Combine(dir, layerIni);
        if (!File.Exists(ini) && File.Exists(Path.Combine(bak, layerIni)))
        {
            try { File.Copy(Path.Combine(bak, layerIni), ini, true); }
            catch { }
        }
        if (!File.Exists(ini))
        {
            message = "缺少兼容文件，请先双击 兼容修复.bat";
            return false;
        }
        if (!File.Exists(Path.Combine(dir, gameExe)))
        {
            message = "缺少汉化外挂，请先双击 兼容修复.bat";
            return false;
        }
        if (!File.Exists(Path.Combine(dir, @"LG_Data\LG_DF_BHD_TS.dll")))
        {
            message = "缺少汉化字体，请先双击 兼容修复.bat";
            return false;
        }
        message = "已就绪";
        return true;
    }

    static int RunAndWait(string dir, string file, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(file, args ?? "");
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false;      // 必须：游戏要挂在 game_starter.exe 名下，平台按进程树认
        using (Process p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    // 汉化外挂拉起 dfbhd.dat 后陪跑到游戏退出；启动器按进程名找到 dfbhd.dat，
    // 等它退出并取它的退出码（游戏是外挂的子进程，这是汉化外挂的限制）。
    int RunLoaderAndWait(string loader)
    {
        DateTime launch = DateTime.Now;
        ProcessStartInfo psi = new ProcessStartInfo(loader, gameArgs ?? "");
        psi.WorkingDirectory = AppDir();
        psi.UseShellExecute = false;
        Process loaderProc = Process.Start(psi);
        Log("汉化外挂 pid=" + loaderProc.Id);
        Process game = null;
        for (int i = 0; i < 120 && game == null; i++)
        {
            Thread.Sleep(500);
            game = FindGameProcess(launch);
            if (game == null && loaderProc.HasExited && i > 10) break;
        }
        if (game == null)
        {
            int rc = 0;
            try { if (loaderProc.HasExited) rc = loaderProc.ExitCode; } catch { }
            Log("没有等到游戏进程 " + gameProcess + "（汉化外挂退出码 " + rc + "）");
            throw new Exception("游戏没有启动，请看运行日志");
        }
        Log("游戏进程 " + game.Id + " 已启动，等待退出");
        try { game.WaitForExit(); }
        catch (Exception ex) { Log("等待游戏退出失败：" + ex.Message); throw; }
        int code = 0;
        try { code = game.ExitCode; } catch { }
        try { loaderProc.WaitForExit(5000); } catch { }
        try { game.Dispose(); } catch { }
        try { loaderProc.Dispose(); } catch { }
        return code;
    }

    Process FindGameProcess(DateTime notBefore)
    {
        string stem = Path.GetFileNameWithoutExtension(gameProcess);
        foreach (Process p in Process.GetProcesses())
        {
            bool match = false;
            try
            {
                string n = p.ProcessName;
                match = n.Equals(gameProcess, StringComparison.OrdinalIgnoreCase)
                     || (stem.Length > 2 && n.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && !n.Contains("med"));
                if (match && p.StartTime < notBefore) match = false;
            }
            catch { match = false; }
            if (match) return p;
            p.Dispose();
        }
        return null;
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
        if (cbHud != null)
        {
            cbHud.Enabled = !value;
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
                case "gameprocess": gameProcess = val; break;
                case "gameiconexe": gameIconExe = val; break;
                case "layerdll": layerDll = val; break;
                case "layerini": layerIni = val; break;
                case "layerbackupdir": layerBackupDir = val; break;
                case "intro": intro = val; break;
                case "tips": tips = val; break;
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
