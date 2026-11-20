// 雷电4（Raiden IV · NESiCA 街机版）启动器
// 皮革三栏 900x620；作弊与配备独立普通窗口；外挂型陪跑（TeknoParrotUi -> loader -> game）
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

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革） ----------
    internal static readonly Color BgTop        = Color.FromArgb(0x3A, 0x20, 0x15);
    internal static readonly Color BgBottom     = Color.FromArgb(0x1C, 0x0F, 0x0A);
    internal static readonly Color CardTop      = Color.FromArgb(0x5A, 0x34, 0x21);
    static readonly Color CardBottom   = Color.FromArgb(0x3B, 0x21, 0x16);
    static readonly Color BandTop      = Color.FromArgb(0x24, 0x13, 0x0D);
    static readonly Color BandBottom   = Color.FromArgb(0x3A, 0x20, 0x15);
    internal static readonly Color PanelEdge    = Color.FromArgb(0xB9, 0x8A, 0x55);
    internal static readonly Color Gold         = Color.FromArgb(0xE3, 0xC0, 0x7A);
    static readonly Color GoldBright   = Color.FromArgb(0xF0, 0xD5, 0x96);
    static readonly Color GoldDim      = Color.FromArgb(0x9A, 0x6A, 0x2A);
    internal static readonly Color Ink          = Color.FromArgb(0xF4, 0xE3, 0xC6);
    internal static readonly Color InkDim       = Color.FromArgb(0xD8, 0xBD, 0x96);
    internal static readonly Color OnGold       = Color.FromArgb(0x2A, 0x16, 0x0D);
    static readonly Color LeatherDark  = Color.FromArgb(0x1C, 0x0F, 0x0A);
    static readonly Color LeatherLight = Color.FromArgb(0x6F, 0x4A, 0x2C);
    static readonly Color Stitch       = Color.FromArgb(0xB9, 0x8A, 0x55);
    static readonly Color Ok           = Color.FromArgb(0xD9, 0xA4, 0x41);
    static readonly Color Warn         = Color.FromArgb(0xE0, 0x7A, 0x5F);

    internal Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    internal float dpi = 1f;
    int S(int v) { return (int)Math.Round(v * dpi); }

    Rectangle colLeft, colCenter, colRight;
    Rectangle rcStart, rcCheat, rcRestore, rcLog, rcExit;
    int centerTop;
    string status = "已就绪";
    Color statusColor = Ok;
    bool busy, finishing, autoStart;
    int hover = -1, focus = -1;
    int exitCode;
    CheatForm cheatForm;
    Point mainHome = Point.Empty;

    // ---------- 配置（launcher.ini 可覆盖，持久文件不写盘符） ----------
    internal string gameTitle = "雷电4";
    string gameExe = "game.exe";
    string gameProcess = "game";
    string tpDir = ".";
    string profileName = "RaidenIVNesica.xml";
    string intro = "纵版射击游戏，共五关，支持双人同屏。\n吃武器道具升级火力，炸弹危急时清屏。\n本作为街机 NESiCA 版，画面已适配新系统。";
    string centerNote = "画面已适配新系统，直接进游戏。";
    string entryGuide = "启动后等画面出现，\n点一下游戏画面再按 5 开始。\n1 投币，小键盘 5 加入 2P。";
    string tips = "按键没反应时，先点一下游戏画面\n（把窗口调到前面），再按键。\n移动 WASD，射击 J，炸弹 K，\n与模拟器按键设置页一致。";

    // 右栏键位表：与模拟器设置页玩家已绑定的键一致（UserProfiles 的 BindName 为证）。
    static readonly string[,] KeyRows =
    {
        { "投币", "pc:1" },
        { "1P 开始", "pc:5" },
        { "2P 开始", "pc:NumPad5" },
        { "移动", "pc:W pc:A pc:S pc:D" },
        { "射击", "pc:J" },
        { "炸弹", "pc:K" },
        { "暂停", "pc:Pause" },
        { "退出", "pc:Esc" },
    };

    [STAThread]
    static int Main(string[] args)
    {
        // 规则 42：双横线归一化成单斜杠，日志记原始参数
        string[] norm = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--")) a = "/" + a.Substring(2);
            else if (a.StartsWith("-")) a = "/" + a.Substring(1);
            norm[i] = a;
        }
        if (norm.Length > 0 && (norm[0] == "/selftest")) return SelfTest(Tail(norm));
        if (norm.Length > 0 && (norm[0] == "/readings")) return ReadingsCli();
        if (norm.Length > 0 && (norm[0] == "/wer")) return WerCli(Tail(norm));
        if (norm.Length > 0 && norm[0].StartsWith("/jjget=")) return JjGetCli(norm[0].Substring(7));
        if (norm.Length > 0 && norm[0].StartsWith("/jjset=")) return JjSetCli(norm[0].Substring(7));
        if (norm.Length > 0 && norm[0] == "/trainer=on") { StartTrainer(); return 0; }
        if (norm.Length > 0 && norm[0] == "/trainer=off") { StopTrainer(); return 0; }
        foreach (string a in norm)
            if (a == "/hideparent") HideParentConsole();
        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        foreach (string a in norm)
            if (a == "/go" || a == "/auto") l.autoStart = true;
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；父进程：" + ParentProcessName() +
            "；零交互：" + (l.autoStart ? "是" : "否"));
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        if (JjBool("no_death")) StartTrainer();
        Application.Run(l);
        return l.exitCode;
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
        // 规则 28：DPI 与最终尺寸在构造函数一次算完，居中一步到位
        float rawDpi = 96f;
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { if (g.DpiX > 0f) rawDpi = g.DpiX; }
        dpi = rawDpi / 96f;
        if (dpi < 0.5f || dpi > 4f) dpi = 1f;
        MakeFonts();
        Keycaps.Configure(dpi, fCap, fCapZh);
        ClientSize = new Size(S(900), S(620));
        LayoutRects();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        LoadLauncherIcon();
        Log("启动 " + gameTitle + "｜DPI=" + dpi.ToString("0.##") + "｜窗口 " + ClientSize.Width + "x" + ClientSize.Height);
        if (autoStart) BeginInvoke(new Action(delegate { StartGame(); }));
    }

    void MakeFonts()
    {
        string ui = PickFont(new string[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" },
                             SystemFonts.MessageBoxFont.Name);
        string mono = PickFont(new string[] { "Consolas", "Courier New" }, "Courier New");
        fTitle  = new Font(ui, 15f * dpi, FontStyle.Bold);
        fHead   = new Font(ui, 10.5f * dpi, FontStyle.Bold);
        fRowSel = new Font(ui, 10f * dpi, FontStyle.Bold);
        fRow    = new Font(ui, 10f * dpi, FontStyle.Regular);
        fBody   = new Font(ui, 9.5f * dpi, FontStyle.Regular);
        fCap    = new Font(ui, 9f * dpi, FontStyle.Bold);
        fCapZh  = new Font(ui, 8.5f * dpi, FontStyle.Bold);
        fMono   = new Font(mono, 9f * dpi, FontStyle.Regular);
        fBrand  = new Font(ui, 12f * dpi, FontStyle.Bold);
    }

    static string PickFont(string[] want, string fallback)
    {
        try
        {
            InstalledFontCollection fc = new InstalledFontCollection();
            foreach (string name in want)
                foreach (FontFamily f in fc.Families)
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return name;
        }
        catch { }
        return fallback;
    }

    void LayoutRects()
    {
        int y = S(92), h = S(456);
        colLeft   = new Rectangle(S(20), y, S(252), h);
        colCenter = new Rectangle(S(288), y, S(296), h);
        colRight  = new Rectangle(S(600), y, S(280), h);
        int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
        int stackH = S(20) + S(18) + S(12) + S(44) + S(10) + S(34) + S(8) + S(34) + S(12) + S(18);
        centerTop = colCenter.Y + (colCenter.Height - stackH) / 2;
        rcStart   = new Rectangle(inner, centerTop + S(20) + S(18) + S(12), innerW, S(44));
        int w1 = (innerW - S(8)) * 9 / 16;
        rcCheat   = new Rectangle(inner, rcStart.Bottom + S(10), w1, S(34));
        rcRestore = new Rectangle(rcCheat.Right + S(8), rcCheat.Y, innerW - w1 - S(8), S(34));
        rcLog     = new Rectangle(inner, rcCheat.Bottom + S(8), w1, S(34));
        rcExit    = new Rectangle(rcLog.Right + S(8), rcLog.Y, innerW - w1 - S(8), S(34));
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
        Card(g, colLeft); Card(g, colCenter); Card(g, colRight);
        DrawLeft(g); DrawCenter(g); DrawRight(g); DrawFooter(g);
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
            for (int x = r.X; x < r.Right; x += 7) g.DrawLine(p, x, r.Y, x, r.Bottom);
        using (Pen hi = new Pen(Color.FromArgb(45, 255, 220, 160), 1f))
            g.DrawLine(hi, r.X, r.Y, r.Right, r.Y);
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

    void DrawHeader(Graphics g)
    {
        Point tp = new Point(S(20), S(18));
        TextRenderer.DrawText(g, gameTitle, fTitle, new Point(tp.X + 1, tp.Y + 1), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, gameTitle, fTitle, tp, GoldBright, Color.Transparent);
        TextRenderer.DrawText(g, "MOSS · 雷电系列 · NESiCA 街机版", fBody,
            new Point(S(20), S(46)), InkDim, Color.Transparent);
    }

    void DrawLeft(Graphics g)
    {
        int x = colLeft.X + S(16), w = colLeft.Width - S(32);
        int y = colLeft.Y + S(16);
        TextRenderer.DrawText(g, "关于这款游戏", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(28);
        y = Body(g, intro, new Rectangle(x, y, w, colLeft.Height - S(120)), Ink);
        int by = colLeft.Bottom - S(76);
        using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, x, by, x + w, by);
        using (Pen s = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawLine(s, x, by + 1, x + w, by + 1);
        Body(g, "玩不了或报错\n点「打开运行日志」\n把内容发给我们", new Rectangle(x, by + S(10), w, S(60)), InkDim);
    }

    void DrawCenter(Graphics g)
    {
        int x = colCenter.X + S(16), w = colCenter.Width - S(32);
        TextRenderer.DrawText(g, "启动说明", fHead, new Point(x, centerTop), Gold, Color.Transparent);
        Body(g, centerNote, new Rectangle(x, centerTop + S(24), w, S(40)), Ink);
        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy, hover == 0, focus == 0);
        DrawButton(g, rcCheat, "作弊与配备", false, !busy, hover == 1, focus == 1);
        DrawButton(g, rcRestore, "恢复原版", false, !busy, hover == 2, focus == 2);
        DrawButton(g, rcLog, "打开运行日志", false, true, hover == 3, focus == 3);
        DrawButton(g, rcExit, "退出", false, true, hover == 4, focus == 4);
        Body(g, "无限命开关打开后立即生效，游戏退出自动停。", new Rectangle(x, rcExit.Bottom + S(12), w, S(30)), InkDim);
    }

    void DrawRight(Graphics g)
    {
        int x = colRight.X + S(16), w = colRight.Width - S(32);
        int y = colRight.Y + S(16);
        TextRenderer.DrawText(g, "进游戏说明", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(26);
        y = Body(g, entryGuide, new Rectangle(x, y, w, S(80)), Ink);
        y += S(8);
        TextRenderer.DrawText(g, "操作说明 · 默认键", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(26);
        for (int i = 0; i < KeyRows.GetLength(0); i++)
        {
            TextRenderer.DrawText(g, KeyRows[i, 0], fRow, new Point(x, y + S(4)), Ink, Color.Transparent);
            int kw = Keycaps.GroupWidth(g, KeyRows[i, 1], S(24));
            Keycaps.DrawGroup(g, KeyRows[i, 1], x + w - kw, y, S(24), false);
            y += S(28);
        }
        y += S(4);
        using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, x, y, x + w, y);
        using (Pen s = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawLine(s, x, y + 1, x + w, y + 1);
        y += S(12);
        TextRenderer.DrawText(g, "提示", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(26);
        Body(g, tips, new Rectangle(x, y, w, colRight.Bottom - y - S(14)), Ink);
    }

    void DrawFooter(Graphics g)
    {
        int y = S(578);
        using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, colLeft.X, S(566), ClientSize.Width - S(20), S(566));
        using (Pen s = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawLine(s, colLeft.X, S(567), ClientSize.Width - S(20), S(567));
        StatusDot(g, status, statusColor, colLeft.X, y);
        string hint = busy ? "关窗只是隐藏，游戏继续跑" : "游戏退出后本窗口自动关闭";
        Size sz = TextRenderer.MeasureText(g, hint, fMono);
        TextRenderer.DrawText(g, hint, fMono,
            new Point(ClientSize.Width - S(20) - sz.Width, y), InkDim, Color.Transparent);
        Size brand = TextRenderer.MeasureText(g, BrandGuard.Text, fBrand);
        Point bp = new Point((ClientSize.Width - brand.Width) / 2, y - S(4));
        TextRenderer.DrawText(g, BrandGuard.Text, fBrand, new Point(bp.X + 1, bp.Y + 1), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, BrandGuard.Text, fBrand, bp, GoldBright, Color.Transparent);
    }

    void StatusDot(Graphics g, string text, Color dot, int x, int y)
    {
        using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, x, y + S(4), S(9), S(9));
        TextRenderer.DrawText(g, text, fMono, new Point(x + S(15), y), InkDim, Color.Transparent);
    }

    int Body(Graphics g, string text, Rectangle r, Color color)
    {
        int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(3);
        int y = r.Top;
        foreach (string raw in text.Split('\n'))
        {
            if (y + lh > r.Bottom) break;
            if (raw.Length == 0) { y += lh / 2; continue; }
            bool head = raw.StartsWith("# ");
            Font f = head ? fHead : fBody;
            string line = head ? raw.Substring(2) : raw;
            while (line.Length > 0)
            {
                if (y + lh > r.Bottom) return y;
                int take = line.Length;
                while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > r.Width)
                    take--;
                TextRenderer.DrawText(g, line.Substring(0, take), f, new Point(r.Left, y),
                    head ? Gold : color, Color.Transparent);
                y += lh;
                line = line.Substring(take);
            }
        }
        return y;
    }

    void DrawButton(Graphics g, Rectangle r, string text, bool primary, bool enabled, bool hot, bool focused)
    {
        Color top, bottom, edge, fore;
        if (!enabled)
        {
            top = Color.FromArgb(0x4A, 0x3A, 0x2E); bottom = Color.FromArgb(0x2E, 0x21, 0x19);
            edge = Color.FromArgb(0x5A, 0x4A, 0x3A); fore = Color.FromArgb(0xB8, 0xA5, 0x8E);
        }
        else if (primary)
        {
            top = hot ? GoldBright : Gold;
            bottom = hot ? Color.FromArgb(0xD8, 0xB2, 0x5F) : GoldDim;
            edge = LeatherLight; fore = OnGold;
        }
        else
        {
            top = hot ? Color.FromArgb(0x6B, 0x42, 0x28) : CardTop;
            bottom = hot ? Color.FromArgb(0x4A, 0x2A, 0x1B) : CardBottom;
            edge = PanelEdge; fore = Ink;
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

    void FocusRing(Graphics g, Rectangle r)
    {
        using (GraphicsPath p = Round(Rectangle.Inflate(r, S(5), S(5)), S(10)))
        using (Pen pen = new Pen(Color.FromArgb(120, 0, 0, 0), 3f)) g.DrawPath(pen, p);
        using (GraphicsPath p = Round(Rectangle.Inflate(r, S(3), S(3)), S(9)))
        using (Pen pen = new Pen(GoldBright, 2f)) g.DrawPath(pen, p);
    }

    // ---------- 鼠标 / 键盘 ----------
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (!busy && rcCheat.Contains(pt)) return 1;
        if (!busy && rcRestore.Contains(pt)) return 2;
        if (rcLog.Contains(pt)) return 3;
        if (rcExit.Contains(pt)) return 4;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcCheat);
            r = Rectangle.Union(r, rcRestore);
            r = Rectangle.Union(r, rcLog);
            r = Rectangle.Union(r, rcExit);
            return Rectangle.Inflate(r, S(8), S(8));
        }
    }

    void RepaintControls() { Invalidate(ButtonsRect); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; RepaintControls(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hover != -1) { hover = -1; RepaintControls(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int h = HitTest(e.Location);
        if (h >= 0) { focus = h; Activate(h); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
            case Keys.Escape: Close(); return true;
            case Keys.Enter:
            case Keys.Space:
                if (focus >= 0) { Activate(focus); return true; }
                break;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void CycleFocus(int delta)
    {
        int[] order = busy ? new int[] { 3, 4 } : new int[] { 0, 1, 2, 3, 4 };
        int idx = Array.IndexOf(order, focus);
        if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
        focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
        RepaintControls();
    }

    void Activate(int i)
    {
        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenCheatForm(); return; }
        if (i == 2) { RestoreOriginal(); return; }
        if (i == 3) { OpenLog(); return; }
        if (i == 4) { Close(); }
    }

    // ---------- 启动链（外挂型陪跑：UI -> loader -> game） ----------
    void StartGame()
    {
        if (busy) return;
        string gamePath = Path.Combine(AppDir(), gameExe);
        string tpPath = Path.Combine(AppDir(), tpDir);
        string uiPath = Path.Combine(tpPath, "TeknoParrotUi.exe");
        if (!File.Exists(gamePath)) { SetStatus("缺游戏文件，请把运行日志发给我们", Warn); Log("缺游戏文件：" + gamePath); return; }
        if (!File.Exists(uiPath)) { SetStatus("缺启动组件，请把运行日志发给我们", Warn); Log("缺 TeknoParrotUi：" + uiPath); return; }
        // 模拟器单实例会杀旧 UI：已有局在跑时绝不新起（保护玩家当前局）
        Process[] oldUi = Process.GetProcessesByName("TeknoParrotUi");
        if (oldUi.Length > 0)
        {
            SetStatus("已有游戏在运行，请先退出旧局再启动", Warn);
            Log("检测到 TeknoParrotUi 已在运行（pid " + oldUi[0].Id + "），放弃新启动");
            return;
        }
        Process oldGame = FindGameProcess(DateTime.MinValue, gamePath);
        if (oldGame != null)
        {
            SetStatus("已有游戏在运行，请先退出旧局再启动", Warn);
            Log("检测到 game 已在运行（pid " + oldGame.Id + "），放弃新启动");
            return;
        }
        SetBusy(true, "游戏运行中…");
        Log("点「启动游戏」｜profile=" + profileName);
        Thread t = new Thread(delegate()
        {
            try
            {
                WriteGamePath(tpPath, gamePath);
                int code = RunLoaderAndWait(uiPath, tpPath, gamePath);
                Log("游戏进程退出，退出码 0x" + ((uint)code).ToString("X8") + "，启动器随之退出");
                if (code != 0) CollectWer(gamePath);
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
                SetBusy(false, null);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    void WriteGamePath(string tpPath, string gamePath)
    {
        string up = Path.Combine(tpPath, "UserProfiles", profileName);
        if (!File.Exists(up))
        {
            string gp = Path.Combine(tpPath, "GameProfiles", profileName);
            if (!File.Exists(gp)) throw new Exception("缺 profile：" + profileName);
            Directory.CreateDirectory(Path.GetDirectoryName(up));
            File.Copy(gp, up);
        }
        string xml = File.ReadAllText(up, Encoding.UTF8);
        // TP 的 EmuBlacklist 对 GamePath 目录名做 GetFiles，相对名会炸；
        // 绿色靠启动器每次按自身位置动态写绝对路径，整目录拷走首启自愈。
        string want = "<GamePath>" + gamePath + "</GamePath>";
        int a = xml.IndexOf("<GamePath>");
        int b = xml.IndexOf("</GamePath>");
        if (a >= 0 && b > a)
            xml = xml.Substring(0, a) + want + xml.Substring(b + "</GamePath>".Length);
        else
            xml = xml.Replace("<GamePath></GamePath>", want);
        File.WriteAllText(up, xml, Encoding.UTF8);
        Log("GamePath 已按当前目录写入：" + gamePath);
    }

    int RunLoaderAndWait(string uiPath, string tpPath, string gamePath)
    {
        DateTime launch = DateTime.Now;
        ProcessStartInfo psi = new ProcessStartInfo(uiPath, "--profile=" + profileName + " --startMinimized");
        psi.WorkingDirectory = tpPath;
        psi.UseShellExecute = false;
        Process ui = Process.Start(psi);
        Log("TeknoParrotUi 已起 pid=" + ui.Id + "（--profile 直启 + 起始最小化）");
        // 看门狗：loader 控制台直接隐藏（用户要求不出现任何黑窗口）；游戏窗口与 UI 零干预
        Thread watch = new Thread(delegate()
        {
            DateTime fastUntil = DateTime.Now.AddSeconds(60);
            while (!finishing)
            {
                try { HideLoaderConsoles(launch); }
                catch { }
                Thread.Sleep(DateTime.Now < fastUntil ? 150 : 1000);
            }
        });
        watch.IsBackground = true;
        watch.Start();
        Process game = null;
        for (int i = 0; i < 240 && game == null; i++)
        {
            Thread.Sleep(500);
            game = FindGameProcess(launch, gamePath);
            if (game == null && ui.HasExited && i > 20) break;
        }
        if (game == null)
        {
            try { if (!ui.HasExited) ui.CloseMainWindow(); } catch { }
            throw new Exception("游戏没有启动，请看运行日志");
        }
        Log("找到游戏进程 pid=" + game.Id + "，陪跑等待退出");
        // 外部找来的进程需要趁活着持有句柄，退出后才能读到真实退出码
        IntPtr gh = IntPtr.Zero;
        try { gh = game.Handle; } catch { }
        game.WaitForExit();
        int code;
        if (gh == IntPtr.Zero || !GetExitCodeProcess(gh, out code))
        {
            try { code = game.ExitCode; }
            catch { code = 1; Log("读不到游戏退出码，按 1 记录"); }
        }
        try
        {
            if (!ui.WaitForExit(5000))
            {
                ui.CloseMainWindow();
                if (!ui.WaitForExit(3000)) { ui.Kill(); Log("UI 未自退，已结束 pid=" + ui.Id); }
            }
        }
        catch (Exception ex) { Log("收尾 UI 进程异常：" + ex.Message); }
        return code;
    }

    Process FindGameProcess(DateTime notBefore, string gamePath)
    {
        foreach (Process p in Process.GetProcessesByName(gameProcess))
        {
            try
            {
                if (p.StartTime < notBefore) continue;
                string file = p.MainModule.FileName;
                if (string.Equals(file, gamePath, StringComparison.OrdinalIgnoreCase)) return p;
            }
            catch { }
        }
        return null;
    }

    void HideLoaderConsoles(DateTime launch)
    {
        foreach (Process p in Process.GetProcesses())
        {
            string n = p.ProcessName;
            if (!n.StartsWith("OpenParrotLoader", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (p.StartTime < launch) continue;
                uint pid = (uint)p.Id;
                EnumWindows(delegate(IntPtr h, IntPtr lp)
                {
                    uint wpid;
                    GetWindowThreadProcessId(h, out wpid);
                    if (wpid == pid && IsWindowVisible(h)) ShowWindow(h, 0);
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
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
                if (cheatForm != null) { cheatForm.Close(); cheatForm = null; }
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
            Log("玩家关窗，隐藏窗口后台陪跑（无托盘）");
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    void SetBusy(bool value, string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), value, text); return; }
        busy = value;
        hover = -1;
        if (value) focus = -1;
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

    // ---------- 作弊与配备（独立普通窗口） ----------
    void OpenCheatForm()
    {
        if (cheatForm != null && !cheatForm.IsDisposed) { cheatForm.BringToFront(); return; }
        cheatForm = new CheatForm(this);
        Rectangle wa = Screen.FromControl(this).WorkingArea;
        if (mainHome == Point.Empty) mainHome = Location;
        int cw = cheatForm.Width;
        if (Right + S(16) + cw <= wa.Right)
        {
            cheatForm.Location = new Point(Right + S(16), Math.Max(wa.Top, Top));
        }
        else
        {
            Location = new Point(wa.Left + S(8), Math.Max(wa.Top, Top));
            cheatForm.Location = new Point(Right + S(16), Math.Max(wa.Top, Top));
            cheatForm.Width = Math.Max(S(420), wa.Right - Right - S(24));
        }
        cheatForm.FormClosed += delegate
        {
            if (!finishing && mainHome != Point.Empty) Location = mainHome;
            cheatForm = null;
        };
        cheatForm.Show();
        Log("打开作弊与配备窗口");
    }

    void RestoreOriginal()
    {
        string ini = Path.Combine(AppDir(), "JJ11.ini");
        string bak = Path.Combine(AppDir(), "JJ11.ini.sjis-bak");
        if (!File.Exists(bak))
        {
            SetStatus("缺原版备份，无法恢复，请把运行日志发给我们", Warn);
            Log("恢复原版失败：缺 " + bak);
            return;
        }
        try
        {
            StopTrainer();
            JjSet("no_death", "FALSE");
            File.Copy(bak, ini, true);
            Log("恢复原版：JJ11.ini.sjis-bak -> JJ11.ini");
            SetStatus("已恢复原版配置", Ok);
            if (cheatForm != null && !cheatForm.IsDisposed) cheatForm.Reload();
        }
        catch (Exception ex)
        {
            Log("恢复原版失败：" + ex);
            SetStatus("恢复失败，请把运行日志发给我们", Warn);
        }
    }

    // ---------- 无敌 trainer（运行时命数锁定，纯数据写，不碰游戏文件） ----------
    static string TrainerPidPath { get { return Path.Combine(AppDir(), "trainer.pid"); } }
    static string TrainerScript { get { return Path.Combine(AppDir(), "tools", "trainer.ps1"); } }

    internal static bool TrainerRunning()
    {
        try
        {
            if (!File.Exists(TrainerPidPath)) return false;
            int pid = int.Parse(File.ReadAllText(TrainerPidPath).Trim());
            using (Process p = Process.GetProcessById(pid)) return !p.HasExited;
        }
        catch { return false; }
    }

    internal static void StartTrainer()
    {
        if (TrainerRunning()) { Log("无敌 trainer 已在跑，不重复起"); return; }
        if (!File.Exists(TrainerScript)) { Log("缺 trainer 脚本：" + TrainerScript); return; }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("pwsh.exe",
                "-NoProfile -File \"" + TrainerScript + "\" -Lock 3 -PidFile \"" + TrainerPidPath + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
            Log("无敌 trainer 已起（命数锁定 3）");
        }
        catch (Exception ex) { Log("起 trainer 失败：" + ex.Message); }
    }

    internal static void StopTrainer()
    {
        try
        {
            if (File.Exists(TrainerPidPath))
            {
                int pid = int.Parse(File.ReadAllText(TrainerPidPath).Trim());
                using (Process p = Process.GetProcessById(pid))
                    if (!p.HasExited) { p.Kill(); Log("无敌 trainer 已停 pid=" + pid); }
            }
        }
        catch (Exception ex) { Log("停 trainer 失败：" + ex.Message); }
        try { if (File.Exists(TrainerPidPath)) File.Delete(TrainerPidPath); } catch { }
    }

    // ---------- JJ11.ini（Shift-JIS 注释保留，只动纯 ASCII 生效行） ----------
    static readonly Encoding Sjis = Encoding.GetEncoding(932);

    static string JjPath { get { return Path.Combine(AppDir(), "JJ11.ini"); } }

    static string[] JjLines()
    {
        if (!File.Exists(JjPath)) return new string[0];
        return Sjis.GetString(File.ReadAllBytes(JjPath)).Split('\n');
    }

    static void JjWrite(string[] lines)
    {
        File.WriteAllBytes(JjPath, Sjis.GetBytes(string.Join("\n", lines)));
    }

    static bool JjActive(string line, string key, out string value)
    {
        value = null;
        string t = line.TrimStart();
        if (t.StartsWith("//")) return false;
        int eq = t.IndexOf('=');
        if (eq <= 0) return false;
        if (!string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) return false;
        string v = t.Substring(eq + 1);
        int c = v.IndexOf("//");
        if (c >= 0) v = v.Substring(0, c);
        value = v.Trim();
        return true;
    }

    internal static string JjGet(string key)
    {
        foreach (string line in JjLines())
        {
            string v;
            if (JjActive(line, key, out v)) return v;
        }
        return null;
    }

    internal static bool JjBool(string key)
    {
        string v = JjGet(key);
        return v != null && (v.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || v == "1");
    }

    internal static int JjInt(string key, int fallback)
    {
        string v = JjGet(key);
        int n;
        if (v != null && int.TryParse(v, out n)) return n;
        return fallback;
    }

    internal static void JjSet(string key, string value)
    {
        string[] lines = JjLines();
        bool done = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string v;
            if (!JjActive(lines[i], key, out v)) continue;
            string t = lines[i].TrimStart();
            int eq = t.IndexOf('=');
            string head = lines[i].Substring(0, lines[i].Length - t.Length) + t.Substring(0, eq + 1);
            string tail = "";
            int c = t.Substring(eq + 1).IndexOf("//");
            if (c >= 0) tail = "\t\t\t" + t.Substring(eq + 1 + c);
            lines[i] = head + " " + value + tail;
            done = true;
            break;
        }
        if (!done)
        {
            List<string> list = new List<string>(lines);
            string nl = key + "\t\t= " + value + "\r";
            // 原文件以换行结尾时 Split 会留一个空尾元素，插在它前面，保持文件结尾形状
            if (list.Count > 0 && list[list.Count - 1].Length == 0) list.Insert(list.Count - 1, nl);
            else list.Add(nl);
            lines = list.ToArray();
        }
        JjWrite(lines);
        Log("JJ11.ini 写入：" + key + " = " + value);
    }

    // ---------- 作弊写入的命令行回归入口（不启动游戏；结论另存 UTF-8 文件） ----------
    static int JjGetCli(string key)
    {
        try
        {
            string v = JjGet(key);
            File.WriteAllText(Path.Combine(AppDir(), "jjread.txt"), key + "=" + (v == null ? "(无)" : v), Encoding.UTF8);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDir(), "jjread.txt"), "读取失败：" + ex.Message, Encoding.UTF8);
            return 1;
        }
    }

    static int JjSetCli(string pair)
    {
        int eq = pair.IndexOf('=');
        if (eq <= 0) return 1;
        try
        {
            JjSet(pair.Substring(0, eq), pair.Substring(eq + 1));
            return 0;
        }
        catch (Exception ex)
        {
            Log("jjset 失败：" + ex);
            return 1;
        }
    }

    // ---------- 崩溃取证（WER，系统后台报告，重试几秒） ----------
    void CollectWer(string gamePath)
    {
        try
        {
            string exeName = Path.GetFileName(gamePath);
            string[] roots = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows", "WER", "ReportArchive"),
            };
            string found = null;
            for (int attempt = 0; attempt < 3 && found == null; attempt++)
            {
                Thread.Sleep(2000);
                foreach (string root in roots)
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string dir in Directory.GetDirectories(root, "AppCrash_" + exeName + "_*"))
                    {
                        foreach (string f in Directory.GetFiles(dir, "*.wer"))
                        {
                            found = f;
                            break;
                        }
                        if (found != null) break;
                    }
                    if (found != null) break;
                }
            }
            if (found == null) { Log("崩溃取证：没抓到 WER 报告"); return; }
            string dumpDir = Path.Combine(AppDir(), "崩溃转储");
            Directory.CreateDirectory(dumpDir);
            string dest = Path.Combine(dumpDir, Path.GetFileName(found));
            File.Copy(found, dest, true);
            string text = File.ReadAllText(found, Encoding.Unicode);
            string sig = "";
            foreach (string line in text.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("Sig[", StringComparison.OrdinalIgnoreCase) &&
                    (t.Contains("ExceptionCode") || t.Contains("ExceptionOffset") || t.Contains("ModuleName")))
                    sig += t + "；";
            }
            Log("崩溃取证：已存 " + dest + "｜" + (sig.Length > 0 ? sig : "报告内无异常签名行"));
        }
        catch (Exception ex) { Log("崩溃取证异常：" + ex.Message); }
    }

    static int WerCli(string[] args)
    {
        if (args.Length < 2) return 1;
        try
        {
            string text = File.ReadAllText(args[0], Encoding.Unicode);
            StringBuilder sb = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("Sig[", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("DynamicSig[", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine(t);
            }
            File.WriteAllText(args[1], sb.ToString(), Encoding.UTF8);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(args[1], "读取失败：" + ex.Message, Encoding.UTF8);
            return 1;
        }
    }

    // ---------- 环境读数（只读不改系统状态） ----------
    static int ReadingsCli()
    {
        StringBuilder sb = new StringBuilder();
        bool ok = true;
        try
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Rectangle bd = Screen.PrimaryScreen.Bounds;
            float rdpi = 96f;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { if (g.DpiX > 0f) rdpi = g.DpiX; }
            sb.AppendLine("屏幕宽高=" + bd.Width + "x" + bd.Height);
            sb.AppendLine("工作区=" + wa.Width + "x" + wa.Height);
            sb.AppendLine("DPI=" + rdpi.ToString("0.##"));
            if (bd.Width <= 0 || bd.Height <= 0 || wa.Width <= 0 || wa.Height <= 0 || rdpi <= 0f) ok = false;
            string dir = AppDir();
            bool game = File.Exists(Path.Combine(dir, "game.exe"));
            bool ui = File.Exists(Path.Combine(dir, "TeknoParrotUi.exe"));
            bool jj = File.Exists(Path.Combine(dir, "JJ11.ini"));
            bool bak = File.Exists(Path.Combine(dir, "JJ11.ini.sjis-bak"));
            sb.AppendLine("game.exe=" + (game ? "在" : "缺"));
            sb.AppendLine("TeknoParrotUi=" + (ui ? "在" : "缺"));
            sb.AppendLine("JJ11.ini=" + (jj ? "在" : "缺"));
            sb.AppendLine("原版备份=" + (bak ? "在" : "缺"));
            if (!game || !ui || !jj) ok = false;
        }
        catch (Exception ex)
        {
            sb.AppendLine("读数异常：" + ex.Message);
            ok = false;
        }
        sb.AppendLine("断言=" + (ok ? "通过" : "失败"));
        try { File.WriteAllText(Path.Combine(AppDir(), "readings.txt"), sb.ToString(), Encoding.UTF8); }
        catch { }
        return ok ? 0 : 1;
    }

    // ---------- 日志 / 图标 / 配置 ----------
    static readonly object logGate = new object();
    static string LogPath { get { return Path.Combine(AppDir(), "运行日志.txt"); } }

    internal static void Log(string msg)
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
            SetStatus("日志打不开，请把运行日志发给我们", Warn);
        }
    }

    void LoadLauncherIcon()
    {
        try
        {
            Icon ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (ic != null) { Icon = ic; return; }
        }
        catch (Exception ex) { Log("取 exe 图标失败：" + ex.Message); }
        Icon = SystemIcons.Application;
        Log("exe 图标取不到，退回系统默认图标");
    }

    static string AppDir() { return Path.GetDirectoryName(Application.ExecutablePath); }

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
            string val = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
            switch (key)
            {
                case "title": gameTitle = val; break;
                case "gameexe": gameExe = val; break;
                case "gameprocess": gameProcess = val; break;
                case "tpdir": tpDir = val; break;
                case "profile": profileName = val; break;
                case "intro": intro = val; break;
                case "centernote": centerNote = val; break;
                case "entryguide": entryGuide = val; break;
                case "tips": tips = val; break;
            }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (cheatForm != null && !cheatForm.IsDisposed) cheatForm.Close();
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

    // ---------- 命令行入口 ----------
    static string[] Tail(string[] a)
    {
        string[] r = new string[a.Length - 1];
        Array.Copy(a, 1, r, 0, r.Length);
        return r;
    }

    static int SelfTest(string[] args)
    {
        try
        {
            if (args.Length < 1) return 1;
            string dir = AppDir();
            string file = args[0];
            string rest = "";
            for (int i = 1; i < args.Length; i++) rest += (rest.Length == 0 ? "" : " ") + args[i];
            if (!Path.IsPathRooted(file))
            {
                string local = Path.Combine(dir, file);
                if (File.Exists(local)) file = local;
            }
            ProcessStartInfo psi = new ProcessStartInfo(file, rest);
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

    static string ParentProcessName()
    {
        try
        {
            int parent = ParentPid(Process.GetCurrentProcess().Id);
            if (parent <= 0) return "未知";
            return Process.GetProcessById(parent).ProcessName;
        }
        catch { return "未知"; }
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

    static int ParentPid(int pid)
    {
        IntPtr snap = CreateToolhelp32Snapshot(0x2, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return 0;
        try
        {
            PROCESSENTRY32 pe = new PROCESSENTRY32();
            pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            if (!Process32First(snap, ref pe)) return 0;
            do
            {
                if (pe.th32ProcessID == (uint)pid) return (int)pe.th32ParentProcessID;
            } while (Process32Next(snap, ref pe));
            return 0;
        }
        finally { CloseHandle(snap); }
    }

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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 pe);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 pe);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out int code);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);

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
}

// ---------- 作弊与配备窗口：开关 + 下拉，状态从 JJ11.ini 生效行反算 ----------
class CheatForm : Form
{
    readonly Launcher host;
    float dpi;
    int S(int v) { return (int)Math.Round(v * dpi); }

    class CheatSwitch
    {
        public string Key; public string Label; public string Note;
        public Rectangle Rect; public bool On;
    }

    readonly CheatSwitch[] switches =
    {
        new CheatSwitch { Key = "no_death", Label = "无限命（永不结束）", Note = "命数锁 3、永不结束；机子被打中仍会爆一下重生" },
    };

    int hoverSwitch = -1;

    internal CheatForm(Launcher host)
    {
        this.host = host;
        dpi = host.dpi;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.Manual;
        Text = host.gameTitle + " · 作弊与配备";
        BackColor = Launcher.BgBottom;
        ClientSize = new Size(S(480), S(180));
        MinimumSize = new Size(S(420), S(160));
        Keycaps.Configure(dpi, host.fCap, host.fCapZh);
        Reload();
        LayoutNow();
    }

    internal void Reload()
    {
        foreach (CheatSwitch sw in switches) sw.On = Launcher.JjBool(sw.Key);
        Invalidate();
    }

    void LayoutNow()
    {
        int x = S(20), w = ClientSize.Width - S(40);
        int y = S(56);
        foreach (CheatSwitch sw in switches)
        {
            sw.Rect = new Rectangle(x, y, w, S(30));
            y += S(36);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutNow();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        using (LinearGradientBrush b = new LinearGradientBrush(all, Launcher.BgTop, Launcher.BgBottom, LinearGradientMode.Vertical))
            g.FillRectangle(b, all);
        int x = S(20);
        TextRenderer.DrawText(g, "作弊与配备", host.fHead, new Point(x, S(16)), Launcher.Gold, Color.Transparent);
        TextRenderer.DrawText(g, "无限命打开后立即生效；游戏退出自动停，也可手动关。", host.fBody,
            new Point(x, S(36)), Launcher.InkDim, Color.Transparent);
        foreach (CheatSwitch sw in switches)
        {
            TextRenderer.DrawText(g, sw.Label, host.fRowSel, new Point(sw.Rect.X, sw.Rect.Y + S(5)), Launcher.Ink, Color.Transparent);
            TextRenderer.DrawText(g, sw.Note, host.fBody, new Point(sw.Rect.X + S(70), sw.Rect.Y + S(6)), Launcher.InkDim, Color.Transparent);
            Rectangle tg = new Rectangle(sw.Rect.Right - S(46), sw.Rect.Y + S(4), S(46), S(22));
            using (GraphicsPath p = Round(tg, S(11)))
            using (SolidBrush b = new SolidBrush(sw.On ? Launcher.Gold : Color.FromArgb(0x2A, 0x16, 0x0D)))
                g.FillPath(b, p);
            using (GraphicsPath p = Round(tg, S(11)))
            using (Pen pen = new Pen(Launcher.PanelEdge, 1f)) g.DrawPath(pen, p);
            int kx = sw.On ? tg.Right - S(20) : tg.X + S(2);
            using (SolidBrush b = new SolidBrush(sw.On ? Launcher.OnGold : Launcher.InkDim))
                g.FillEllipse(b, kx, tg.Y + S(2), S(18), S(18));
            TextRenderer.DrawText(g, sw.On ? "开" : "关", host.fBody,
                new Point(sw.On ? tg.X + S(6) : tg.Right - S(20), tg.Y + S(4)),
                sw.On ? Launcher.OnGold : Launcher.InkDim, Color.Transparent);
        }
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        for (int i = 0; i < switches.Length; i++)
        {
            if (!switches[i].Rect.Contains(e.Location)) continue;
            switches[i].On = !switches[i].On;
            Launcher.JjSet(switches[i].Key, switches[i].On ? "TRUE" : "FALSE");
            if (switches[i].Key == "no_death")
            {
                if (switches[i].On) Launcher.StartTrainer(); else Launcher.StopTrainer();
            }
            Invalidate();
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = -1;
        for (int i = 0; i < switches.Length; i++)
            if (switches[i].Rect.Contains(e.Location)) h = i;
        if (h != hoverSwitch) { hoverSwitch = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
    }
}