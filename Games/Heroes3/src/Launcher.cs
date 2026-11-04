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
using Microsoft.Win32;

// 英雄无敌3：死亡阴影启动器（game_starter.exe），界面规范见仓库
// .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、三栏 900×620、拟物皮革、正常 TextRenderer。
//
// 两条通道六个档位（名字是用户选的 B 套：大白话 + 技术名）：
// cnc-ddraw 7.1 的 GDI 软件画面（全屏 / 4:3黑边 / 窗口 / 原版640）与 DX9硬件加速，
// dgVoodoo2 转译（窗口）；auto 与 DXVK 在本游戏实测易崩，不提供。
// 启动器直接启动 Heroes3.exe，按所选模式切换 _presets\ 里的配置，退出后恢复默认。

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革，见 .qoder/skills/legacy-launcher-gui/SKILL.md） ----------
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
    string gameTitle = "英雄无敌3：死亡阴影";
    string gameExe = "Heroes3.exe";
    string gameArgs = "";
    string gameProcess = "Heroes3";
    string gameIconExe = "Heroes3.exe";
    string initExe = "";
    bool runInit = false;
    long affinity = 0;
    string savesSource = "";
    string savesTarget = "";
    string layerDll = "ddraw.dll";
    string layerBackupDir = "_cnc-ddraw";
    string intro =
        "《英雄无敌3：死亡阴影》是 New World Computing\n" +
        "1999 年的回合制策略经典：招募英雄、经营城镇、\n" +
        "探索地图，用魔法和兵种打一场场战役。\n" +
        "\n" +
        "这份是 v3.1 简体中文完整版，\n" +
        "直接进游戏即可。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给管理员";
    string entryGuide =
        "启动后先等加载（第一次较慢）\n" +
        "想切窗口选第 3 档\n" +
        "画面异常先回第 1 档\n" +
        "多人：游戏内选 IPX 或 TCP/IP\n" +
        "联机组件已带好，不用另装";
    string tips =
        "# 提示\n" +
        "默认第 1 档：软件画面 GDI\n" +
        "黑屏/花屏先回第 1 档，再试窗口档\n" +
        "备选：DX9硬件加速、\n" +
        "dgVoodoo2转换（无边框全屏），能用就别常换\n" +
        "花屏/黑屏先关远程软件、HDR、覆盖层\n" +
        "高刷屏已经处理过，不用再调";
    string centerNote =
        "共 6 档，默认第 1 档软件画面 GDI。\n" +
        "人物拉胖选 2，切窗口选 3 或 4，\n" +
        "都不行再试：5 DX9硬件加速、\n" +
        "6 dgVoodoo2转换（无边框全屏）。\n" +
        "\n" +
        "启动器只切换显示方式，不改存档。";

    // ---------- 启动方式 ----------
    static readonly string[] ModeNames =
    {
        "1  软件画面 GDI（全屏）",
        "2  软件画面 GDI（4:3黑边）",
        "3  软件画面 GDI（窗口1280×720）",
        "4  原版640×480（窗口）",
        "5  DX9硬件加速（全屏）",
        "6  dgVoodoo2转换（无边框全屏）"
    };
    static readonly string[] ModePresets =
    {
        "ddraw.ini",
        "ddraw-43.ini",
        "ddraw-window.ini",
        "ddraw-640.ini",
        "ddraw-d3d9.ini",
        "dgVoodoo-window.conf"
    };
    // 每个模式顶在 ddraw.dll 位置上的层：cnc = cnc-ddraw，voodoo = dgVoodoo2
    static readonly string[] ModeLayer =
    {
        // dgVoodoo2 全屏这条路实测会让游戏报 DDERR_INVALIDRECT（WINGRAPH.CPP:456），已撤下，只留窗口
        "cnc", "cnc", "cnc", "cnc", "cnc", "voodoo"
    };
    static readonly string[] LayerDirs =
    {
        "cnc", "_cnc-ddraw",
        "voodoo", "_dgvoodoo2"
    };

    // 窗口模式（1280×720）：除零类闪退的兜底方式
    const int WINDOW_MODE = 2;

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int mode = 5;                    // 默认第 6 档：dgVoodoo2转换（无边框全屏）
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, layerOk;
    string status = "已就绪";
    Color statusColor = Ok;
    LegendItem[][] legend = new LegendItem[0][];
    volatile int exitCode;
    volatile bool finishing;
    NotifyIcon tray;
    bool autoStart;

    // ---------- 字体/布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcLabelMode, rcComboMode, rcStart, rcFolder, rcExit;
    SelectPanel selMode;
    ContextMenuStrip modeMenu;

    class LegendItem { public string Caption; public string[] Caps; public string Note; }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "/selftest" || args[0] == "-selftest"))
            return SelfTest(Tail(args));
        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();
        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        int remembered = ReadModeMemory();
        if (remembered >= 0 && remembered < ModeNames.Length) l.mode = remembered;
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
            "；零交互：" + (l.autoStart ? "是" : "否"));
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        Application.Run(l);
        return l.exitCode;
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
        if (selMode == null) selMode = MakeModeSelect();
        LayoutRects();
        string msg;
        layerOk = EnsureLayer(out msg);
        status = msg;
        statusColor = layerOk ? Ok : Warn;
        LoadGameIcon();
        Log("启动 " + gameTitle + "｜" + status + (autoStart ? "｜/go 零交互" : ""));
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
        int btnY = colCenter.Bottom - S(14) - S(34) - S(10) - S(44);
        rcLabelMode = new Rectangle(inner, btnY - S(66), innerW, S(22));
        rcComboMode = new Rectangle(inner, btnY - S(40), innerW, S(30));
        rcStart = new Rectangle(inner, btnY, innerW, S(44));
        rcFolder = new Rectangle(inner, btnY + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y, innerW - (rcFolder.Right + S(8) - inner), S(34));
        PlaceSelector(selMode, rcComboMode);
    }

    // ---------- 绘制 ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        DrawLeatherBg(g, all);
        DrawBand(g, new Rectangle(0, 0, ClientSize.Width, S(76)));
        int footY = S(560);
        DrawBand(g, new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY));

        DrawHeader(g);
        DrawLeft(g);
        DrawCenter(g);
        DrawRight(g);
        DrawFooter(g);
    }

    // 背景：竖直渐变 + 两组反向 1px 斜线当皮纹 + PathGradientBrush 压暗四角（不引图片）
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
        TextRenderer.DrawText(g, "Heroes of Might and Magic III · v3.1",
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
        DrawWrapped(g, intro, x, y, colLeft.Width - S(32), fBody, Ink);
    }

    void DrawCenter(Graphics g)
    {
        Card(g, colCenter);
        int x = colCenter.X + S(16), y = colCenter.Y + S(14);
        TextRenderer.DrawText(g, "准备开玩", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(32);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);

        TextRenderer.DrawText(g, "启动方式", fHead, new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy && layerOk,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true, hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
        if (focus == 3 && selMode != null) FocusRing(g, selMode.Bounds);
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

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
            TextRenderer.DrawText(g, "操作说明", fHead, new Point(x, y), Gold, Color.Transparent);
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
                    if (cx + itemW > rightEdge && cx > x) break;
                    int ty = y + (capH - cs.Height) / 2;
                    TextRenderer.DrawText(g, item.Caption, fBody, new Point(cx, ty), Ink, Color.Transparent);
                    cx += cs.Width + S(6);
                    if (item.Note != null)
                    {
                        Size ns = TextRenderer.MeasureText(g, item.Note, fBody);
                        TextRenderer.DrawText(g, item.Note, fBody, new Point(cx, y + (capH - ns.Height) / 2), Ink, Color.Transparent);
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
        using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
            g.DrawLine(sh, colLeft.X, S(567), ClientSize.Width - S(20), S(567));
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

    void Keycap(Graphics g, string label, int x, int y, int w, int h)
    {
        Rectangle r = new Rectangle(x, y, w, h);
        using (GraphicsPath p = Round(r, S(5)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, CapFace, CapSide, LinearGradientMode.Vertical))
                g.FillPath(b, p);
            using (Pen edge = new Pen(PanelEdge, 1f)) g.DrawPath(edge, p);
        }
        using (Pen hi = new Pen(Color.FromArgb(95, CapTopHi), 1f))
            g.DrawLine(hi, r.X + S(3), r.Y + S(2), r.Right - S(3), r.Y + S(2));
        using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
            g.DrawLine(sh, r.X + S(3), r.Bottom - S(2), r.Right - S(3), r.Bottom - S(2));
        Font f = CapFont(label);
        TextRenderer.DrawText(g, label, f,
            new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), Ink, Color.Transparent,
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

    // ---------- 键帽表 ----------
    void RebuildLegend()
    {
        legend = new LegendItem[][]
        {
            new[] { PlainItem("鼠标左键", "移动/选择") },
            new[] { PlainItem("鼠标右键", "查看/取消") },
            new[] { Item("等待", "空格") },
            new[] { Item("结束回合", "Enter") },
            new[] { Item("菜单", "Esc") }
        };
    }

    static LegendItem Item(string caption, params string[] caps)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        it.Caps = caps;
        return it;
    }

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
        if (!busy && selMode != null && selMode.Bounds.Contains(pt)) return 3;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcFolder);
            r = Rectangle.Union(r, rcExit);
            if (selMode != null) r = Rectangle.Union(r, selMode.Bounds);
            return r;
        }
    }

    void RepaintControls() { Invalidate(ButtonsRect); }

    static void PlaceSelector(SelectPanel p, Rectangle slot)
    {
        if (p == null) return;
        p.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);
    }

    SelectPanel MakeModeSelect()
    {
        SelectPanel p = new SelectPanel();
        p.BackColor = Color.Transparent;
        p.ForeColor = Ink;
        p.Font = fRowSel;
        p.Text = ModeNames[mode];
        p.Cursor = Cursors.Hand;
        p.Paint += PaintModeSelect;
        p.Click += delegate { ShowModeMenu(); };
        Controls.Add(p);
        return p;
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
        Rectangle tr = new Rectangle(S(12), 0, p.Width - S(36), p.Height);
        TextRenderer.DrawText(e.Graphics, p.Text, fRowSel, tr,
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
        if (modeMenu == null) BuildModeMenu();
        for (int i = 0; i < modeMenu.Items.Count; i++)
            modeMenu.Items[i].Text = (i == mode ? "\u2713  " : "    ") + ModeNames[i];
        FitMenuSize(modeMenu, ModeNames);
        modeMenu.Show(selMode, new Point(0, selMode.Height + S(2)));
        Log("下拉高度自检：项数=" + ModeNames.Length + "，弹层高=" + modeMenu.Height +
            "，能装下=" + (modeMenu.Height >= ModeNames.Length * modeMenu.Items[0].Height));
    }

    void StepMode(int delta)
    {
        int n = ModeNames.Length;
        mode = ((mode + delta) % n + n) % n;
        selMode.Text = ModeNames[mode];
        selMode.Invalidate();
        Log("选择显示方式：" + ModeNames[mode]);
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
                Log("选择显示方式：" + ModeNames[idx]);
            };
            modeMenu.Items.Add(item);
        }
    }

    // 框本身宽度不动（布局照旧），只把弹层加宽到装得下最长的一项
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
        int[] order = busy ? new int[] { 1, 2 } : new int[] { 3, 0, 1, 2 };
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
        if (i == 2) { Close(); }
    }

    // ---------- 现场快照 ----------
    // 0xC0000094 是整数除零。这游戏是 1999 年的代码，导入表里就有 GetDeviceCaps 和 GetDiskFreeSpaceA：
    // 系统把刷新率报成 0、或某个盘的扇区/簇大小报成 0，它拿去当除数就直接炸。
    // 这里把这两个来源原样记进 运行日志.txt，并据此决定要不要改用窗口模式。
    bool SnapshotEnvironment(out string reason)
    {
        reason = "";
        int refresh = 0, cx = 0, cy = 0, dpiX = 0, dpiY = 0;
        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                refresh = GetDeviceCaps(hdc, VREFRESH);
                cx = GetDeviceCaps(hdc, HORZRES);
                cy = GetDeviceCaps(hdc, VERTRES);
                dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
        }
        catch (Exception ex) { Log("取显示参数失败：" + ex.Message); }
        int screens = 1;
        try { screens = Screen.AllScreens.Length; } catch { }

        string root = "";
        try { root = Path.GetPathRoot(AppDir()); } catch { }
        uint bps = 0, spc = 0, freeC = 0, totalC = 0;
        bool diskOk = false;
        if (root.Length > 0)
        {
            try { diskOk = GetDiskFreeSpaceA(root, out spc, out bps, out freeC, out totalC); }
            catch (Exception ex) { Log("取磁盘参数失败：" + ex.Message); }
        }
        string floppy = "";
        try
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                string n = d.Name;
                if (n != "A:\\" && n != "B:\\") continue;
                uint a, b, c, e;
                bool ok = GetDiskFreeSpaceA(n, out a, out b, out c, out e);
                floppy += " " + n + (ok ? ("(每簇扇数" + a + " 每扇区字节" + b + ")") : "(读不到)");
            }
        }
        catch { }

        Log("现场快照：显示 " + cx + "×" + cy + " 刷新率=" + refresh + " DPI=" + dpiX + "/" + dpiY +
            " 屏幕数=" + screens + "；游戏盘 " + root + " 每扇区字节=" + bps + " 每簇扇区=" + spc +
            " 簇 总" + totalC + "/可用" + freeC + (diskOk ? "" : "（GetDiskFreeSpace 失败）") +
            (floppy.Length == 0 ? "" : "；软驱" + floppy) +
            "；AppPath=" + RegValue("AppPath") + " CDDrive=" + RegValue("CDDrive"));

        List<string> zero = new List<string>();
        if (refresh <= 0) zero.Add("刷新率=" + refresh);
        if (bps == 0 || spc == 0) zero.Add("游戏盘每扇区字节/每簇扇区=" + bps + "/" + spc);
        if (cx <= 0 || cy <= 0) zero.Add("桌面尺寸=" + cx + "×" + cy);
        if (zero.Count == 0) return false;
        reason = string.Join("、", zero.ToArray());
        return true;
    }

    static string RegValue(string name)
    {
        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey k = baseKey.OpenSubKey(
                @"SOFTWARE\New World Computing\Heroes of Might and Magic?III\1.0"))
            {
                if (k == null) return "（键不存在）";
                object v = k.GetValue(name);
                return v == null ? "（无此值）" : Convert.ToString(v);
            }
        }
        catch (Exception ex) { return "（读失败:" + ex.Message + "）"; }
    }

    // ---------- 换层：把所选模式要用的 ddraw.dll 顶上去，并铺好它的配置 ----------
    static string LayerDirOf(string tag)
    {
        for (int i = 0; i < LayerDirs.Length; i += 2)
            if (LayerDirs[i] == tag) return LayerDirs[i + 1];
        return "_cnc-ddraw";
    }

    // 返回 false 表示这个模式的层或配置不在，调用方要退回默认模式
    bool ApplyLayer(int which, out string fail)
    {
        fail = "";
        string tag = ModeLayer[which];
        string dir = LayerDirOf(tag);
        string dllSrc = Path.Combine(AppDir(), dir, "ddraw.dll");
        string confSrc = Path.Combine(AppDir(), "_presets", ModePresets[which]);
        if (!File.Exists(dllSrc)) { fail = dir + "\\ddraw.dll 缺失"; return false; }
        if (!File.Exists(confSrc)) { fail = "_presets\\" + ModePresets[which] + " 缺失"; return false; }
        try
        {
            File.Copy(dllSrc, Path.Combine(AppDir(), "ddraw.dll"), true);
            if (tag == "voodoo")
            {
                File.Copy(confSrc, Path.Combine(AppDir(), "dgVoodoo.conf"), true);
                // dgVoodoo2 和 cnc-ddraw 都靠 ddraw.ini / dgVoodoo.conf 读配置，
                // 用哪一层就只留哪一份，免得上一份的残留把这一层带歪
                try { if (File.Exists(Path.Combine(AppDir(), "ddraw.ini"))) File.Delete(Path.Combine(AppDir(), "ddraw.ini")); } catch { }
            }
            else
            {
                File.Copy(confSrc, Path.Combine(AppDir(), "ddraw.ini"), true);
                try { if (File.Exists(Path.Combine(AppDir(), "dgVoodoo.conf"))) File.Delete(Path.Combine(AppDir(), "dgVoodoo.conf")); } catch { }
            }
            Log("已挂载 " + (tag == "voodoo" ? "dgVoodoo2" : "cnc-ddraw") + " + " + ModePresets[which]);
            return true;
        }
        catch (Exception ex) { fail = ex.Message; return false; }
    }

    // 收尾回到默认档（第 6 档 dgVoodoo2）；缺 dgVoodoo2 文件时退回 cnc-ddraw GDI，
    // 总之只留一份配置在位，不在机器上留两层叠着的现场
    void RestoreDefaultLayer()
    {
        try
        {
            string vDll = Path.Combine(AppDir(), "_dgvoodoo2", "ddraw.dll");
            string vConf = Path.Combine(AppDir(), "_presets", "dgVoodoo-window.conf");
            if (File.Exists(vDll) && File.Exists(vConf))
            {
                File.Copy(vDll, Path.Combine(AppDir(), "ddraw.dll"), true);
                File.Copy(vConf, Path.Combine(AppDir(), "dgVoodoo.conf"), true);
                string left = Path.Combine(AppDir(), "ddraw.ini");
                if (File.Exists(left)) File.Delete(left);
                Log("收尾回到默认档：dgVoodoo2 + dgVoodoo-window.conf");
                return;
            }
            string dll = Path.Combine(AppDir(), "_cnc-ddraw", "ddraw.dll");
            string conf = Path.Combine(AppDir(), "_presets", "ddraw.ini");
            if (File.Exists(dll)) File.Copy(dll, Path.Combine(AppDir(), "ddraw.dll"), true);
            if (File.Exists(conf)) File.Copy(conf, Path.Combine(AppDir(), "ddraw.ini"), true);
            string rest = Path.Combine(AppDir(), "dgVoodoo.conf");
            if (File.Exists(rest)) File.Delete(rest);
            Log("收尾退回 cnc-ddraw GDI（缺 dgVoodoo2 文件）");
        }
        catch (Exception ex) { Log("恢复默认层失败：" + ex.Message); }
    }

    // ---------- 记住这台机器验证过能玩的画面方式 ----------
    static string ModeMemoryPath() { return Path.Combine(AppDir(), "显示方式记忆.txt"); }

    static int ReadModeMemory()
    {
        try
        {
            if (!File.Exists(ModeMemoryPath())) return -1;
            int v;
            if (!int.TryParse(File.ReadAllText(ModeMemoryPath(), Encoding.UTF8).Trim(), out v)) return -1;
            return v;
        }
        catch { return -1; }
    }

    static void SaveModeMemory(int which)
    {
        try { File.WriteAllText(ModeMemoryPath(), which.ToString(), Encoding.UTF8); }
        catch (Exception ex) { Log("记画面方式失败：" + ex.Message); }
    }

    // ---------- 启动逻辑 ----------
    void StartGame()
    {
        if (busy) return;
        string message;
        if (!EnsureLayer(out message))
        {
            status = message; statusColor = Warn; layerOk = false; Invalidate();
            return;
        }
        layerOk = true;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe; statusColor = Warn; Invalidate();
            return;
        }
        string presetPath = Path.Combine(AppDir(), "_presets", ModePresets[mode]);
        string layerSrc = Path.Combine(AppDir(), LayerDirOf(ModeLayer[mode]), "ddraw.dll");
        if (!File.Exists(presetPath) || !File.Exists(layerSrc))
        {
            Log("缺少显示预设或兼容层：" + presetPath + " / " + layerSrc);
            status = "缺少显示文件，请先运行 兼容修复.bat";
            statusColor = Warn; Invalidate();
            return;
        }
        string launchProcess = gameProcess.Length > 0 ? gameProcess : "Heroes3";
        Log("点「启动游戏」：" + ModeNames[mode]);
        SetBusy(true, "启动中：" + ModeNames[mode].Substring(ModeNames[mode].IndexOf(' ') + 1));
        Thread t = new Thread(delegate ()
        {
            int usedMode = mode;
            try
            {
                PrepareGame();
                EnsureRegistry();
                string why;
                if (SnapshotEnvironment(out why) && usedMode != WINDOW_MODE && usedMode != 3)
                {
                    Log("检测到能当除数的 0 值（" + why + "），本次改用「" + ModeNames[WINDOW_MODE] + "」");
                    usedMode = WINDOW_MODE;
                    SetStatus("已自动改用窗口画面，避免闪退", Ok);
                }
                int attempt = 0;
                while (true)
                {
                    attempt++;
                    string failLayer;
                    if (!ApplyLayer(usedMode, out failLayer))
                    {
                        Log("该模式不可用（" + failLayer + "），退回「软件画面 GDI（全屏）」");
                        usedMode = 0;
                        if (attempt > 1) { FinishWith(1); break; }
                        continue;
                    }
                    SetStatus("游戏运行中…", Ok);
                    DateTime start = DateTime.Now;
                    int code = RunGameWithAffinity(exePath, gameArgs, launchProcess);
                    int secs = (int)(DateTime.Now - start).TotalSeconds;
                    bool bad = (uint)code >= 0xC0000000;
                    Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                        (bad ? "（系统级异常）" : ""));
                    if (bad && secs < 25 && attempt == 1 && usedMode != WINDOW_MODE && usedMode != 3)
                    {
                        Log("开局就异常退出，改用「" + ModeNames[WINDOW_MODE] + "」再试一次");
                        SetStatus("游戏闪退，已自动改用窗口重试", Ok);
                        usedMode = WINDOW_MODE;
                        continue;
                    }
                    if (secs >= 45)
                    {
                        SaveModeMemory(usedMode);
                        if (usedMode != mode) Log("这个画面方式在这台机器上稳住了，已记住，下次直接用它");
                    }
                    FinishWith(code);
                    break;
                }
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
            }
            finally
            {
                RestoreDefaultLayer();
                SetBusy(false, null);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    void PrepareGame()
    {
        if (runInit && File.Exists(Path.Combine(AppDir(), initExe)))
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(AppDir(), initExe));
                psi.WorkingDirectory = AppDir();
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    Log("init.exe 已执行，退出码 " + p.ExitCode);
                }
            }
            catch (Exception ex) { Log("init.exe 执行失败（继续启动）：" + ex.Message); }
        }
        if (savesSource.Length > 0 && savesTarget.Length > 0)
        {
            try
            {
                string target = Environment.ExpandEnvironmentVariables(savesTarget);
                Directory.CreateDirectory(target);
                string src = Path.Combine(AppDir(), savesSource);
                if (Directory.Exists(src))
                {
                    foreach (string f in new[] { "profile.bin", "slot-A.bin", "slot-B.bin", "slot-C.bin" })
                    {
                        string s = Path.Combine(src, f), d = Path.Combine(target, f);
                        if (File.Exists(s) && !File.Exists(d)) File.Copy(s, d, true);
                    }
                    Log("存档目录已检查：" + target);
                }
            }
            catch (Exception ex) { Log("存档准备失败（继续启动）：" + ex.Message); }
        }
    }

    void EnsureRegistry()
    {
        string dir = AppDir().TrimEnd('\\') + "\\";
        string keyPath = @"SOFTWARE\New World Computing\Heroes of Might and Magic?III\1.0";
        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey k = baseKey.OpenSubKey(keyPath))
            {
                if (k != null && string.Equals(Convert.ToString(k.GetValue("AppPath")), dir,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Log("注册表 AppPath 已就绪");
                    return;
                }
            }

            string drive = Path.GetPathRoot(dir).TrimEnd('\\');
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            sb.AppendLine(@"[HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\New World Computing\Heroes of Might and Magic?III\1.0]");
            sb.AppendLine("\"AppPath\"=\"" + dir.Replace("\\", "\\\\") + "\"");
            sb.AppendLine("\"CDDrive\"=\"" + drive + "\"");
            sb.AppendLine("\"Show Intro\"=dword:00000001");
            sb.AppendLine("\"Test Decomp\"=dword:00000000");
            sb.AppendLine("\"Test Read\"=dword:00000000");
            sb.AppendLine("\"Test Blit\"=dword:00000000");
            string tmp = Path.Combine(Path.GetTempPath(), "h3-launcher.reg");
            File.WriteAllText(tmp, sb.ToString(), Encoding.Unicode);
            ProcessStartInfo psi = new ProcessStartInfo("reg.exe", "import \"" + tmp + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                if (p.ExitCode == 0) Log("注册表 AppPath 已写入：" + dir);
                else Log("注册表写入失败（继续启动），退出码 " + p.ExitCode);
            }
            try { File.Delete(tmp); } catch { }
        }
        catch (Exception ex)
        {
            Log("注册表设置失败（继续启动）：" + ex.Message);
        }
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
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessAffinityMask(IntPtr h, UIntPtr mask);
    [DllImport("kernel32.dll")]
    static extern uint ResumeThread(IntPtr h);
    [DllImport("kernel32.dll")]
    static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    // ---------- 除零类崩溃的取证用 API ----------
    const int VREFRESH = 0, HORZRES = 8, VERTRES = 10, LOGPIXELSX = 88, LOGPIXELSY = 90;
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    // 注意出参顺序：每簇扇数、每扇区字节、可用簇、总簇（MSDN 就是这个顺序，写反会把日志标错）
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool GetDiskFreeSpaceA(string root, out uint sectorsPerCluster,
        out uint bytesPerSector, out uint freeClusters, out uint totalClusters);

    int RunGameWithAffinity(string exePath, string launchArgs, string launchProcess)
    {
        DateTime startedAt = DateTime.Now;
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        string cmd = "\"" + exePath + "\"" + (launchArgs.Length > 0 ? " " + launchArgs : "");
        bool started = CreateProcess(exePath, cmd, IntPtr.Zero, IntPtr.Zero,
            false, 0x4, IntPtr.Zero, AppDir(), ref si, out pi);     // CREATE_SUSPENDED
        if (started)
        {
            if (affinity > 0)
            {
                bool ok = SetProcessAffinityMask(pi.hProcess, (UIntPtr)(ulong)affinity);
                Log("SetProcessAffinityMask 0x" + affinity.ToString("X") + " = " + ok);
            }
            ResumeThread(pi.hThread);
            CloseHandle(pi.hThread);
            Log("包装进程 " + pi.dwProcessId + (affinity > 0 ? "（亲和性 0x" + affinity.ToString("X") + "）" : ""));

            // 游戏自己会再拉一层 Future Soldier DX11.exe；等待真正的游戏进程，
            // 这样崩溃码来自游戏本体，而不是只拿到包装进程的 0。
            Process actual = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline && actual == null)
            {
                foreach (Process p in Process.GetProcessesByName(launchProcess))
                {
                    bool match;
                    try { match = p.StartTime >= startedAt.AddSeconds(-2); }
                    catch { match = true; }
                    if (match && actual == null) actual = p;
                    else p.Dispose();
                }
                if (actual == null) Thread.Sleep(250);
            }

            bool gotActualCode = false;
            int actualCode = 0;
            if (actual != null)
            {
                int actualPid = actual.Id;
                actual.Dispose();
                Log("实际游戏进程 " + actualPid + "（" + launchProcess + "）");
                IntPtr hActual = OpenProcess(0x101000, false, actualPid);   // SYNCHRONIZE | QUERY_LIMITED_INFORMATION
                if (hActual != IntPtr.Zero)
                {
                    WaitForSingleObject(hActual, 0xFFFFFFFF);   // INFINITE
                    uint code;
                    gotActualCode = GetExitCodeProcess(hActual, out code);
                    if (gotActualCode) actualCode = (int)code;
                    CloseHandle(hActual);
                }
                else
                {
                    Log("OpenProcess 实际游戏进程失败：" + Marshal.GetLastWin32Error());
                }
                WaitForSingleObject(pi.hProcess, 30000);
            }
            else
            {
                Log("未找到 " + launchProcess + "，等待包装进程");
                WaitForSingleObject(pi.hProcess, 0xFFFFFFFF);   // INFINITE
            }

            uint wrapperCode;
            bool gotWrapper = GetExitCodeProcess(pi.hProcess, out wrapperCode);
            CloseHandle(pi.hProcess);
            Log("GetExitCodeProcess(包装) = " + gotWrapper + "，退出码 " + wrapperCode + "；游戏实际退出码 = " + (gotActualCode ? actualCode.ToString() : "未取到"));
            return gotActualCode ? actualCode : (int)wrapperCode;
        }
        Log("CreateProcess 失败，退回普通启动：错误 " + Marshal.GetLastWin32Error());
        ProcessStartInfo psi = new ProcessStartInfo(exePath, launchArgs);
        psi.WorkingDirectory = AppDir();
        psi.UseShellExecute = false;
        using (Process game = Process.Start(psi))
        {
            game.WaitForExit();
            return game.ExitCode;
        }
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
            Icon own = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (own != null) { Icon = own; return; }
        }
        catch (Exception ex) { Log("取 exe 图标失败：" + ex.Message); }
        // 取不到只退回系统默认图标；绝不从游戏 exe 抽图标（平台/任务管理器会把启动器当成游戏进程）
        Icon = SystemIcons.Application;
    }

    bool EnsureLayer(out string message)
    {
        string dir = AppDir();
        if (!File.Exists(Path.Combine(dir, gameExe)))
        {
            message = "找不到游戏程序 " + gameExe;
            return false;
        }
        if (layerDll.Length == 0)
        {
            message = "已就绪";
            return true;
        }
        string dll = Path.Combine(dir, layerDll);
        if (!File.Exists(dll) && layerBackupDir.Length > 0 && File.Exists(Path.Combine(dir, layerBackupDir, layerDll)))
        {
            try { File.Copy(Path.Combine(dir, layerBackupDir, layerDll), dll, true); }
            catch (Exception ex) { message = "恢复运行文件失败：" + ex.Message; return false; }
        }
        if (!File.Exists(dll))
        {
            message = "缺少兼容文件，请先双击 兼容修复.bat";
            return false;
        }
        message = "已就绪";
        return true;
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
        if (selMode != null) selMode.Enabled = !value;
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
                case "initexe": initExe = val; break;
                case "runinit": runInit = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "affinity":
                    long a;
                    if (long.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out a)) affinity = a;
                    else if (long.TryParse(val, out a)) affinity = a;
                    break;
                case "savessource": savesSource = val; break;
                case "savestarget": savesTarget = val; break;
                case "layerdll": layerDll = val; break;
                case "layerbackupdir": layerBackupDir = val; break;
                case "intro": intro = val; break;
                case "entryguide": entryGuide = val; break;
                case "tips": tips = val; break;
                case "centernote": centerNote = val; break;
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
