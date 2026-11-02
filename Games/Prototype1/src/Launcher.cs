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

// 虐杀原形 1 / 2 启动器（game_starter.exe），界面规范见仓库
// .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、三栏 900×620、红渐变、正常 TextRenderer。
//
// 启动方式与游戏目录里的 go.bat 保持一致：P1 先跑 init.exe 再启动 prototypef.exe；
// P2 直接启动 prototype2.exe；两者都不加任何分辨率/窗口参数，并按 go.bat 的 0x3ff 亲和性启动。
// 平台托管：游戏是 game_starter.exe 的直接子进程，启动器陪跑到游戏退出并原样回传退出码。

class Launcher : Form
{
    // ---------- 设计令牌 ----------
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

    // ---------- launcher.ini 可覆盖 ----------
    string gameTitle = "虐杀原形 Prototype";
    string gameExe = "prototypef.exe";
    string gameProcess = "prototypef";
    string gameIconExe = "prototypef.exe";
    string initExe = "init.exe";
    bool runInit = true;
    long affinity = 0x3ff;
    string layerDll = "d3d9.dll";
    string layerBackupDir = "_dxvk";
    string intro =
        "《虐杀原形》是 2009 年的\n" +
        "开放世界动作游戏：\n" +
        "主角在曼哈顿追查病毒，\n" +
        "能跑墙、滑翔、变形、吸收路人。\n" +
        "\n" +
        "这份是简体中文硬盘版，\n" +
        "直接进游戏即可。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给管理员";
    string entryGuide =
        "启动后先等加载（第一次较慢）\n" +
        "过场动画按 Esc / Enter 跳过\n" +
        "分辨率、画质在游戏「选项」里改";
    string tips =
        "# 提示\n" +
        "启动器按游戏原本方式启动，不加参数\n" +
        "按键以游戏内「选项 → 控制」为准";
    string centerNote =
        "启动器按游戏原本的方式开游戏，\n" +
        "不改分辨率、不覆盖游戏设置。\n" +
        "\n" +
        "第一次进游戏先等加载；\n" +
        "分辨率、画质请进游戏「选项」里改。";

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
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
    Rectangle rcStart, rcFolder, rcExit;

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
        rcStart = new Rectangle(inner, btnY, innerW, S(44));
        rcFolder = new Rectangle(inner, btnY + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y, innerW - (rcFolder.Right + S(8) - inner), S(34));
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
        TextRenderer.DrawText(g, "Radical Entertainment · 简体中文硬盘版",
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
        int x = colCenter.X + S(16), y = colCenter.Y + S(14);
        TextRenderer.DrawText(g, "准备开玩", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(32);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy && layerOk,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true, hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
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
            new[] { Item("移动", "W", "A", "S", "D") },
            new[] { PlainItem("视角", "鼠标") },
            new[] { PlainItem("攻击", "鼠标左键") },
            new[] { PlainItem("抓取", "鼠标右键") },
            new[] { Item("跳跃", "空格") },
            new[] { Item("冲刺", "Shift") },
            new[] { Item("互动", "E") },
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
        return -1;
    }

    Rectangle ButtonsRect
    {
        get { return Rectangle.Union(Rectangle.Union(rcStart, rcFolder), rcExit); }
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
        int[] order = busy ? new int[] { 1, 2 } : new int[] { 0, 1, 2 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
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
        Log("点「启动游戏」：" + gameTitle);
        Environment.SetEnvironmentVariable("DXVK_LOG_LEVEL", "none");   // 和 go.bat 一致
        SetBusy(true, "启动中…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                SetStatus("游戏运行中…", Ok);
                PrepareGame();
                int code = RunGameWithAffinity(exePath);
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
            }
            finally { SetBusy(false, null); }
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

    int RunGameWithAffinity(string exePath)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        PROCESS_INFORMATION pi;
        bool started = CreateProcess(exePath, "\"" + exePath + "\"", IntPtr.Zero, IntPtr.Zero,
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
            Log("游戏进程 " + pi.dwProcessId + "（亲和性 0x" + affinity.ToString("X") + "）");
            WaitForSingleObject(pi.hProcess, 0xFFFFFFFF);   // INFINITE
            uint code;
            bool got = GetExitCodeProcess(pi.hProcess, out code);
            CloseHandle(pi.hProcess);
            Log("GetExitCodeProcess = " + got + "，退出码 " + code);
            return (int)code;
        }
        Log("CreateProcess 失败，退回普通启动：错误 " + Marshal.GetLastWin32Error());
        ProcessStartInfo psi = new ProcessStartInfo(exePath);
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
            string exe = Path.Combine(AppDir(), string.IsNullOrEmpty(gameIconExe) ? gameExe : gameIconExe);
            if (File.Exists(exe))
            {
                Icon ic = Icon.ExtractAssociatedIcon(exe);
                if (ic != null) { Icon = ic; return; }
            }
        }
        catch { }
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
                case "gameprocess": gameProcess = val; break;
                case "gameiconexe": gameIconExe = val; break;
                case "initexe": initExe = val; break;
                case "runinit": runInit = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "affinity":
                    long a;
                    if (long.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out a)) affinity = a;
                    else if (long.TryParse(val, out a)) affinity = a;
                    break;
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
