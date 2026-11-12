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
using Microsoft.Win32;

// 使命召唤6 现代战争2 重制版（MW2CR）语言切换启动器 game_starter.exe
// 界面规范见仓库 .qoder/skills/legacy-launcher-gui/SKILL.md：纯 GDI+ 自绘、三栏 900×620、拟物皮革。
// 布局：左栏游戏简介 / 中栏自绘语言下拉框（默认简体）+ 启动按钮 / 右栏键帽操作说明。
// 语言开关有两处，点「启动游戏」时都写：
//   1) 注册表 HKCU\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR  LOCALE = zhCN/zhTW/enUS
//      （游戏本体读它，社区通行做法，3DM 语言替换方案）
//   2) 游戏目录 Launcher.db（4 字节纯文本 enUS/zhCN/zhTW，离线安装器/部分版本读它）
// 平台托管契约：游戏是直接子进程、启动器陪跑、退出码原样回传、关窗隐藏不杀游戏。
// Build:
//   csc -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001
//       -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:game_starter.exe Launcher.cs

class Launcher : Form
{
    // ---------- 设计令牌（拟物皮革，取自 leather-recipes.md §0） ----------
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

    string gameTitle = "使命召唤6 · 现代战争2 重制版";
    string gameExe = "MW2CR.exe";
    string intro =
        "《使命召唤6：现代战争2》重制版，\n" +
        "经典第一人称射击游戏的画面重制。\n" +
        "\n" +
        "用选中的语言直接进游戏，不用进战网，\n" +
        "也不用自己找设置。\n" +
        "\n" +
        "# 玩不了或报错\n" +
        "点「打开运行日志」\n" +
        "把内容发给我们";

    // ---------- 语言选项（下拉框内容） ----------
    class Lang
    {
        public string Label;
        public string Code;
        public Lang(string l, string c) { Label = l; Code = c; }
    }
    static readonly Lang[] Langs =
    {
        new Lang("简体中文", "zhCN"),
        new Lang("繁體中文", "zhTW"),
        new Lang("English 英文", "enUS"),
    };
    static string[] LangNames()
    {
        string[] a = new string[Langs.Length];
        for (int i = 0; i < Langs.Length; i++) a[i] = Langs[i].Label;
        return a;
    }

    // ---------- 运行状态 ----------
    const int NoFocus = -99;
    int mode = 0;                    // 默认第 1 档：简体中文
    int hover = -1, focus = NoFocus;
    int rightScroll, rightScrollMax;
    bool busy, finishing;
    string status = "已就绪";
    Color statusColor = Ok;
    LegendItem[][] legend = new LegendItem[0][];
    volatile int exitCode;
    NotifyIcon tray;
    bool autoStart;

    class LegendItem { public string Caption; public string[] Caps; public string Note; }

    // ---------- 字体/布局 ----------
    Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
    float dpi = 1f;
    Rectangle colLeft, colCenter, colRight;
    Rectangle rcLabelMode, rcComboMode, rcStart, rcFolder, rcExit;
    SelectPanel selLang;
    ContextMenuStrip langMenu;

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
        {
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;
            string aa = a.ToLowerInvariant();
            if (aa == "/zhcn" || aa == "--zhcn") l.mode = 0;
            else if (aa == "/zhtw" || aa == "--zhtw") l.mode = 1;
            else if (aa == "/enus" || aa == "--enus") l.mode = 2;
        }
        Log("启动参数：" + (args.Length == 0 ? "（无）" : string.Join(" ", args)) +
            "；零交互：" + (l.autoStart ? "是" : "否") +
            "；选择语言：" + Langs[l.mode].Code +
            "；当前语言：" + ReadLocale());
        if (!BrandGuard.Verify())
        {
            MessageBox.Show("启动器文件被修改，请重新解压。", "启动器");
            return 3;
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

    public Launcher()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;   // 必须居中
        KeyPreview = true;
        BackColor = BgBottom;
        Text = "使命召唤6 重制版 · 启动器";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { Log("取自身图标失败，用系统默认图标"); }
        ClientSize = new Size(S(900), S(620));
        RebuildLegend();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using (Graphics g = CreateGraphics()) { dpi = g.DpiX / 96f; }
        MakeFonts();
        LayoutRects();
        if (selLang == null)
        {
            selLang = MakeLanguageSelect();
            PlaceSelector(selLang, rcComboMode);   // 新建后必须定位，否则停在 (0,0) 浮到标题栏
        }
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
    }

