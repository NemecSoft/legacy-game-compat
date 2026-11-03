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

// 鬼武者：剑之道启动器（game_starter.exe）。
// 界面规范见仓库 .qoder/skills/legacy-launcher-gui/SKILL.md。
// 核心职责：每次启动前把 config.ini 的显示方式固定为“无边框 + 当前桌面分辨率”，
// 让宽屏 / 2K 机器都按各自桌面输出，避免沿用别人机器上的全屏分辨率索引。

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
    string gameTitle = "鬼武者：剑之道";
    string gameExe = "OnimushaWotS.exe";
    string gameArgs = "";
    string intro =
        "《鬼武者：剑之道》是 CAPCOM 的战国题材\n" +
        "动作游戏，以宫本武藏为主角，挥舞太刀\n" +
        "与鬼之力斩妖除魔。\n" +
        "\n" +
        "战斗讲究一击一杀的紧张感，\n" +
        "关卡和演出充满和风与诡异气息。\n" +
        "\n" +
        "这份是免安装版，点「启动游戏」即可。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给管理员";
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
    LegendItem[][] legend = new LegendItem[0][];
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

    class LegendItem { public string Caption; public string[] Caps; }

    [STAThread]
    static int Main(string[] args)
    {
        DisplayGuard.EnableDpiAwareness();

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
            DisplayGuard.GetDesktop(out w, out h, out src);
            string msg;
            bool ok = DisplayGuard.Apply(AppDir(), w, h, out msg);
            Log("命令行修复分辨率：" + msg + "（" + src + " " + w + "×" + h + "）");
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
        if (selMode == null)
        {
            selMode = MakeModeSelect();
            selMode.TabIndex = 0;
        }
        LayoutRects();
        DisplayGuard.GetDesktop(out desktopW, out desktopH, out desktopSource);
        string msg = DisplayGuard.Describe(desktopW, desktopH);
        status = "已就绪";
        statusColor = Ok;
        LoadGameIcon();
        Log("启动 " + gameTitle + "｜桌面 " + msg + "（" + desktopSource + "）｜DPI " + dpi.ToString("0.##"));
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
        int noteBottom = colCenter.Y + S(46) + MeasureCenterNoteHeight(innerW);
        int labelY = noteBottom + S(16);
        int comboY = labelY + S(24);
        int startY = comboY + S(34) + S(14);
        rcStart = new Rectangle(inner, startY, innerW, S(44));
        int bw = (innerW - S(16)) / 3;
        rcFix = new Rectangle(inner, rcStart.Bottom + S(10), bw, S(34));
        rcLog = new Rectangle(rcFix.Right + S(8), rcFix.Y, bw, S(34));
        rcExit = new Rectangle(rcLog.Right + S(8), rcLog.Y, innerW - (rcLog.Right + S(8) - inner), S(34));
        rcLabelMode = new Rectangle(inner, labelY, innerW, S(22));
        rcComboMode = new Rectangle(inner, comboY, innerW, S(30));
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
        TextRenderer.DrawText(g, "Onimusha: Way of the Sword · CAPCOM",
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
        TextRenderer.DrawText(g, "启动说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(32);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);
        TextRenderer.DrawText(g, "启动选项", fHead,
            new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFix, "修复分辨率", false, !busy,
            hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcLog, "打开运行日志", false, true,
            hover == 2 || focus == 2, focus == 2);
        DrawButton(g, rcExit, "退出", false, true,
            hover == 3 || focus == 3, focus == 3);
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
        int x = colRight.X + S(16);
        int h = TextRenderer.MeasureText(g, "国", fHead).Height;
        if (InViewport(y, h, top, bottom))
            TextRenderer.DrawText(g, "操作说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(30);
        y = DrawLegendRows(g, legend, x, y, top, bottom);
        y += S(12);
        return DrawWrapped(g, "核心：敌人攻击瞬间按右键化劲；空格回避；不要贪刀。",
            x, y, colRight.Width - S(32), fBody, InkDim);
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
                    if (cx + itemW > rightEdge && cx > x) break;
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

    // ---------- 键帽表（来自游戏内「操作」页实测） ----------
    void RebuildLegend()
    {
        legend = new LegendItem[][]
        {
            new[] { Item("移动", "W", "A", "S", "D") },
            new[] { Item("单手攻击", "鼠标左键") },
            new[] { Item("双手攻击", "R") },
            new[] { Item("防御 / 化劲", "鼠标右键") },
            new[] { Item("回避", "空格") },
            new[] { Item("锁定目标", "鼠标中键") },
            new[] { Item("调查", "F") },
            new[] { Item("使用道具", "Q") },
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

    void RepaintControls() { Invalidate(ButtonsRect); }

    void PlaceSelect(Control c, Rectangle slot)
    {
        if (c == null) return;
        c.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);
    }

    SelectPanel MakeModeSelect()
    {
        SelectPanel p = new SelectPanel();
        p.BackColor = Color.FromArgb(0x4A, 0x00, 0x12);
        p.ForeColor = Ink;
        p.Font = fRow;
        p.Text = LaunchModeNames[launchMode];
        p.Cursor = Cursors.Hand;
        p.Paint += PaintModeSelect;
        p.Click += delegate { ShowModeMenu(); };
        Controls.Add(p);
        return p;
    }

    void PaintModeSelect(object sender, PaintEventArgs e)
    {
        Control p = (Control)sender;
        Color back = p.Enabled ? Color.FromArgb(0x4A, 0x00, 0x12) : Color.FromArgb(0x3A, 0x10, 0x18);
        using (SolidBrush b = new SolidBrush(back)) e.Graphics.FillRectangle(b, p.ClientRectangle);
        using (Pen edge = new Pen(p.Enabled ? GoldDim : Color.FromArgb(0x6A, 0x40, 0x30), 1f))
            e.Graphics.DrawRectangle(edge, 0, 0, p.Width - 1, p.Height - 1);
        Rectangle tr = new Rectangle(S(10), 0, p.Width - S(34), p.Height);
        TextRenderer.DrawText(e.Graphics, p.Text, fRow, tr,
            p.Enabled ? Ink : Color.FromArgb(0xC8, 0xA0, 0xA0), Color.Transparent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using (Pen pen = new Pen(p.Enabled ? Gold : GoldDim, 1.5f))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = p.Width - S(14), cy = p.Height / 2;
            e.Graphics.DrawLine(pen, cx - S(4), cy - S(2), cx, cy + S(2));
            e.Graphics.DrawLine(pen, cx, cy + S(2), cx + S(4), cy - S(2));
        }
    }

    void ShowModeMenu()
    {
        if (busy) return;
        if (modeMenu == null) BuildModeMenu();
        for (int i = 0; i < modeMenu.Items.Count; i++)
            modeMenu.Items[i].Text = (i == launchMode ? "✓  " : "    ") + LaunchModeNames[i];
        modeMenu.Show(selMode, new Point(0, selMode.Height + S(2)));
    }

    void BuildModeMenu()
    {
        modeMenu = new ContextMenuStrip();
        modeMenu.ShowImageMargin = false;
        modeMenu.ShowCheckMargin = false;
        modeMenu.BackColor = Color.FromArgb(0x4A, 0x00, 0x12);
        modeMenu.ForeColor = Ink;
        modeMenu.Font = fRow;
        modeMenu.Padding = new Padding(S(4));
        modeMenu.Renderer = new RedMenuRenderer();
        for (int i = 0; i < LaunchModeNames.Length; i++)
        {
            int idx = i;
            ToolStripMenuItem item = new ToolStripMenuItem(LaunchModeNames[i]);
            item.ForeColor = Ink;
            item.Padding = new Padding(S(8), S(4), S(8), S(4));
            item.Click += delegate
            {
                launchMode = idx;
                selMode.Text = LaunchModeNames[idx];
                selMode.Invalidate();
                Log("选择启动方式：" + LaunchModeNames[idx]);
            };
            modeMenu.Items.Add(item);
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
        int[] order = busy ? new int[] { 2, 3 } : new int[] { 0, 1, 2, 3 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
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

        DisplayGuard.GetDesktop(out desktopW, out desktopH, out desktopSource);
        string desktop = DisplayGuard.Describe(desktopW, desktopH);
        int mode = launchMode;
        SetBusy(true, "正在启动…");
        Log("点「启动游戏」：" + (mode == 0 ? "游戏自动检测" : "固定桌面分辨率"));
        Thread t = new Thread(delegate ()
        {
            try
            {
                if (mode == 1)
                {
                    string msg;
                    if (!DisplayGuard.Apply(AppDir(), desktopW, desktopH, out msg))
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
        SetBusy(true, "正在修复分辨率…");
        Thread t = new Thread(delegate ()
        {
            try
            {
                DisplayGuard.GetDesktop(out desktopW, out desktopH, out desktopSource);
                string msg;
                bool ok = DisplayGuard.Apply(AppDir(), desktopW, desktopH, out msg);
                Log("修复分辨率：" + msg);
                SetStatus(ok ? msg : "修复失败：" + msg, ok ? Ok : Warn);
            }
            catch (Exception ex)
            {
                Log("修复分辨率失败：" + ex);
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
        try
        {
            if (File.Exists(@"D:\ico.ico")) { Icon = new Icon(@"D:\ico.ico"); return; }
        }
        catch { }
        Icon = SystemIcons.Application;
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
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    class RedMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(0x4A, 0x00, 0x12)))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush b = new SolidBrush(e.Item.Selected ? Color.FromArgb(0x7A, 0x0F, 0x22) : Color.FromArgb(0x4A, 0x00, 0x12)))
                e.Graphics.FillRectangle(b, r);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.FromArgb(0xFF, 0xE6, 0x8C) : Color.FromArgb(0xFF, 0xF3, 0xE6);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Color.FromArgb(0xC9, 0xA2, 0x27), 1f))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    // ---------- 显示守卫 ----------
    static class DisplayGuard
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

        public static bool Apply(string dir, int w, int h, out string message)
        {
            string path = Path.Combine(dir, "config.ini");
            if (!File.Exists(path))
            {
                if (w <= 0 || h <= 0)
                {
                    message = "首次启动：游戏会按桌面分辨率自动生成设置";
                    return true;
                }
                try
                {
                    StringBuilder first = new StringBuilder();
                    first.Append("[Graphics]\n");
                    first.Append("DisplayType=Auto\n");
                    first.Append("NormalWindowResolution=(" + w + "," + h + ")\n");
                    first.Append("WindowMode=Borderless\n");
                    first.Append("[RenderConfig]\n");
                    first.Append("FullScreenMode=false\n");
                    first.Append("NormalWindowResolution=(" + w + ".000000," + h + ".000000)\n");
                    first.Append("WindowMode=Borderless\n");
                    File.WriteAllText(path, first.ToString(), new UTF8Encoding(false));
                    message = "首次启动：已按桌面 " + Describe(w, h) + " 创建分辨率设置";
                    return true;
                }
                catch (Exception ex)
                {
                    message = "创建分辨率设置失败：" + ex.Message;
                    Log("创建 config.ini 失败：" + ex);
                    return false;
                }
            }
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                string nl = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
                bool finalNewline = text.EndsWith("\n", StringComparison.Ordinal);
                string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                List<string> lines = new List<string>(raw);
                if (finalNewline && lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);

                bool changed = false;
                SetKey(lines, "Graphics", "WindowMode", "Borderless", ref changed);
                SetKey(lines, "Graphics", "DisplayType", "Auto", ref changed);
                if (w > 0 && h > 0)
                    SetKey(lines, "Graphics", "NormalWindowResolution", "(" + w + "," + h + ")", ref changed);
                SetKey(lines, "RenderConfig", "WindowMode", "Borderless", ref changed);
                SetKey(lines, "RenderConfig", "FullScreenMode", "false", ref changed);
                if (w > 0 && h > 0)
                    SetKey(lines, "RenderConfig", "NormalWindowResolution",
                        "(" + w + ".000000," + h + ".000000)", ref changed);

                if (!changed)
                {
                    message = "分辨率已就绪：桌面 " + Describe(w, h) + "，无边框全屏";
                    return true;
                }

                Backup(path, dir);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.Append(lines[i]);
                    if (i < lines.Count - 1 || finalNewline) sb.Append(nl);
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                message = "已按桌面 " + Describe(w, h) + " 调整分辨率（无边框全屏）";
                return true;
            }
            catch (Exception ex)
            {
                message = "写画面设置失败：" + ex.Message;
                Log("写 config.ini 失败：" + ex);
                return false;
            }
        }

        static void Backup(string path, string dir)
        {
            try
            {
                string bak = Path.Combine(dir, Path.Combine("_legacy_compat_backup", "OnimushaWotS"));
                Directory.CreateDirectory(bak);
                string dst = Path.Combine(bak, "config.ini");
                if (!File.Exists(dst)) File.Copy(path, dst);
            }
            catch (Exception ex)
            {
                Log("备份 config.ini 失败（继续）：" + ex.Message);
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
