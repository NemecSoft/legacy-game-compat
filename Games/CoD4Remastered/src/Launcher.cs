using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 使命召唤4：现代战争 重制版（h1_sp64_ship.exe）启动器 game_starter.exe
// 界面规范见仓库 .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、三栏 900×620、拟物皮革。
// 布局：左栏游戏简介 / 中栏启动按钮组 / 右栏拟物键帽操作说明（键位取自 players2\keys.cfg）。
// 本启动器不切语言、不写任何语言配置：游戏目录里的语言开关保持原样。
// 平台托管契约：游戏是直接子进程、启动器陪跑、退出码原样回传、关窗隐藏不杀游戏。
// 窗口零干预（AGENTS 规则 44）：不碰游戏进程的窗口，全屏切换交给游戏自己。
// Build:
//   csc -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001
//       -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:game_starter.exe Keycaps.cs Launcher.cs

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革，取自 leather-recipes.md §0） ----------
    static readonly Color BgTop = Color.FromArgb(0x3A, 0x20, 0x15);
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
    static readonly Color LeatherDark = Color.FromArgb(0x1C, 0x0F, 0x0A);
    static readonly Color LeatherLight = Color.FromArgb(0x6F, 0x4A, 0x2C);
    static readonly Color Stitch = Color.FromArgb(0xB9, 0x8A, 0x55);
    static readonly Color Ok = Color.FromArgb(0xD9, 0xA4, 0x41);
    static readonly Color Warn = Color.FromArgb(0xE0, 0x7A, 0x5F);

    static readonly string GameExe = "h1_sp64_ship.exe";
    string gameExe = GameExe;
    string gameTitle = "使命召唤4 · 现代战争重制版";
    string gameSubTitle = "Infinity Ward · 2007 年原作的画面重制";
    string intro =
        "《使命召唤4：现代战争》重制版，\n" +
        "现代战争系列的第一部，\n" +
        "经典单人战役的画面重制。\n" +
        "\n" +
        "跟着剧情在中东和东欧\n" +
        "执行一个个任务，\n" +
        "从新兵一路打到最后一枪。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给我们";
    string centerNote = "点下面的按钮，直接进游戏。";

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

    class LegendItem { public string Caption; public string Spec; }

    // ---------- 字体/布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand, fSub;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcStart, rcFolder, rcExit;

    [STAThread]
    static int Main(string[] rawArgs)
    {
        string[] args = Normalize(rawArgs);   // --xxx 归一成 /xxx，同时继续认 /xxx（规则 42）
        if (args.Length > 0 && (args[0] == "/selftest" || args[0] == "-selftest"))
            return SelfTest(Tail(args));
        if (args.Length > 0 && (args[0] == "/wer" || args[0] == "-wer"))
            return WerDump(Tail(args));
        foreach (string a in args)
            if (a == "/hideparent" || a == "-hideparent") HideParentConsole();
        foreach (string a in rawArgs)
            if (a.IndexOf(":\\") >= 0 || a.IndexOf("/Program Files/") >= 0)
                Log("【警告】参数疑似被 shell 路径转换：" + a);
        Application.EnableVisualStyles();
        Launcher l = new Launcher();
        foreach (string a in args)
            if (a == "/go" || a == "-go" || a == "/auto") l.autoStart = true;
        Log("启动参数：" + (rawArgs.Length == 0 ? "（无）" : string.Join(" ", rawArgs)) +
            "；零交互：" + (l.autoStart ? "是" : "否"));
        LogReadings();
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
        }
        Application.Run(l);
        return l.exitCode;
    }

    static string[] Normalize(string[] raw)
    {
        string[] a = new string[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            string s = raw[i];
            a[i] = (s.StartsWith("--") && s.Length > 2) ? "/" + s.Substring(2) : s;
        }
        return a;
    }

    static string[] Tail(string[] a)
    {
        string[] r = new string[Math.Max(0, a.Length - 1)];
        if (a.Length > 1) Array.Copy(a, 1, r, 0, a.Length - 1);
        return r;
    }

    public Launcher()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;   // 必须居中（规则 28）
        KeyPreview = true;
        BackColor = BgBottom;
        Text = "使命召唤4 重制版 · 启动器";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { Log("取自身图标失败，用系统默认图标"); }
        ClientSize = new Size(S(900), S(620));            // 尺寸一步到位，居中才不会偏
        RebuildLegend();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using (Graphics g = CreateGraphics()) { dpi = g.DpiX / 96f; }
        MakeFonts();
        Keycaps.Configure(dpi, fCap, fCapZh);             // 通用键帽库注入（硬约束 17）
        LayoutRects();
        if (autoStart) BeginInvoke(new Action(delegate { StartGame(); }));
    }

    int S(int v) { return (int)Math.Round(v * dpi); }

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
        fSub = new Font(ui, 9f * dpi, FontStyle.Regular);
    }

    static string PickFont(string[] want, string fallback)
    {
        try
        {
            using (InstalledFontCollection fc = new InstalledFontCollection())
                foreach (string name in want)
                    foreach (FontFamily f in fc.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return name;
        }
        catch { }
        return fallback;
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
        int groupH = S(48) + S(10) + S(34);
        int btnY = colCenter.Y + (colCenter.Height - groupH) / 2 + S(30);
        rcStart = new Rectangle(inner, btnY, innerW, S(48));
        rcFolder = new Rectangle(inner, btnY + S(48) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y,
            innerW - (rcFolder.Right + S(8) - inner), S(34));
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

    void DrawLeatherBg(Graphics g, Rectangle r)
    {
        using (LinearGradientBrush b = new LinearGradientBrush(r, BgTop, BgBottom, LinearGradientMode.Vertical))
            g.FillRectangle(b, r);
        using (Pen dark = new Pen(Color.FromArgb(12, 0, 0, 0), 1f))
            for (int x = -r.Height; x < r.Width; x += 9) g.DrawLine(dark, x, 0, x + r.Height, r.Height);
        using (Pen light = new Pen(Color.FromArgb(8, 255, 220, 160), 1f))
            for (int x = 0; x < r.Width + r.Height; x += 13) g.DrawLine(light, x, 0, x - r.Height, r.Height);
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

    void DrawHeader(Graphics g)
    {
        TextRenderer.DrawText(g, gameTitle, fTitle,
            new Point(colLeft.X + S(1), S(17)), Color.FromArgb(150, 0, 0, 0), Color.Transparent);
        TextRenderer.DrawText(g, gameTitle, fTitle, new Point(colLeft.X, S(16)), GoldBright, Color.Transparent);
        TextRenderer.DrawText(g, gameSubTitle, fSub, new Point(colLeft.X + S(1), S(48)), InkDim, Color.Transparent);
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
        y += S(38);
        DrawWrapped(g, intro, x, y, colLeft.Width - S(32), fBody, Ink);
    }

    void DrawCenter(Graphics g)
    {
        Card(g, colCenter);
        int x = colCenter.X + S(16), y = colCenter.Y + S(14);
        TextRenderer.DrawText(g, "准备开玩", fHead, new Point(x, y), Gold, Color.Transparent);
        y += S(38);
        DrawWrapped(g, centerNote, x, y, colCenter.Width - S(32), fBody, Ink);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true,
            hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true,
            hover == 2 || focus == 2, focus == 2);
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
        return DrawLegendRows(g, legend, x, y, top, bottom);
    }

    int DrawLegendRows(Graphics g, LegendItem[] rows, int x, int y, int top, int bottom)
    {
        int capH = S(24);
        int rightEdge = colRight.Right - S(16);
        foreach (LegendItem it in rows)
        {
            int rowH = capH + S(9);
            if (InViewport(y, capH, top, bottom))
            {
                Size cs = TextRenderer.MeasureText(g, it.Caption, fBody);
                TextRenderer.DrawText(g, it.Caption, fBody,
                    new Point(x, y + (capH - cs.Height) / 2), Ink, Color.Transparent);
                int kw = Keycaps.GroupWidth(g, it.Spec, capH);
                int kx = x + cs.Width + S(10);
                if (kx + kw > rightEdge)
                {
                    // 一行放不下就拆成两行，绝不把 cx 重置造成叠字
                    kx = x + S(10);
                    y += capH;
                    rowH += capH;
                }
                if (InViewport(y, capH, top, bottom))
                    Keycaps.DrawGroup(g, it.Spec, kx, y, capH, true);
            }
            y += rowH;
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

    void StatusDot(Graphics g, string text, Color c, int x, int y)
    {
        using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y + S(5), S(9), S(9));
        TextRenderer.DrawText(g, text, fBody, new Point(x + S(16), y), InkDim, Color.Transparent);
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

    // ---------- 键帽表：键位来自游戏自己的 players2\keys.cfg ----------
    void RebuildLegend()
    {
        legend = new LegendItem[]
        {
            Row("前后左右", "pc:W pc:A pc:S pc:D"),
            Row("跳跃", "pc:Space"),
            Row("奔跑", "pc:Shift"),
            Row("蹲下", "pc:C"),
            Row("趴下", "pc:Ctrl"),
            Row("换弹", "pc:R"),
            Row("互动", "pc:F"),
            Row("手雷", "pc:G"),
            Row("切换武器", "pc:1 pc:2"),
            Row("开火", "pc:Mouse1"),
            Row("瞄准", "pc:Mouse2"),
            Row("暂停菜单", "pc:Esc"),
        };
    }

    static LegendItem Row(string caption, string spec)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption; it.Spec = spec;
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
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcFolder);
            return Rectangle.Union(r, rcExit);
        }
    }

    void RepaintControls() { Invalidate(ButtonsRect); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; RepaintControls(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hover != -1) { hover = -1; Invalidate(); }
        base.OnMouseLeave(e);
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
        if (i == 0) StartGame();
        else if (i == 1) OpenLog();
        else if (i == 2) Close();
    }

    // ---------- 启动游戏 ----------
    void StartGame()
    {
        if (busy) return;
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序"; statusColor = Warn; Invalidate();
            Log("启动失败：找不到 " + gameExe);
            return;
        }
        busy = true;
        status = "正在启动游戏…";
        statusColor = Ok;
        Invalidate();
        Log("点「启动游戏」：" + gameExe + (autoStart ? "（零交互）" : ""));
        DateTime t0 = DateTime.Now;

        Thread t = new Thread(delegate ()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exePath);
                psi.WorkingDirectory = AppDir();
                psi.UseShellExecute = false;     // 游戏是启动器的直接子进程
                using (Process p = Process.Start(psi))
                {
                    Log("已启动游戏，pid=" + p.Id);
                    // 点启动后窗口保留可见（2026-11-12 用户定案：操作说明要一直留着，除非玩家自己点退出）；
                    // 只有玩家主动关窗才转托盘（OnFormClosing），游戏退出后本窗口自动关闭。
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    int secs = (int)(DateTime.Now - t0).TotalSeconds;
                    Log("游戏退出，退出码 0x" + ((uint)exitCode).ToString("X8") + "，用时 " + secs + " 秒");
                    if (exitCode != 0) CaptureCrash();
                }
                FinishWith(exitCode);
            }
            catch (Exception ex)
            {
                Log("启动失败：" + ex);
                exitCode = 1;
                BeginInvoke(new Action(delegate
                {
                    Show(); WindowState = FormWindowState.Normal;
                    status = "启动失败，请看运行日志"; statusColor = Warn; Invalidate();
                }));
            }
            finally { busy = false; }
        });
        t.IsBackground = true;
        t.Start();
    }

    void EnsureTray()
    {
        if (tray != null) return;
        tray = new NotifyIcon();
        tray.Icon = Icon;
        tray.Text = "使命召唤4 重制版 · 启动器（游戏运行中，双击唤回）";
        tray.Visible = true;
        tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; };
    }

    void FinishWith(int code)
    {
        finishing = true;
        BeginInvoke(new Action(delegate
        {
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            Close();
        }));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy && !finishing)
        {
            e.Cancel = true;
            Hide();                          // 游戏中关窗不杀游戏
            Log("关窗转后台，游戏继续运行");
            EnsureTray();
        }
        base.OnFormClosing(e);
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
        if (fSub != null) fSub.Dispose();
        base.OnFormClosed(e);
    }

    void OpenLog()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("notepad.exe");
            psi.Arguments = "\"" + LogPath() + "\"";
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex) { Log("打开运行日志失败：" + ex); }
    }

    static string AppDir() { return AppDomain.CurrentDomain.BaseDirectory; }
    static string LogPath() { return Path.Combine(AppDir(), "运行日志.txt"); }

    static void Log(string line)
    {
        try
        {
            string p = LogPath();
            if (File.Exists(p) && new FileInfo(p).Length > 200 * 1024)
            {
                string[] old = File.ReadAllLines(p, Encoding.UTF8);
                int keep = Math.Min(300, old.Length);
                string[] last = new string[keep];
                Array.Copy(old, old.Length - keep, last, 0, keep);
                File.WriteAllLines(p, last, new UTF8Encoding(false));
            }
            File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + "\r\n",
                new UTF8Encoding(false));
        }
        catch { }
    }

    // ---------- 环境读数：只记档，不改系统（规则 37 / 40） ----------
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    [DllImport("kernel32.dll")] static extern bool GetDiskFreeSpace(string root,
        out uint spc, out uint bps, out uint freeC, out uint totalC);

    static void LogReadings()
    {
        try
        {
            int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
            IntPtr dc = GetDC(IntPtr.Zero);
            int refresh = 0, dpiX = 0;
            try { refresh = GetDeviceCaps(dc, 116); dpiX = GetDeviceCaps(dc, 88); }
            finally { ReleaseDC(IntPtr.Zero, dc); }
            if (w <= 0 || h <= 0) Log("【警告】显示宽高读数不可信：" + w + "x" + h);
            if (refresh <= 0 || refresh > 1000) { Log("【警告】刷新率读数不可信：" + refresh + "，按 60 记档"); refresh = 60; }
            if (dpiX <= 0) { Log("【警告】DPI 读数不可信，按 96 记档"); dpiX = 96; }
            uint spc, bps, freeC, totalC;
            string disk = "磁盘几何：读取失败";
            if (GetDiskFreeSpace(Path.GetPathRoot(AppDir()), out spc, out bps, out freeC, out totalC))
            {
                bool sane = spc > 0 && (bps == 512 || bps == 1024 || bps == 2048 || bps == 4096)
                            && freeC <= totalC;
                disk = "磁盘几何：每扇区字节=" + bps + "，每簇扇数=" + spc +
                       "，可用簇=" + freeC + "，总簇=" + totalC + "，断言=" + (sane ? "通过" : "不可信");
            }
            Log("现场读数：桌面=" + w + "x" + h + "，刷新率=" + refresh + "Hz，DPI=" + dpiX + "；" + disk);
        }
        catch (Exception ex) { Log("现场读数失败：" + ex); }
    }

    // ---------- 崩溃取证：系统 WER 报告自动抄进运行日志 + 游戏目录 ----------
    static readonly string[] CodeNames = { "Exception Code", "异常代码" };
    static readonly string[] OffsetNames = { "Exception Offset", "异常偏移" };
    static readonly string[] ModuleNames = { "Fault Module Name", "故障模块名称" };

    static bool IsName(string got, string[] names)
    {
        foreach (string n in names)
            if (string.Equals(got, n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string ReadWerText(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        string u = Encoding.Unicode.GetString(b);
        if (u.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("Sig[") >= 0) return u;
        return Encoding.UTF8.GetString(b);
    }

    // 按「字段名字」认，不按 Sig 序号猜；中英文系统两套名字都认（规则 35）
    static string ParseWer(string path)
    {
        string text = ReadWerText(path);
        string code = null, offset = null, module = null, curName = null;
        foreach (string raw in text.Split('\n'))
        {
            string ln = raw.Trim().TrimEnd('\r');
            int eq = ln.IndexOf('=');
            if (eq < 1) continue;
            string k = ln.Substring(0, eq).Trim();
            string v = ln.Substring(eq + 1).Trim();
            if (k.EndsWith(".Name", StringComparison.OrdinalIgnoreCase)) { curName = v; continue; }
            if (k.EndsWith(".Value", StringComparison.OrdinalIgnoreCase) && curName != null)
            {
                if (IsName(curName, CodeNames)) code = v;
                else if (IsName(curName, OffsetNames)) offset = v;
                else if (IsName(curName, ModuleNames)) module = v;
                curName = null;
            }
        }
        if (code == null) return "崩溃捕获：报告里没读到异常码字段";
        return "崩溃捕获：异常码 0x" + code.ToUpperInvariant() +
               "，出错地址 +0x" + (offset ?? "未知") +
               "，故障模块 " + (module ?? "未知");
    }

    static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
            try { File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true); } catch { }
        foreach (string d in Directory.GetDirectories(from))
            CopyTree(d, Path.Combine(to, Path.GetFileName(d)));
    }

    static void CaptureCrash()
    {
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                       @"Microsoft\Windows\WER");
            string match = "AppCrash_" + Path.GetFileNameWithoutExtension(GameExe) + "_*";
            for (int retry = 0; retry < 4; retry++)     // 系统后台写报告，重试几秒再收
            {
                foreach (string sub in new[] { "ReportQueue", "ReportArchive" })
                {
                    string dir = Path.Combine(root, sub);
                    if (!Directory.Exists(dir)) continue;
                    foreach (string report in Directory.GetDirectories(dir, match))
                    {
                        foreach (string wer in Directory.GetFiles(report, "*.wer"))
                        {
                            Log(ParseWer(wer));
                            string dump = Path.Combine(AppDir(), "崩溃转储");
                            string dest = Path.Combine(dump, Path.GetFileName(report));
                            if (!Directory.Exists(dest)) CopyTree(report, dest);
                            Log("崩溃报告已存进游戏目录：" + Path.GetFileName(report));
                            return;
                        }
                    }
                }
                Thread.Sleep(1500);
            }
            Log("崩溃捕获：没抓到系统自动生成的崩溃报告");
        }
        catch (Exception ex) { Log("崩溃捕获失败：" + ex); }
    }

    // ---------- 不启动游戏的开关 ----------
    // game_starter.exe --selftest <程序> [参数…]：直接起了再回传它的退出码
    static int SelfTest(string[] args)
    {
        if (args.Length < 1) return 1;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = args[0];
            psi.Arguments = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "";
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
        }
        catch (Exception ex) { Log("自测失败：" + ex); return 2; }
    }

    // game_starter.exe --wer <报告.wer> <输出.txt>：解析结论另存 UTF-8，给回归脚本断言用
    static int WerDump(string[] args)
    {
        if (args.Length < 2) return 1;
        try
        {
            string text = ParseWer(args[0]);
            File.WriteAllText(args[1], text, new UTF8Encoding(false));
            return text.IndexOf("异常码 0x", StringComparison.Ordinal) >= 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(args[1], "解析失败：" + ex, new UTF8Encoding(false)); } catch { }
            return 2;
        }
    }

    static void HideParentConsole()
    {
        try
        {
            IntPtr h = GetConsoleWindow();
            if (h != IntPtr.Zero) ShowWindow(h, 0);
        }
        catch { }
    }

    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // 品牌自校验：编码字节 + SHA-256，exe 内不出现明文
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
