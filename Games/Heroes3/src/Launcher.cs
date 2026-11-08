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
    long affinity = 0;
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
        "把内容发给我们";
    string centerNote =
        "1  软件画面 GDI（全屏）\n" +
        "2  软件画面 GDI（4:3黑边）\n" +
        "3  软件画面 GDI（窗口1280×720）\n" +
        "4  原版640×480（窗口）\n" +
        "5  DX9硬件加速（全屏）\n" +
        "6  dgVoodoo2转换（无边框全屏）\n" +
        "";

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
        // dgVoodoo2 全屏那条实测报 DDERR_INVALIDRECT（WINGRAPH.CPP:456）已判死；窗口档正常，是默认档
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
        foreach (string a0 in args)
        {
            if (a0 == "/readings" || a0 == "-readings") return DumpReadings();
            // /wer "<某个 Report.wer>"：只把这份崩溃报告读成一行结论（维护/回归用，不启动游戏、不写日志）
            if (a0 == "/wer")
            {
                if (args.Length < 2 || !File.Exists(args[1]))
                {
                    Console.WriteLine("用法：game_starter.exe /wer \"<Report.wer 路径>\" [结论另存的 UTF-8 文件]");
                    return 2;
                }
                string line = "崩溃捕获：" + ParseWer(args[1]);
                // 结论同时可以另存成 UTF-8：控制台代码页五花八门，脚本要读的是这份字节
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
        // 取不到 DPI（返回 0 或离谱值）时绝不让它变成 0 缩放：字体尺寸 0 会直接抛异常，界面全塌
        float rawDpi = 96f;
        using (Graphics g = CreateGraphics()) { if (g.DpiX > 0f) rawDpi = g.DpiX; }
        dpi = rawDpi / 96f;
        if (dpi < 0.5f || dpi > 4f) dpi = 1f;
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
        LogRegistrySnapshot();   // 先记原始状态：别让补齐动作把「客户机原本缺什么」盖掉
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
            TextRenderer.DrawText(g, "操作说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(30);
        return DrawLegendRows(g, legend, x, y, top, bottom);
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

    // ---------- 现场读数：启动前和 /readings 共用这一套采集 ----------
    // 0xC0000094 是整数除零。这游戏是 1999 年的代码，导入表里就有 GetDeviceCaps 和 GetDiskFreeSpaceA：
    // 系统把刷新率报成 0、或某个盘的扇区/簇大小报成 0，它拿去当除数就直接炸。
    class EnvRead
    {
        public int refresh, cx, cy, dpiX, dpiY, screens = 1;
        public string root = "", abDrives = "";
        public uint bps, spc, freeC, totalC;
        public bool diskOk;
        public string driveType = "";
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
        try { e.screens = Screen.AllScreens.Length; } catch { }
        try { e.root = Path.GetPathRoot(AppDir()); } catch { }
        if (e.root.Length > 0)
        {
            // 盘类型先问：老游戏对「映射盘/网络盘/内存盘」的几何读数最容易变成 0 除数
            e.driveType = DriveTypeName(GetDriveTypeW(e.root));
            try { e.diskOk = GetDiskFreeSpaceA(e.root, out e.spc, out e.bps, out e.freeC, out e.totalC); }
            catch (Exception ex) { Log("取磁盘参数失败：" + ex.Message); }
        }
        try
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                string n = d.Name;
                if (n != "A:\\" && n != "B:\\") continue;
                // 盘符不等于设备：现在的 A:/B: 基本都是虚拟盘或误报，类型只认 GetDriveType 的回答
                string kind = DriveTypeName(GetDriveTypeW(n));
                uint a, b, c, g;
                bool ok = GetDiskFreeSpaceA(n, out a, out b, out c, out g);
                e.abDrives += " " + n + "（" + kind + "）" +
                    (ok ? ("(每簇扇数" + a + " 每扇区字节" + b + ")") : "(读不到)");
            }
        }
        catch { }
        return e;
    }

    static string ReadingsLine(EnvRead e)
    {
        return "现场快照：显示 " + e.cx + "×" + e.cy + " 刷新率=" + e.refresh +
            " DPI=" + e.dpiX + "/" + e.dpiY + " 屏幕数=" + e.screens +
            "；游戏盘 " + e.root + (e.driveType.Length == 0 ? "" : "（" + e.driveType + "）") +
            " 每扇区字节=" + e.bps + " 每簇扇区=" + e.spc +
            " 簇 总" + e.totalC + "/可用" + e.freeC + (e.diskOk ? "" : "（GetDiskFreeSpace 失败）") +
            (e.abDrives.Length == 0 ? "" : "；A/B 盘" + e.abDrives) +
            "；AppPath=" + RegValue("AppPath") + " CDDrive=" + RegValue("CDDrive");
    }

    // 这些值会被 1999 年的代码直接当除数：0、越界、自相矛盾都算坏值
    static List<string> BadReadings(EnvRead e)
    {
        List<string> bad = new List<string>();
        if (e.refresh < 1 || e.refresh > 1000) bad.Add("刷新率=" + e.refresh);
        if (e.cx <= 0 || e.cy <= 0) bad.Add("桌面尺寸=" + e.cx + "×" + e.cy);
        if (!e.diskOk) bad.Add("游戏盘读数失败");
        else
        {
            if (e.bps == 0 || e.spc == 0) bad.Add("每扇区字节/每簇扇区=" + e.bps + "/" + e.spc);
            ulong bytesPerCluster = (ulong)e.bps * e.spc;
            if (bytesPerCluster > 0 && (bytesPerCluster < 512 || bytesPerCluster > 2097152))
                bad.Add("每簇字节=" + bytesPerCluster + "（" + e.driveType + "）");
            if (e.totalC > 0 && e.freeC > e.totalC) bad.Add("可用簇 " + e.freeC + " > 总簇 " + e.totalC);
        }
        return bad;
    }

    // /readings：只采集读数写日志，不启动游戏（给远程排障用）
    static int DumpReadings()
    {
        EnvRead e = ReadEnvironment();
        List<string> bad = BadReadings(e);
        string line = ReadingsLine(e);
        string verdict = "读数校验：" + (bad.Count == 0 ? "全部合理" : "可疑 —— " + string.Join("、", bad.ToArray()));
        Log(line);
        Log(verdict);
        Console.WriteLine(line);
        Console.WriteLine(verdict);
        string[] regLines = RegistrySnapshotLines();
        LogRegistrySnapshot();
        foreach (string rl in regLines) Console.WriteLine(rl);
        return 0;
    }

    bool SnapshotEnvironment(out string reason)
    {
        reason = "";
        EnvRead e = ReadEnvironment();
        System.Collections.Generic.List<string> bad = BadReadings(e);
        Log(ReadingsLine(e));
        Log("读数校验：" + (bad.Count == 0 ? "全部合理" : "可疑 —— " + string.Join("、", bad.ToArray())));
        if (bad.Count == 0) return false;
        reason = string.Join("、", bad.ToArray());
        return true;
    }

    // IPX 联机（DirectPlay）要把 dpwsockx.dll 登记到 HKLM。
    // 按 dplay-setup 源码里的键值自己写（不跑它的窗口程序），无窗口、无点击；没权限只记日志。
    // 崩了以后自动把 Windows 的崩溃报告（含出错模块 + 偏移）收进游戏目录，不用远程连机器。
    // WER 报告是系统自动生成的：不动任何设置，也不需要谁去点。
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
            for (int wait = 0; wait < 8 && best == null; wait++)
            {
                if (wait > 0) Thread.Sleep(1000);
                best = FindWerFolder(roots, exeName, out bestTime);
            }
            if (best == null) { Log("崩溃报告：WER 里没有本游戏的记录"); return; }
            string[] wers = Directory.GetFiles(best, "*.wer");
            if (wers.Length == 0) { Log("崩溃报告：" + best + " 里没有 .wer"); return; }
            string dstFile = Path.Combine(AppDir(), "崩溃转储", Path.GetFileName(best) + ".wer");
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
            File.Copy(wers[0], dstFile, true);
            Log("崩溃捕获：" + ParseWer(wers[0]) + "；报告已存 " + dstFile);
        }
        catch (Exception ex) { Log("读崩溃报告失败：" + ex.Message); }
    }

    // .wer 是 UTF-16，Sig[n].Name / Sig[n].Value 成对排；名字随系统语言（中文叫「异常代码」，英文叫 "Exception Code"），
    // 所以按名字认，别按序号猜。异常码 + 偏移 + 故障模块就是定位要的那三样。
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
        if (code.Length == 0 && offset.Length == 0 && module.Length == 0) return "报告里没认出异常码/模块（原文见报告）";
        return "异常码 " + (code.Length == 0 ? "没读到" : "0x" + code) +
               "，出错地址 " + (offset.Length == 0 ? "没读到" : "+0x" + offset) +
               "，故障模块 " + (module.Length == 0 ? "没读到" : module);
    }

    static string SigKind(string name)
    {
        if (name.Length == 0) return "";
        if (name.IndexOf("异常代码") >= 0 || name.IndexOf("Exception Code") >= 0) return "code";
        if (name.IndexOf("异常偏移") >= 0 || name.IndexOf("Exception Offset") >= 0) return "offset";
        // 中文「故障模块名称」/ 英文 "Fault Module Name"（别把「故障模块版本 / Fault Module Version」也认进来）
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

    void EnsureDirectPlayIpx()
    {
        try
        {
            const string spKey = @"SOFTWARE\Microsoft\DirectPlay\Service Providers\{685BC400-9D2C-11cf-A9CD-00AA006886E3}";
            const string svKey = @"SOFTWARE\Microsoft\DirectPlay\Services\{5146ab8cb6b1ce11920c00aa006c4972}";
            if (IpxRegistered(spKey)) { Log("DirectPlay IPX：已登记"); return; }
            if (!File.Exists(Path.Combine(AppDir(), "dpwsockx.dll")))
            {
                Log("DirectPlay IPX：缺 dpwsockx.dll，跳过");
                return;
            }
            using (Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(
                       Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32))
            using (Microsoft.Win32.RegistryKey k = hklm.CreateSubKey(spKey))
            {
                k.SetValue("DescriptionA", "IPX Connection For DirectPlay", Microsoft.Win32.RegistryValueKind.String);
                k.SetValue("DescriptionW", "IPX Connection For DirectPlay", Microsoft.Win32.RegistryValueKind.String);
                k.SetValue("dwReserved1", 50, Microsoft.Win32.RegistryValueKind.DWord);
                k.SetValue("dwReserved2", 0, Microsoft.Win32.RegistryValueKind.DWord);
                k.SetValue("Guid", "{685BC400-9D2C-11cf-A9CD-00AA006886E3}", Microsoft.Win32.RegistryValueKind.String);
                k.SetValue("Path", "dpwsockx.dll", Microsoft.Win32.RegistryValueKind.String);
            }
            using (Microsoft.Win32.RegistryKey hklm2 = Microsoft.Win32.RegistryKey.OpenBaseKey(
                       Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32))
            using (Microsoft.Win32.RegistryKey k = hklm2.CreateSubKey(svKey))
            {
                k.SetValue("Description", "WinSock IPX Connection For DirectPlay", Microsoft.Win32.RegistryValueKind.String);
                k.SetValue("Path", "dpwsockx.dll", Microsoft.Win32.RegistryValueKind.ExpandString);
            }
            Log("DirectPlay IPX：" + (IpxRegistered(spKey) ? "已静默登记" : "登记没成功（不影响启动，多人房可能连不上）"));
        }
        catch (Exception ex) { Log("DirectPlay IPX 登记异常：" + ex.Message); }
    }

    // 多网卡（局域网 + Radmin/虚拟网）都在 IPX 网络号 00:00:00:01 下会让游戏初始化失败。
    // 这里自动挑一块物理网卡写进 HKCU\Software\IPXWrapper\primary（写当前用户那一份，不动系统）。
    void EnsureIpxPrimaryInterface()
    {
        try
        {
            string[] skip = { "radmin", "virtual", "vmware", "virtualbox", "hyper-v", "vethernet",
                              "tap-", "loopback", "wsl", "bluetooth", "wan miniport" };
            System.Net.NetworkInformation.NetworkInterface best = null;
            int bestScore = -1;
            foreach (System.Net.NetworkInformation.NetworkInterface ni in
                     System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                string desc = (ni.Description ?? "").ToLowerInvariant();
                bool virt = false;
                foreach (string s in skip) if (desc.IndexOf(s, StringComparison.Ordinal) >= 0) { virt = true; break; }
                if (virt) continue;
                string ip = null;
                foreach (System.Net.NetworkInformation.UnicastIPAddressInformation u in
                         ni.GetIPProperties().UnicastAddresses)
                {
                    if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    { ip = u.Address.ToString(); break; }
                }
                if (ip == null) continue;
                int score = ip.StartsWith("192.168.") ? 3 : ip.StartsWith("10.") ? 2 : ip.StartsWith("172.") ? 1 : 0;
                if (score > bestScore) { bestScore = score; best = ni; }
            }
            if (best == null) { Log("IPX 网卡：没有可用物理网卡，保持默认"); return; }
            byte[] b = best.GetPhysicalAddress().GetAddressBytes();
            if (b.Length < 6) { Log("IPX 网卡：读不到 MAC，保持默认"); return; }
            string mac = "";
            for (int i = 0; i < 6; i++) mac += (i > 0 ? ":" : "") + b[i].ToString("X2");
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IPXWrapper"))
            {
                object cur = k.GetValue("primary");
                if (cur != null && string.Equals(Convert.ToString(cur), mac, StringComparison.OrdinalIgnoreCase))
                {
                    Log("IPX 网卡：" + best.Description + " " + mac + "（已是主网卡）");
                    return;
                }
                k.SetValue("primary", mac, Microsoft.Win32.RegistryValueKind.String);
                Log("IPX 网卡：" + best.Description + " " + mac + "（已写入 HKCU\\Software\\IPXWrapper）");
            }
        }
        catch (Exception ex) { Log("IPX 网卡选择失败：" + ex.Message); }
    }

    static bool IpxRegistered(string spKey)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(
                       Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32))
            using (Microsoft.Win32.RegistryKey k = hklm.OpenSubKey(spKey))
            {
                object v = k == null ? null : k.GetValue("Path");
                return v != null && string.Equals(Convert.ToString(v), "dpwsockx.dll", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
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
    // 1999 年的代码把刷新率当除数，虚拟显示器/串流驱动又常给它 0。
    // 不去猜、也不依赖读数：启动前直接把桌面设成 60Hz，游戏自己读到的就是 60；退出后还原。
    DEVMODE? Apply60Hz()
    {
        try
        {
            DEVMODE cur = new DEVMODE();
            cur.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettings(null, -1, ref cur)) { Log("取当前显示模式失败，跳过 60Hz 设置"); return null; }
            // 只改刷新率这一项，分辨率沿用当前模式；所有机器走同一条路，不做“先探测再决定”
            DEVMODE want = cur;
            want.dmDisplayFrequency = 60;
            want.dmFields = 0x400000;                       // DM_DISPLAYFREQUENCY
            int rc = ChangeDisplaySettings(ref want, 0);
            Log("启动前设 60Hz：" + (rc == 0 ? "成功" : "失败 rc=" + rc + "，按原样启动") +
                "（当前模式 " + cur.dmPelsWidth + "×" + cur.dmPelsHeight + "）");
            if (rc != 0) return null;

            IntPtr hdc = GetDC(IntPtr.Zero);
            try { Log("设完复查刷新率=" + GetDeviceCaps(hdc, VREFRESH)); } finally { ReleaseDC(IntPtr.Zero, hdc); }
            return cur;
        }
        catch (Exception ex) { Log("设 60Hz 失败：" + ex.Message); return null; }
    }

    void RestoreDisplay(DEVMODE? before)
    {
        if (before == null) return;
        try
        {
            DEVMODE dm = before.Value;
            dm.dmFields = 0x400000;
            int rc = ChangeDisplaySettings(ref dm, 0);
            Log("退出后还原显示模式：" + (rc == 0 ? "成功" : "失败 rc=" + rc));
        }
        catch (Exception ex) { Log("还原显示模式失败：" + ex.Message); }
    }

    // 玩家只面对一个界面：配置缺了就自己找一份能用的，绝不叫他去点别的 bat
    string FallbackPreset(int which, string wanted)
    {
        string[] tries =
        {
            Path.Combine(AppDir(), "_presets", "ddraw.ini"),
            Path.Combine(AppDir(), "ddraw.ini"),
            Path.Combine(AppDir(), "_cnc-ddraw", "ddraw.ini")
        };
        foreach (string c in tries)
        {
            if (File.Exists(c) && !string.Equals(c, wanted, StringComparison.OrdinalIgnoreCase))
            {
                Log("显示预设缺失（" + Path.GetFileName(wanted) + "），自动改用 " + c);
                return c;
            }
        }
        return wanted;
    }

    bool ApplyLayer(int which, out string fail)
    {
        fail = "";
        string tag = ModeLayer[which];
        string dir = LayerDirOf(tag);
        string dllSrc = Path.Combine(AppDir(), dir, "ddraw.dll");
        string confSrc = Path.Combine(AppDir(), "_presets", ModePresets[which]);
        if (!File.Exists(dllSrc)) { fail = dir + "\\ddraw.dll 缺失"; return false; }
        if (!File.Exists(confSrc)) confSrc = FallbackPreset(which, confSrc);   // 自己找一份能用的
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

    // 收尾回到默认档（第 6 档 dgVoodoo2）；缺文件时退回 cnc-ddraw GDI
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
        if (!File.Exists(layerSrc)) { EnsureLayer(out message); }
        if (!File.Exists(presetPath)) presetPath = FallbackPreset(mode, presetPath);
        if (!File.Exists(presetPath) || !File.Exists(layerSrc))
        {
            Log("缺少显示预设或兼容层（自修也失败）：" + presetPath + " / " + layerSrc);
            status = "显示文件不完整，请把运行日志发给我们";
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
                EnsureRegistry();
                EnsureDirectPlayIpx();
                EnsureIpxPrimaryInterface();
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
                    // 把所有可能变成 0 的环境值先钉死：启动前无条件按 60Hz 设置一次显示模式，退出后还原。
                    // 1999 年的代码会把刷新率直接当除数，遇到虚拟显示器/串流驱动报 0 就 0xC0000094。
                    DEVMODE? beforeMode = Apply60Hz();
                    DateTime start = DateTime.Now;
                    int code;
                    try { code = RunGameWithAffinity(exePath, gameArgs, launchProcess); }
                    finally { RestoreDisplay(beforeMode); }
                    int secs = (int)(DateTime.Now - start).TotalSeconds;
                    bool bad = (uint)code >= 0xC0000000;
                    if (bad) CaptureWerReport(gameExe);
                    Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                        (bad ? "（系统级异常）" : ""));
                    Log("注册表复查：" + RegistrySummary());
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

    // ---------- 注册表：启动前原始快照 + 缺啥补啥（2026-11-07 客户机 0xC0000094 追查） ----------
    // 11.reg 的键名是 Magic® III（0xAE + 空格），setup.reg / 旧启动器是 Magic?III：两套都写，别猜游戏读哪个。
    // 只写 32 位视图（游戏是 32 位进程）；64 位视图只读、进日志当证据（老 reg 双击常落到那边去）。
    // 幂等：AppPath/CDDrive 与实际目录不同才改；其余值只在缺失时补默认值，绝不覆盖玩家在游戏里改过的设置。
    const string RegKeyQ = @"SOFTWARE\New World Computing\Heroes of Might and Magic?III\1.0";
    static readonly string RegKeyR =
        @"SOFTWARE\New World Computing\Heroes of Might and Magic" + "\u00AE" + @" III\1.0";
    static readonly string[] RegKeyPaths = { RegKeyQ, RegKeyR };

    // 名字|类型(s字符串/d数字)|默认值，分号分隔；AppPath/CDDrive 运行时换成实际目录和盘符
    // 默认值 = 本机实测能跑通的全套（与 tools\注册表修复.bat 完全一致，Test 三项=0）。
    // 2026-11-07 客户机实证：bat 写入这套值后游戏不再 0xC0000094，故启动器默认值向 bat 看齐。
    const string RegDefaults =
        "AppPath|s|.;CDDrive|s|.;Show Intro|d|1;Music Volume|d|5;Sound Volume|d|5;" +
        "Last Music Volume|d|5;Last Sound Volume|d|5;Walk Speed|d|2;Computer Walk Speed|d|3;" +
        "Show Route|d|1;Move Reminder|d|1;Quick Combat|d|0;Video Subtitles|d|0;Town Outlines|d|1;" +
        "Animate SpellBook|d|1;Window Scroll Speed|d|1;Bink Video|d|0;Blackout Computer|d|0;First Time|d|0;" +
        "Test Decomp|d|0;Test Read|d|0;Test Blit|d|0;Unique System ID|s|I29;Network Default Name|s|Playerer;" +
        "Autosave|d|1;Show Combat Grid|d|0;Show Combat Mouse Hex|d|0;Combat Shade Level|d|0;" +
        "Combat Army Info Level|d|0;Combat Auto Creatures|d|1;Combat Auto Spells|d|1;Combat Catapult|d|1;" +
        "Combat Ballista|d|1;Combat First Aid Tent|d|1;Combat Speed|d|0;Main Game Show Menu|d|1;" +
        "Main Game X|d|0;Main Game Y|d|0;Main Game Full Screen|d|1";

    struct RegItem { public string Name; public bool IsString; public string Def; }

    static RegItem[] RegItems()
    {
        string[] parts = RegDefaults.Split(';');
        RegItem[] r = new RegItem[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string[] f = parts[i].Split('|');
            r[i].Name = f[0]; r[i].IsString = f[1] == "s"; r[i].Def = f[2];
        }
        return r;
    }

    static string RegTag(string keyPath)
    {
        return keyPath.IndexOf("\u00AE") >= 0 ? "\u00AEIII" : "?III";
    }

    static string ValText(object v) { return v == null ? "（无）" : Convert.ToString(v); }

    // 只读：把「客户机原本缺什么」原样写进日志，必须在补齐之前写
    static string[] RegistrySnapshotLines()
    {
        List<string> lines = new List<string>();
        string dir = AppDir().TrimEnd('\\') + "\\";
        string drive = "";
        try { drive = Path.GetPathRoot(dir).TrimEnd('\\'); } catch { }
        lines.Add("注册表快照：游戏目录 " + dir + " 盘符 " + drive +
                  "；应有 " + RegItems().Length + " 项（对照 11.reg / setup.reg）");
        RegItem[] items = RegItems();
        foreach (RegistryView view in new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            foreach (string keyPath in RegKeyPaths)
            {
                string tag = (view == RegistryView.Registry32 ? "32位" : "64位") + " " + RegTag(keyPath);
                try
                {
                    using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey k = b.OpenSubKey(keyPath))
                    {
                        if (k == null)
                        {
                            lines.Add("注册表键：" + tag + " 不存在（缺 " + items.Length + " 项）");
                            continue;
                        }
                        List<string> miss = new List<string>();
                        foreach (RegItem it in items) if (k.GetValue(it.Name, null) == null) miss.Add(it.Name);
                        lines.Add("注册表键：" + tag + " 存在/" + k.ValueCount + "项" +
                            " AppPath=" + ValText(k.GetValue("AppPath")) +
                            " CDDrive=" + ValText(k.GetValue("CDDrive")) +
                            " 缺" + miss.Count + "项" +
                            (miss.Count == 0 ? "；已齐" : "（" + string.Join("、", miss.ToArray()) + "）"));
                    }
                }
                catch (Exception ex) { lines.Add("注册表键：" + tag + " 读失败：" + ex.Message); }
            }
        }
        return lines.ToArray();
    }

    static bool regSnapshotLogged = false;
    static void LogRegistrySnapshot()
    {
        if (regSnapshotLogged) return;
        regSnapshotLogged = true;
        foreach (string s2 in RegistrySnapshotLines()) Log(s2);
    }

    // 游戏退出后再看一眼：哪个键的值数变了，就说明游戏实际读的是那一个
    static string RegistrySummary()
    {
        string s2 = "";
        foreach (string keyPath in RegKeyPaths)
        {
            string tag = RegTag(keyPath);
            if (s2.Length > 0) s2 += "；";
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey k = b.OpenSubKey(keyPath))
                    s2 += tag + "=" + (k == null ? "不存在" : k.ValueCount + "项");
            }
            catch (Exception ex) { s2 += tag + "=读失败(" + ex.Message + ")"; }
        }
        return s2;
    }

    void EnsureRegistry()
    {
        string dir = AppDir().TrimEnd('\\') + "\\";
        string drive = "";
        try { drive = Path.GetPathRoot(dir).TrimEnd('\\'); } catch { }
        RegItem[] items = RegItems();
        foreach (string keyPath in RegKeyPaths)
        {
            string tag = "32位 " + RegTag(keyPath);
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey k = b.CreateSubKey(keyPath))
                {
                    if (k == null) { Log("注册表补齐：" + tag + " 打不开（权限不足？），跳过"); continue; }
                    List<string> added = new List<string>();
                    int fixedN = 0;
                    foreach (RegItem it in items)
                    {
                        object cur = k.GetValue(it.Name, null);
                        string want = it.Name == "AppPath" ? dir : (it.Name == "CDDrive" ? drive : it.Def);
                        if (cur == null)
                        {
                            added.Add(it.Name);
                            if (it.IsString) k.SetValue(it.Name, want, RegistryValueKind.String);
                            else k.SetValue(it.Name, Convert.ToInt32(want), RegistryValueKind.DWord);
                        }
                        else if ((it.Name == "AppPath" || it.Name == "CDDrive") &&
                                 !string.Equals(Convert.ToString(cur), want, StringComparison.OrdinalIgnoreCase))
                        {
                            k.SetValue(it.Name, want, RegistryValueKind.String);
                            fixedN++;
                        }
                    }
                    Log("注册表补齐：" + tag + " 补" + added.Count + "项/校正" + fixedN + "项（应有" +
                        items.Length + "项）" +
                        (added.Count == 0 ? "；原本已齐" : "；补的是：" + string.Join("、", added.ToArray())));
                }
            }
            catch (Exception ex) { Log("注册表补齐：" + tag + " 写不进（继续启动，不阻塞）：" + ex.Message); }
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
    // VREFRESH = 116（不是 0；0 是 DRIVERVERSION，真实驱动返回 0x4000=16384，害我们一直把刷新率记成 16384）
    const int VREFRESH = 116, HORZRES = 8, VERTRES = 10, LOGPIXELSX = 88, LOGPIXELSY = 90;
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    // DEVMODEW 的规范布局（sizeof = 220）：dmPosition 是 POINTL(8) + dmDisplayOrientation(4)，
    // 之后还有 dmDisplayFixedOutput(4)；少一个字段整体错位，读出来的宽高/刷新率全是隔壁字段的值
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
    static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
    // 注意出参顺序：每簇扇数、每扇区字节、可用簇、总簇（MSDN 就是这个顺序，写反会把日志标错）
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool GetDiskFreeSpaceA(string root, out uint sectorsPerCluster,
        out uint bytesPerSector, out uint freeClusters, out uint totalClusters);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern uint GetDriveTypeW(string root);
    static string DriveTypeName(uint t)
    {
        // GetDriveType：0 未知、1 路径没有根目录、2 可移动、3 本地硬盘、4 网络、5 光盘、6 内存盘
        switch (t)
        {
            case 0: return "未知"; case 1: return "路径无效"; case 2: return "可移动盘";
            case 3: return "本地硬盘"; case 4: return "网络盘"; case 5: return "光盘";
            case 6: return "内存盘";
            default: return "未知(" + t + ")";
        }
    }

    // 直接启动游戏并等它退出：不做「包装进程 / 再去找实际进程」那一套，少一层就少一处出错
    int RunGameWithAffinity(string exePath, string launchArgs, string launchProcess)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        string cmd = "\"" + exePath + "\"" + (launchArgs.Length > 0 ? " " + launchArgs : "");
        bool started = CreateProcess(exePath, cmd, IntPtr.Zero, IntPtr.Zero, false,
            0x4, IntPtr.Zero, AppDir(), ref si, out pi);      // CREATE_SUSPENDED
        if (!started)
        {
            Log("CreateProcess 失败（err=" + Marshal.GetLastWin32Error() + "），改用 Process.Start");
            ProcessStartInfo psi = new ProcessStartInfo(exePath, launchArgs);
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = false;
            using (Process game = Process.Start(psi))
            {
                game.WaitForExit();
                return game.ExitCode;
            }
        }
        if (affinity > 0)
        {
            bool ok = SetProcessAffinityMask(pi.hProcess, (UIntPtr)(ulong)affinity);
            Log("SetProcessAffinityMask 0x" + affinity.ToString("X") + " = " + ok);
        }
        ResumeThread(pi.hThread);
        CloseHandle(pi.hThread);
        Log("已启动游戏 " + pi.dwProcessId);
        WaitForSingleObject(pi.hProcess, 0xFFFFFFFF);
        uint code;
        bool got = GetExitCodeProcess(pi.hProcess, out code);
        CloseHandle(pi.hProcess);
        Log("游戏退出码：" + (got ? ((int)code).ToString() : "取不到"));
        return got ? (int)code : -1;
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
                case "affinity":
                    long a;
                    if (long.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out a)) affinity = a;
                    else if (long.TryParse(val, out a)) affinity = a;
                    break;
                case "layerdll": layerDll = val; break;
                case "layerbackupdir": layerBackupDir = val; break;
                case "intro": intro = val; break;
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
