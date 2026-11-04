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

// 战神III 重制版（SpartanPort 移植版）启动器（game_starter.exe）。
// 界面规范见仓库 .qoder/skills/legacy-launcher-gui/SKILL.md。
// 核心职责：替代原版「游戏设置.exe」——写 spartanport.ini / settings.json，
// 按 PC 习惯重排键位，再以 SpartanPort.exe --run + SPARTAN_* 环境变量直接拉起游戏。

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
    string gameTitle = "战神III 重制版";
    string gameExe = "SpartanPort.exe";
    string gameArgs = "--run";
    string intro =
        "《战神III 重制版》是圣莫尼卡的动作冒险经典，\n" +
        "奎托斯踏上天界与冥界，向众神展开最后的复仇。\n" +
        "连招、QTE 与巨怪 Boss 战是它的招牌。\n" +
        "\n" +
        "这份是免安装移植版。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「修复画面」，仍不行就点「打开运行日志」，把内容发给管理员";
    string centerNote = "游戏会自动检测画面。";

    static readonly string[] LaunchModeNames =
    {
        "游戏自动检测（推荐）",
        "画面异常时（固定桌面分辨率）"
    };
    int launchMode;

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, finishing;
    string status = "已就绪";
    Color statusColor = Ok;
    LegendItem[] legend = new LegendItem[0];
    volatile int exitCode;
    NotifyIcon tray;
    bool autoStart;
    int desktopW, desktopH;
    string desktopSource = "";

    // ---------- 字体/布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcLabelMode, rcComboMode, rcStart, rcFix, rcLog, rcExit;
    SelectPanel selMode;
    ContextMenuStrip modeMenu;

    class LegendItem
    {
        public string Caption;
        public string[] Caps;
        public string Glyph;   // square / circle / cross / triangle：游戏里显示的手柄符号
    }

    [STAThread]
    static int Main(string[] args)
    {
        Spartan.EnableDpiAwareness();

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
            Spartan.GetDesktop(out w, out h, out src);
            string msg;
            bool ok = Spartan.ApplySafeDisplay(AppDir(), w, h, out msg);
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
        ClientSize = new Size(900, 620);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using (Graphics g = CreateGraphics()) { dpi = g.DpiX / 96f; }
        MakeFonts();
        ClientSize = new Size(S(900), S(620));
        RebuildLegend();
        if (selMode == null)
        {
            selMode = MakeSelect(LaunchModeNames, launchMode, delegate { ShowModeMenu(); });
            selMode.TabIndex = 0;
        }
        LayoutRects();
        Spartan.GetDesktop(out desktopW, out desktopH, out desktopSource);
        string msg = Spartan.Describe(desktopW, desktopH);
        status = "已就绪";
        statusColor = Ok;
        LoadGameIcon();
        Log("启动 " + gameTitle + "｜桌面 " + msg + "（" + desktopSource + "）｜DPI " + dpi.ToString("0.##") +
            "｜按键 " + Spartan.KeyMapOf());
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
        int c1 = S(252), c2 = S(276), c3 = S(300);
        int top = S(88), bottom = S(552);
        colLeft = new Rectangle(pad, top, c1, bottom - top);
        colCenter = new Rectangle(pad + c1 + gut, top, c2, bottom - top);
        colRight = new Rectangle(pad + c1 + gut + c2 + gut, top, c3, bottom - top);

        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        int noteBottom = colCenter.Y + S(46) + MeasureCenterNoteHeight(innerW);
        rcLabelMode = new Rectangle(inner, noteBottom + S(14), innerW, S(22));
        rcComboMode = new Rectangle(inner, rcLabelMode.Bottom + S(2), innerW, S(30));
        int startY = rcComboMode.Bottom + S(18);
        rcStart = new Rectangle(inner, startY, innerW, S(44));
        // 三个按钮不等宽：「打开运行日志」六个字必须完整显示，退出两个字可以窄
        int wFix = S(72), wLog = S(124);
        rcFix = new Rectangle(inner, rcStart.Bottom + S(10), wFix, S(34));
        rcLog = new Rectangle(rcFix.Right + S(8), rcFix.Y, wLog, S(34));
        rcExit = new Rectangle(rcLog.Right + S(8), rcLog.Y, innerW - (rcLog.Right + S(8) - inner), S(34));
        PlaceSelect(selMode, rcComboMode);
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
        int footY = S(560);
        Rectangle foot = new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY);
        DrawBand(g, foot);

        DrawHeader(g);
        DrawLeft(g);
        DrawCenter(g);
        DrawRight(g);
        DrawFooter(g);
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
        TextRenderer.DrawText(g, "God of War III Remastered · Santa Monica Studio",
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
        TextRenderer.DrawText(g, "启动说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(32);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);
        TextRenderer.DrawText(g, "启动选项", fHead,
            new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFix, "修复画面", false, !busy,
            hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcLog, "打开运行日志", false, true,
            hover == 2 || focus == 2, focus == 2);
        DrawButton(g, rcExit, "退出", false, true,
            hover == 3 || focus == 3, focus == 3);
        if (focus == 4 && selMode != null) FocusRing(g, selMode.Bounds);
    }

    static bool InViewport(int y, int h, int top, int bottom) { return y >= top && y + h <= bottom; }

    void DrawRight(Graphics g)
    {
        Card(g, colRight);
        Region oldClip = g.Clip;
        g.SetClip(colRight, CombineMode.Replace);
        int top = colRight.Y + S(6);
        int bottom = colRight.Bottom - S(6);
        int startY = colRight.Y + S(14) - rightScroll;
        int endY = DrawRightContent(g, startY, top, bottom);
        int contentH = endY - startY;
        int viewport = colRight.Height - S(14) - S(12);
        rightScrollMax = Math.Max(0, contentH - viewport);
        int clamped = Math.Max(0, Math.Min(rightScroll, rightScrollMax));
        if (clamped != rightScroll) { rightScroll = clamped; Invalidate(colRight); }
        g.Clip = oldClip;
        DrawRightScroll(g, viewport, contentH);
    }

    int DrawRightContent(Graphics g, int y, int top, int bottom)
    {
        int x = colRight.X + S(16);
        int h = TextRenderer.MeasureText(g, "国", fHead).Height;
        if (InViewport(y, h, top, bottom))
            TextRenderer.DrawText(g, "操作说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(28);
        int textH = TextRenderer.MeasureText(g, "国", fBody).Height;
        if (InViewport(y, textH, top, bottom))
        {
            TextRenderer.DrawText(g, "手柄", fBody, new Point(x, y), InkDim, Color.Transparent);
            TextRenderer.DrawText(g, "说明", fBody, new Point(x + S(54), y), InkDim, Color.Transparent);
            TextRenderer.DrawText(g, "键盘按键", fBody, new Point(x + S(150), y), InkDim, Color.Transparent);
        }
        y += S(24);
        return DrawLegendTable(g, legend, x, y, top, bottom);
    }

    int DrawLegendTable(Graphics g, LegendItem[] rows, int x, int y, int top, int bottom)
    {
        int capH = S(21);
        int descX = x + S(54);
        int keyX = x + S(150);
        int textH = TextRenderer.MeasureText(g, "国", fBody).Height;
        foreach (LegendItem item in rows)
        {
            if (InViewport(y, capH, top, bottom))
            {
                int ty = y + (capH - textH) / 2;
                if (item.Glyph != null)
                    PsGlyph(g, item.Glyph, x, y, GlyphWidth(g, item.Glyph), capH);
                TextRenderer.DrawText(g, item.Caption, fBody, new Point(descX, ty), Ink, Color.Transparent);
                int cx = keyX;
                foreach (string cap in item.Caps)
                {
                    int cw = CapWidth(g, cap);
                    Keycap(g, cap, cx, y, cw, capH);
                    cx += cw + S(3);
                }
            }
            y += capH + S(2);
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

    // ---------- 键帽表（动作含义来自移植器「控制」页 + README，纯键盘一套） ----------
    void RebuildLegend()
    {
        legend = new LegendItem[]
        {
            G("左摇杆", "移动", "W", "A", "S", "D"),
            G("右摇杆", "前后左右翻滚", "↑", "↓", "←", "→"),
            G("square", "轻攻击", "J"),
            G("triangle", "重攻击", "K"),
            G("cross", "跳跃", "空格"),
            G("circle", "抓取 / 确认", "L"),
            G("L1", "防御 / 弹反", "Q"),
            G("R1", "投链刃", "E"),
            G("L2", "瞄准", "R"),
            G("R2", "斯巴达之怒", "T"),
            G("方向键", "切换武器", "1", "2", "3", "4"),
            G("右摇杆", "闪避", "左Shift"),
            G("L1+○", "快捷抓取", "C"),
            G("触摸板", "暂停", "Esc"),
            G("移植器", "设置菜单", "Insert")
        };
    }

    static LegendItem Item(string caption, params string[] caps)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption;
        it.Caps = caps;
        return it;
    }

    static LegendItem G(string glyph, string caption, params string[] caps)
    {
        LegendItem it = Item(caption, caps);
        it.Glyph = glyph;
        return it;
    }

    int GlyphSize { get { return S(26); } }

    int GlyphWidth(Graphics g, string sym)
    {
        if (IsFaceGlyph(sym)) return S(26);
        if (IsLabelGlyph(sym)) return S(32);
        return Math.Max(S(34), TextRenderer.MeasureText(g, sym, fCap).Width + S(10));
    }

    static bool IsFaceGlyph(string sym)
    {
        return sym == "square" || sym == "circle" || sym == "cross" || sym == "triangle";
    }

    static bool IsLabelGlyph(string sym)
    {
        return sym == "L1" || sym == "L2" || sym == "R1" || sym == "R2";
    }

    // 游戏画面上显示的手柄按键：□ △ ✕ ○ 用官方配色画符号，
    // L1/L2/R2、摇杆、方向键这类画成同款拟物小帽写名字，玩家照着画面找键即可。
    void PsGlyph(Graphics g, string sym, int x, int y, int w, int h)
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
        if (!IsFaceGlyph(sym))
        {
            string label = sym;
            Font f = fCap;
            if (TextRenderer.MeasureText(g, label, f).Width > w - S(6)) f = fCapZh;
            TextRenderer.DrawText(g, label, f,
                new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), Gold, Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }
        Color c;
        switch (sym)
        {
            case "cross": c = Color.FromArgb(0x7C, 0xC3, 0xEE); break;
            case "circle": c = Color.FromArgb(0xE8, 0x7B, 0x7B); break;
            case "square": c = Color.FromArgb(0xEA, 0x9A, 0xC4); break;
            default: c = Color.FromArgb(0x8F, 0xD9, 0xA6); break;
        }
        float cxp = r.X + r.Width / 2f, cyp = r.Y + r.Height / 2f;
        float s = Math.Min(r.Width, r.Height) * 0.30f;
        using (Pen pen = new Pen(c, Math.Max(1.4f, S(2) * 0.7f)))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            if (sym == "square")
                g.DrawRectangle(pen, cxp - s, cyp - s, s * 2f, s * 2f);
            else if (sym == "circle")
                g.DrawEllipse(pen, cxp - s, cyp - s, s * 2f, s * 2f);
            else if (sym == "cross")
            {
                g.DrawLine(pen, cxp - s, cyp - s, cxp + s, cyp + s);
                g.DrawLine(pen, cxp + s, cyp - s, cxp - s, cyp + s);
            }
            else
            {
                PointF[] tri = new PointF[]
                {
                    new PointF(cxp, cyp - s * 1.15f),
                    new PointF(cxp + s * 1.15f, cyp + s * 0.85f),
                    new PointF(cxp - s * 1.15f, cyp + s * 0.85f)
                };
                g.DrawPolygon(pen, tri);
            }
        }
    }

    // ---------- 输入 ----------
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (!busy && rcFix.Contains(pt)) return 1;
        if (rcLog.Contains(pt)) return 2;
        if (rcExit.Contains(pt)) return 3;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get { return Rectangle.Union(Rectangle.Union(Rectangle.Union(rcStart, rcFix), rcLog), rcExit); }
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
        int[] order = busy ? new int[] { 2, 3 } : new int[] { 4, 0, 1, 2, 3 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
        if (i == 4) { ShowModeMenu(); return; }
        if (i == 0) { StartGame(); return; }
        if (i == 1) { FixResolution(); return; }
        if (i == 2) { OpenLog(); return; }
        if (i == 3) { Close(); }
    }

    // ---------- 启动逻辑 ----------
    void StartGame()
    {
        if (busy) return;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe;
            statusColor = Warn;
            Invalidate();
            return;
        }

        Spartan.GetDesktop(out desktopW, out desktopH, out desktopSource);
        int mode = launchMode;
        SetBusy(true, "正在启动…");
        Log("点「启动游戏」：" + LaunchModeNames[mode]);
        Thread t = new Thread(delegate ()
        {
            try
            {
                string msg;
                if (!Spartan.ApplyKeyScheme(AppDir(), out msg))
                {
                    Log("按键设置失败：" + msg);
                    SetStatus("按键设置失败，请点「打开运行日志」", Warn);
                    if (autoStart) { exitCode = 1; FinishWith(1); }
                    return;
                }
                Log("按键：" + msg);
                if (mode == 1)
                {
                    if (!Spartan.ApplySafeDisplay(AppDir(), desktopW, desktopH, out msg))
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
                SetStatus("游戏运行中…", Ok);
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
                Spartan.GetDesktop(out desktopW, out desktopH, out desktopSource);
                string msg;
                bool ok = Spartan.ApplySafeDisplay(AppDir(), desktopW, desktopH, out msg);
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
        ProcessStartInfo psi = new ProcessStartInfo(exePath, gameArgs);
        psi.WorkingDirectory = AppDir();
        psi.UseShellExecute = false;
        Spartan.ApplyLaunchEnvironment(psi, AppDir());
        Log("传给游戏的按键映射：" + Spartan.KeyMapOf());
        using (Process game = Process.Start(psi))
        {
            Log("游戏进程 " + game.Id + "，父进程 " + Process.GetCurrentProcess().Id);
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
        if (fBrand != null) fBrand.Dispose();
        if (modeMenu != null) { modeMenu.Dispose(); modeMenu = null; }

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

    // ---------- 显示守卫 ----------
    static class Spartan
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

        // ---------- 键位表（动作名与默认值来自移植器「控制」页实测） ----------
        // 列：动作名 | 我们的按键（纯键盘） | 移植器默认
        // 只有与「移植器默认」不同的项才写进 settings.json，和原版启动器的行为一致
        static readonly string[,] Actions =
        {
            { "square", "J", "Mouse Left" },
            { "triangle", "K", "Mouse Right" },
            { "circle", "L", "Left Shift" },
            { "cross", "Space", "Space" },
            { "l1", "Q", "Q" },
            { "r1", "E", "E" },
            { "l2", "R", "R" },
            { "r2", "T", "Mouse Forward" },
            { "l3", "None", "None" },
            { "r3", "None", "None" },
            { "rage", "F", "F" },
            { "options", "Tab", "Tab" },
            { "touchpad_left", "None", "P" },
            { "touchpad_right", "Escape", "Escape" },
            { "up", "1", "1" },
            { "down", "3", "3" },
            { "left", "4", "4" },
            { "right", "2", "2" },
            { "grab", "C", "C" },
            { "dodge", "Left Shift", "Mouse Back" },
            { "weapon_next", "None", "Wheel Up" },
            { "weapon_prev", "None", "Wheel Down" }
        };

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

        // 与移植器默认不同的动作 = 需要持久化到 settings.json 的项
        public static string CustomKeyJson()
        {
            StringBuilder body = new StringBuilder();
            for (int i = 0; i < Actions.GetLength(0); i++)
            {
                string want = Actions[i, 1];
                if (string.Equals(want, Actions[i, 2], StringComparison.Ordinal)) continue;
                if (body.Length > 0) body.Append(", ");
                body.Append('"').Append(Actions[i, 0]).Append("\":\"").Append(want).Append('"');
            }
            return body.ToString();
        }

        // 画面异常时的兜底：无边框 + 原生渲染分辨率 + 60 帧 + 垂直同步
        public static bool ApplySafeDisplay(string dir, int w, int h, out string message)
        {
            string path = Path.Combine(dir, "spartanport.ini");
            try
            {
                if (!File.Exists(path))
                {
                    message = "没找到画面设置文件 spartanport.ini，游戏会自己生成";
                    return false;
                }
                string nl;
                bool finalNewline;
                List<string> lines = ReadIniLines(path, out nl, out finalNewline);
                bool changed = false;
                SetKey(lines, "General", "display_mode", "borderless", ref changed);
                SetKey(lines, "General", "render_resolution", "native", ref changed);
                SetKey(lines, "General", "fps_limit", "60", ref changed);
                SetKey(lines, "General", "engine_fps", "60", ref changed);
                SetKey(lines, "General", "vsync", "1", ref changed);
                SetKey(lines, "General", "hdr", "0", ref changed);
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
                Log("写 spartanport.ini 失败：" + ex);
                return false;
            }
        }

        // 把按键方案写进 settings.json（原版启动器同样只改这一个对象）
        public static bool ApplyKeyScheme(string dir, out string message)
        {
            string path = Path.Combine(dir, "settings.json");
            try
            {
                if (!File.Exists(path))
                {
                    message = "没找到按键设置文件 settings.json";
                    return false;
                }
                string text = File.ReadAllText(path, Encoding.UTF8);
                string body = CustomKeyJson();
                int controls = text.IndexOf("\"controls\"", StringComparison.Ordinal);
                if (controls < 0) { message = "按键设置格式不认识，未改动"; return false; }
                int key = text.IndexOf("\"key\"", controls, StringComparison.Ordinal);
                if (key < 0) { message = "按键设置格式不认识，未改动"; return false; }
                int open = text.IndexOf('{', key);
                if (open < 0) { message = "按键设置格式不认识，未改动"; return false; }
                int close = FindJsonBrace(text, open);
                if (close < 0) { message = "按键设置格式不认识，未改动"; return false; }
                string want = "{" + body.ToString() + "}";
                string updated = text.Substring(0, open) + want + text.Substring(close + 1);
                if (updated != text)
                {
                    Backup(path, dir);
                    File.WriteAllText(path, updated, new UTF8Encoding(false));
                    message = "已写入按键方案：纯键盘（J 轻攻击 / K 重攻击 / L 抓取 / Q 防御 / T 射击 / 左Shift 闪避）";
                }
                else message = "按键方案已是当前选择";
                return true;
            }
            catch (Exception ex)
            {
                message = "写按键设置失败：" + ex.Message;
                Log("写 settings.json 失败：" + ex);
                return false;
            }
        }

        static int FindJsonBrace(string s, int open)
        {
            int depth = 0;
            bool inStr = false, esc = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') inStr = true;
                else if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // 拉起游戏时要传给移植器的环境变量（与原版启动器逐项对齐）
        public static void ApplyLaunchEnvironment(ProcessStartInfo psi, string dir)
        {
            string settings = Path.Combine(dir, "settings.json");
            string json = File.Exists(settings) ? File.ReadAllText(settings, Encoding.UTF8) : "";
            string ini = Path.Combine(dir, "spartanport.ini");
            string iniText = File.Exists(ini) ? File.ReadAllText(ini, Encoding.UTF8) : "";

            Set(psi, "SPARTAN_GAME_DIR", JsonString(json, "game_dir", "game"));
            Set(psi, "SPARTAN_UI_LANGUAGE", JsonString(json, "ui_language", "zh"));
            Set(psi, "SPARTAN_LANGUAGE", JsonString(json, "language", "10"));
            Set(psi, "SPARTAN_STICK_PROMPTS", JsonString(json, "stick_prompts", "keyboard"));
            Set(psi, "SPARTAN_STREAM_MEMORY", JsonString(json, "stream_memory", "cached"));
            Set(psi, "SPARTAN_HDR", IniValue(iniText, "hdr", "0"));
            Set(psi, "SPARTAN_DISK_SHADERS", BoolFlag(json, "disk_shaders"));
            Set(psi, "SPARTAN_SHADER_PREFETCH", BoolFlag(json, "shader_prefetch"));
            Set(psi, "SPARTAN_BACKGROUND_WARMUP", BoolFlag(json, "background_warmup"));
            Set(psi, "SPARTAN_PARALLEL_WARMUP", BoolFlag(json, "parallel_warmup"));
            Set(psi, "SPARTAN_PIPELINE_BINARIES", BoolFlag(json, "pipeline_binary_cache"));
            Set(psi, "SPARTAN_PARTIAL_HEIGHT_VIEWS", BoolFlag(json, "partial_height_views"));
            Set(psi, "SPARTAN_SHADER_NOTES", BoolFlag(json, "shader_notes"));
            Set(psi, "SPARTAN_PERF_DIAG", BoolFlag(json, "perf_diag"));
            Set(psi, "SPARTAN_DISCORD", BoolFlag(json, "discord_rpc"));
            Set(psi, "SPARTAN_MODS_ENABLED", BoolFlag(json, "mods_enabled"));
            Set(psi, "SPARTAN_MODS_DIR", Path.Combine(dir, "mods"));
            Set(psi, "SPARTAN_MODS_CONFIG", Path.Combine(dir, "mods.json"));
            Set(psi, "SPARTAN_PATCHES_DIR", Path.Combine(dir, "patches"));
            Set(psi, "SPARTAN_PATCHES_CONFIG", Path.Combine(dir, "patches.json"));
            Set(psi, "SPARTAN_PATCHES_ONLY", "1");
            string patches = JsonPatches(json);
            if (patches.Length > 0) Set(psi, "SPARTAN_PATCHES", patches);
            Set(psi, "SPARTAN_KEY_MAP", KeyMapOf());
        }

        static void Set(ProcessStartInfo psi, string name, string value)
        {
            if (value == null) value = "";
            psi.EnvironmentVariables[name] = value;
        }

        static string BoolFlag(string json, string key)
        {
            string v = JsonString(json, key, "0");
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
        }

        // 顶层 "key":"value" 或 "key":true/10 —— 只取第一个匹配，够用且不引第三方库
        static string JsonString(string json, string key, string fallback)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return fallback;
            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return fallback;
            int i = colon + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;
            if (i >= json.Length) return fallback;
            if (json[i] == '"')
            {
                int end = json.IndexOf('"', i + 1);
                return end < 0 ? fallback : json.Substring(i + 1, end - i - 1);
            }
            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != '\n' && json[i] != '\r') i++;
            return json.Substring(start, i - start).Trim();
        }

        // game_patches 里第一个机型码对应的补丁清单，用分号连接（原版就是这么传的）
        static string JsonPatches(string json)
        {
            int gp = json.IndexOf("\"game_patches\"", StringComparison.Ordinal);
            if (gp < 0) return "";
            int arr = json.IndexOf('[', gp);
            if (arr < 0) return "";
            int close = json.IndexOf(']', arr);
            if (close < 0) return "";
            string inner = json.Substring(arr + 1, close - arr - 1);
            List<string> items = new List<string>();
            int i = 0;
            while (i < inner.Length)
            {
                int q = inner.IndexOf('"', i);
                if (q < 0) break;
                int e = q + 1;
                while (e < inner.Length && inner[e] != '"') e++;
                if (e >= inner.Length) break;
                items.Add(inner.Substring(q + 1, e - q - 1));
                i = e + 1;
            }
            return string.Join(";", items.ToArray());
        }

        static string IniValue(string text, string key, string fallback)
        {
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(eq + 1).Trim();
            }
            return fallback;
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
                string bak = Path.Combine(dir, Path.Combine("_legacy_compat_backup", "GodofWarIII"));
                Directory.CreateDirectory(bak);
                string dst = Path.Combine(bak, Path.GetFileName(path));
                if (!File.Exists(dst)) File.Copy(path, dst);
            }
            catch (Exception ex)
            {
                Log("备份设置文件失败（继续）：" + ex.Message);
            }
        }

        static void SetKey(List<string> lines, string section, string key, string value, ref bool changed)
        {
            int start = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].Trim(), "[" + section + "]", StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                    break;
                }
            }
            if (start < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Length != 0) lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + "=" + value);
                changed = true;
                return;
            }

            int end = lines.Count;
            for (int i = start + 1; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length > 0 && t[0] == '[') { end = i; break; }
            }

            int found = -1;
            for (int i = start + 1; i < end; i++)
            {
                string t = lines[i].Trim();
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    found = i;
                    break;
                }
            }

            if (found >= 0)
            {
                string oldValue = lines[found].Substring(lines[found].IndexOf('=') + 1).Trim();
                if (!string.Equals(oldValue, value, StringComparison.Ordinal))
                {
                    lines[found] = key + "=" + value;
                    changed = true;
                }
                return;
            }

            lines.Insert(end, key + "=" + value);
            changed = true;
        }
    }
}