    static string PickFont(string[] want, string fallback)
    {
        try
        {
            using (InstalledFontCollection fc = new InstalledFontCollection())
            {
                foreach (string name in want)
                    foreach (FontFamily f in fc.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return name;
            }
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
        int btnY = colCenter.Bottom - S(14) - S(34) - S(10) - S(44);
        rcStart = new Rectangle(inner, btnY, innerW, S(44));
        rcFolder = new Rectangle(inner, btnY + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
        rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y,
            innerW - (rcFolder.Right + S(8) - inner), S(34));
        // 下拉框与「启动游戏」按钮之间留 S(30) 空隙，不再贴在一起
        int comboH = S(34);
        rcComboMode = new Rectangle(inner, btnY - S(30) - comboH, innerW, comboH);
        rcLabelMode = new Rectangle(inner, rcComboMode.Y - S(30), innerW, S(22));
        PlaceSelector(selLang, rcComboMode);
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
        string cur = "当前语言：" + LangName(ReadLocale());
        Size sz = TextRenderer.MeasureText(g, cur, fBody);
        TextRenderer.DrawText(g, cur, fBody,
            new Point(ClientSize.Width - S(20) - sz.Width, S(22)), InkDim, Color.Transparent);
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

        TextRenderer.DrawText(g, "游戏语言", fHead, new Point(rcLabelMode.X, rcLabelMode.Y), Gold, Color.Transparent);

        DrawButton(g, rcStart, busy ? "游戏运行中…" : "启动游戏", true, !busy,
            hover == 0 || focus == 0, focus == 0);
        DrawButton(g, rcFolder, "打开运行日志", false, true,
            hover == 1 || focus == 1, focus == 1);
        DrawButton(g, rcExit, "退出", false, true,
            hover == 2 || focus == 2, focus == 2);
        if (focus == 3 && selLang != null) FocusRing(g, selLang.Bounds);
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
                        TextRenderer.DrawText(g, item.Note, fBody,
                            new Point(cx, y + (capH - ns.Height) / 2), Ink, Color.Transparent);
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
            bottom = hot ? CapFace : CardBottom;
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

    // ---------- 键帽表（FPS 常用键位；鼠标不是键盘键，写普通文字） ----------
    void RebuildLegend()
    {
        legend = new LegendItem[][]
        {
            new[] { Item("前后左右", "W", "A", "S", "D") },
            new[] { Item("跳跃", "空格") },
            new[] { Item("奔跑", "Shift") },
            new[] { Item("蹲下", "Ctrl") },
            new[] { Item("换弹", "R") },
            new[] { PlainItem("开火", "鼠标左键") },
            new[] { PlainItem("瞄准", "鼠标右键") },
            new[] { Item("切换武器", "1", "2") },
            new[] { Item("暂停菜单", "Esc") },
        };
    }

    static LegendItem Item(string caption, params string[] caps)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption; it.Caps = caps;
        return it;
    }

    static LegendItem PlainItem(string caption, string note)
    {
        LegendItem it = new LegendItem();
        it.Caption = caption; it.Caps = new string[0]; it.Note = note;
        return it;
    }

    // ---------- 输入 ----------
    int HitTest(Point pt)
    {
        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcFolder.Contains(pt)) return 1;
        if (rcExit.Contains(pt)) return 2;
        if (!busy && selLang != null && selLang.Bounds.Contains(pt)) return 3;
        return -1;
    }

    Rectangle ButtonsRect
    {
        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcFolder);
            r = Rectangle.Union(r, rcExit);
            if (selLang != null) r = Rectangle.Union(r, selLang.Bounds);
            return r;
        }
    }

    void RepaintControls() { Invalidate(ButtonsRect); }

    static void PlaceSelector(SelectPanel p, Rectangle slot)
    {
        if (p == null) return;
        p.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);
    }

    SelectPanel MakeLanguageSelect()
    {
        SelectPanel p = new SelectPanel();
        p.BackColor = Color.Transparent;
        p.ForeColor = Ink;
        p.Font = fRowSel;
        p.Text = Langs[mode].Label;
        p.Cursor = Cursors.Hand;
        p.Paint += PaintLanguageSelect;
        p.Click += delegate { ShowLangMenu(); };
        Controls.Add(p);
        return p;
    }

    void PaintLanguageSelect(object sender, PaintEventArgs e)
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

    void ShowLangMenu()
    {
        if (busy) return;
        if (langMenu == null) BuildLangMenu();
        string[] names = LangNames();
        for (int i = 0; i < langMenu.Items.Count; i++)
            langMenu.Items[i].Text = (i == mode ? "\u2713  " : "    ") + names[i];
        FitMenuSize(langMenu, names);
        langMenu.Show(selLang, new Point(0, selLang.Height + S(2)));
        Log("下拉高度自检：项数=" + names.Length + "，弹层高=" + langMenu.Height +
            "，能装下=" + (langMenu.Height >= names.Length * langMenu.Items[0].Height));
    }

    void StepMode(int delta)
    {
        int n = Langs.Length;
        mode = ((mode + delta) % n + n) % n;
        selLang.Text = Langs[mode].Label;
        selLang.Invalidate();
        Log("选择语言：" + Langs[mode].Code);
    }

    void BuildLangMenu()
    {
        langMenu = new ContextMenuStrip();
        langMenu.ShowImageMargin = false;
        langMenu.ShowCheckMargin = false;
        langMenu.BackColor = LeatherMenuRenderer.MenuBack;
        langMenu.ForeColor = Ink;
        langMenu.Font = fRowSel;
        langMenu.Padding = new Padding(S(4));
        langMenu.Renderer = new LeatherMenuRenderer();
        for (int i = 0; i < Langs.Length; i++)
        {
            int idx = i;
            ToolStripMenuItem item = new ToolStripMenuItem(Langs[i].Label);
            item.ForeColor = Ink;
            item.Padding = new Padding(S(10), S(4), S(10), S(4));
            item.Click += delegate
            {
                mode = idx;
                selLang.Text = Langs[idx].Label;
                selLang.Invalidate();
                Log("选择语言：" + Langs[idx].Code);
            };
            langMenu.Items.Add(item);
        }
    }

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
        int cap = Screen.FromControl(selLang).WorkingArea.Width - S(24);
        menu.MinimumSize = new Size(Math.Max(selLang.Width, Math.Min(want, cap)), 0);
        Log("下拉宽度自检：框宽=" + selLang.Width + "，最长项文字宽=" + widest +
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
            using (SolidBrush b = new SolidBrush(MenuBack)) e.Graphics.FillRectangle(b, e.AffectedBounds);
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
        if (i == 3) { ShowLangMenu(); return; }
        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenLog(); return; }
        if (i == 2) { Close(); }
    }

    // ---------- 语言切换 ----------
    const string LKey = @"Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR";

    static string ReadLocale()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(LKey))
            {
                if (k != null)
                {
                    object v = k.GetValue("LOCALE");
                    if (v != null) return v.ToString();
                }
            }
        }
        catch { }
        try
        {
            string p = Path.Combine(AppDir(), "Launcher.db");
            if (File.Exists(p))
            {
                string s = File.ReadAllText(p).Trim();
                if (s.Length > 0) return s;
            }
        }
        catch { }
        return "enUS";
    }

    static string LangName(string code)
    {
        foreach (Lang l in Langs) if (string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l.Label;
        return code;
    }

    // 两处语言开关都写：注册表（游戏本体读）+ Launcher.db（安装器/部分版本读）
    static bool ApplyLocale(string code, out string msg)
    {
        msg = null;
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(LKey, true))
            {
                if (k == null) { msg = "无法写入语言设置（注册表不可写）"; return false; }
                k.SetValue("LOCALE", code, RegistryValueKind.String);
            }
        }
        catch (Exception ex) { msg = "无法写入语言设置：" + ex.Message; return false; }
        try
        {
            string p = Path.Combine(AppDir(), "Launcher.db");
            File.WriteAllText(p, code, new ASCIIEncoding());   // 4 字节纯文本，无 BOM 无换行
        }
        catch (Exception ex) { msg = "语言已写入，但安装器标记更新失败：" + ex.Message; }
        return true;
    }

    // ---------- 启动游戏 ----------
    void StartGame()
    {
        if (busy) return;
        string code = Langs[mode].Code;
        string msg;
        if (!ApplyLocale(code, out msg))
        {
            status = msg; statusColor = Warn; Invalidate();
            Log("语言切换失败：" + msg);
            return;
        }
        Log("语言切换为 " + code + (msg != null ? "（" + msg + "）" : ""));
        string exePath = Path.Combine(AppDir(), gameExe);
        if (!File.Exists(exePath))
        {
            status = "找不到游戏程序 " + gameExe; statusColor = Warn; Invalidate();
            return;
        }
        busy = true;
        status = "正在启动 " + LangName(code) + "…";
        statusColor = Ok;
        Invalidate();
        Log("点「启动游戏」：" + Langs[mode].Label + "，语言=" + code);

        Thread t = new Thread(delegate ()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exePath);
                psi.WorkingDirectory = AppDir();
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi))
                {
                    // 点启动后窗口保留可见（2026-11-12 用户定案：操作说明要一直留着，除非玩家自己点退出）；
                    // 只有玩家主动关窗才转托盘（OnFormClosing），游戏退出后本窗口自动关闭。
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    Log("游戏退出，退出码 0x" + ((uint)exitCode).ToString("X8"));
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
                    status = "启动失败：" + ex.Message; statusColor = Warn; Invalidate();
                }));
            }
            finally
            {
                busy = false;
            }
        });
        t.IsBackground = true;
        t.Start();
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
            if (tray == null)
            {
                tray = new NotifyIcon();
                tray.Icon = Icon;
                tray.Text = "使命召唤6 · 启动器（游戏运行中，双击唤回）";
                tray.Visible = true;
                tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; };
            }
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
        base.OnFormClosed(e);
    }

    void OpenLog()
    {
        try { Process.Start(new ProcessStartInfo("notepad.exe", LogPath())); }
        catch { }
    }

    static string AppDir() { return AppDomain.CurrentDomain.BaseDirectory; }
    static string LogPath() { return Path.Combine(AppDir(), "运行日志.txt"); }

    static void Log(string line)
    {
        try
        {
            string s = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + "\r\n";
            File.AppendAllText(LogPath(), s, new UTF8Encoding(false));
        }
        catch { }
    }

    // 自检：game_starter.exe /selftest <程序> [参数]，按当前设置启动并回传退出码（不切语言）
    static int SelfTest(string[] args)
    {
        if (args.Length < 1) return 1;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(args[0], string.Join(" ", args, 1, args.Length - 1));
            psi.WorkingDirectory = AppDir();
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
        }
        catch { return 2; }
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
