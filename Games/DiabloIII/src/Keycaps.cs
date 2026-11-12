using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ============================================================================
// 通用拟物键帽库 v1 —— PC / PS / Switch / Xbox 四平台，一套标准件直接拿来用。
//
// 唯一权威实现：仓库 templates\Keycaps.cs
// 各游戏把本文件整份复制到 Games\<游戏>\src\Keycaps.cs，
// build.ps1 的 csc 参数里加上它即可；不要在 Launcher.cs 里另写
// Keycap / PsGlyph / DrawFaceCircle 之类的临时画法。
// 规范与完整键位清单：.qoder\skills\legacy-launcher-gui\references\keycaps.md
//
// 画法基线（暴力摩托 Keycap）：圆角/圆形帽体 + 顶沿高光条 + 底部厚度阴影 +
// 黄铜描边；键帽里的字仍用 TextRenderer（ClearType），全窗口文字渲染一致。
// PC 键 = 矩形帽，PS / Switch / Xbox 面部键 = 圆形帽，肩键/系统键 = 宽矩形帽，
// 摇杆/十字键/触摸板 = 各自形状的帽。所有平台共用同一套立体画法，只是帽形与符号不同。
//
// id 格式：  "<平台>:<键名>"       平台 pc / ps / sw / xb
//   pc:W  pc:Space  pc:LeftShift  pc:Up  pc:F1  pc:Mouse1 ...
//   ps:cross  ps:L1  ps:stick_r  ps:options  ps:touchpad ...
//   sw:A  sw:ZR  sw:home  sw:stick_l ...
//   xb:A  xb:RT  xb:menu  xb:guide ...
// 一组键用空格分隔（"pc:W pc:A pc:S pc:D"）；组合键把 "+" 当独立 token
// （"ps:L1 + ps:cross"），画出来是两个独立键帽中间一个小加号。
// 空串 / "—" 表示这一行没有这个键，不画帽。
// ============================================================================

public static class Keycaps
{
    // ---------- 宿主注入：构造时调一次 Configure ----------
    public static float Dpi = 1f;
    public static Font Font;        // 键帽字（9pt Bold）
    public static Font FontSmall;   // 窄帽 / 长标签（8.5pt Bold）

    // 皮革令牌（与 leather-recipes.md 一致，宿主有自定义令牌时可自行覆盖）
    public static Color Face     = Color.FromArgb(0x4A, 0x2A, 0x1B);
    public static Color Side     = Color.FromArgb(0x1C, 0x0F, 0x0A);
    public static Color Edge     = Color.FromArgb(0xB9, 0x8A, 0x55);
    public static Color Hi       = Color.FromArgb(0x9C, 0x6A, 0x3B);
    public static Color Ink      = Color.FromArgb(0xF4, 0xE3, 0xC6);
    public static Color Gold     = Color.FromArgb(0xF0, 0xD5, 0x96);
    public static Color GoldDim  = Color.FromArgb(0x9A, 0x6A, 0x2A);
    public static Color OnGold   = Color.FromArgb(0x2A, 0x16, 0x0D);

    // 平台官方配色
    static readonly Color PsSquare   = Color.FromArgb(0xEA, 0x9A, 0xC4);
    static readonly Color PsTriangle = Color.FromArgb(0x8F, 0xD9, 0xA6);
    static readonly Color PsCross    = Color.FromArgb(0x7C, 0xC3, 0xEE);
    static readonly Color PsCircle   = Color.FromArgb(0xE8, 0x7B, 0x7B);
    static readonly Color XbA        = Color.FromArgb(0x6D, 0xCB, 0x5A);
    static readonly Color XbB        = Color.FromArgb(0xE2, 0x57, 0x4C);
    static readonly Color XbX        = Color.FromArgb(0x4F, 0xA9, 0xE8);
    static readonly Color XbY        = Color.FromArgb(0xEF, 0xC7, 0x5E);
    static readonly Color SwBlue     = Color.FromArgb(0x00, 0xC3, 0xE3);
    static readonly Color SwRed      = Color.FromArgb(0xFF, 0x45, 0x54);

    public static void Configure(float dpi, Font font, Font small)
    {
        Dpi = dpi; Font = font; FontSmall = small;
    }

    static int S(int v) { return (int)Math.Round(v * Dpi); }

    // ---------- 形状与内容 ----------
    enum Shape { Rect, Round, Dpad, Stick, Touch }
    struct Def
    {
        public string Label;
        public Shape Shape;
        public int Symbol;      // 0 文字 1 □ 2 △ 3 ✕ 4 ○ 5↑ 6↓ 7← 8→ 9 十字键 10 摇杆 11 触摸板
        public Color Accent;    // Color.Empty = 用帽面文字色
    }

    static readonly string[,] PcAlias =
    {
        { "Space", "空格" }, { "LeftShift", "左Shift" }, { "RightShift", "右Shift" },
        { "Shift", "Shift" }, { "Ctrl", "Ctrl" }, { "Alt", "Alt" },
        { "Enter", "Enter" }, { "Esc", "Esc" }, { "Tab", "Tab" },
        { "CapsLock", "Caps" }, { "Backspace", "退格" }, { "Insert", "Insert" },
        { "Delete", "Delete" }, { "Home", "Home" }, { "End", "End" },
        { "PageUp", "PgUp" }, { "PageDown", "PgDn" }, { "PrintScreen", "PrtSc" },
        { "Pause", "Pause" },         { "Mouse1", "左键" }, { "Mouse2", "右键" },
        { "Mouse3", "中键" }, { "Mouse4", "前进侧键" }, { "Mouse5", "后退侧键" }, { "WheelUp", "滚轮↑" }, { "WheelDown", "滚轮↓" },
        { "Minus", "-" }, { "Plus", "+" }, { "Comma", "," }, { "Period", "." },
        { "Slash", "/" }, { "Semicolon", ";" }, { "BracketL", "[" }, { "BracketR", "]" }
    };

    static string Alias(string key)
    {
        for (int i = 0; i < PcAlias.GetLength(0); i++)
            if (PcAlias[i, 0] == key) return PcAlias[i, 1];
        return key;
    }

    static Def Parse(string id)
    {
        Def d = new Def();
        d.Label = id; d.Shape = Shape.Rect; d.Symbol = 0; d.Accent = Color.Empty;
        int c = id.IndexOf(':');
        string plat = c < 0 ? "pc" : id.Substring(0, c);
        string key = c < 0 ? id : id.Substring(c + 1);

        if (plat == "ps")
        {
            switch (key)
            {
                case "square":   d.Symbol = 1; d.Accent = PsSquare;   d.Shape = Shape.Round; d.Label = ""; break;
                case "triangle": d.Symbol = 2; d.Accent = PsTriangle; d.Shape = Shape.Round; d.Label = ""; break;
                case "cross":    d.Symbol = 3; d.Accent = PsCross;    d.Shape = Shape.Round; d.Label = ""; break;
                case "circle":   d.Symbol = 4; d.Accent = PsCircle;   d.Shape = Shape.Round; d.Label = ""; break;
                case "dpad":     d.Symbol = 9;  d.Shape = Shape.Dpad;  d.Label = ""; break;
                case "dpad_up":  d.Symbol = 5; d.Label = ""; break;
                case "dpad_down":d.Symbol = 6; d.Label = ""; break;
                case "dpad_left":d.Symbol = 7; d.Label = ""; break;
                case "dpad_right":d.Symbol = 8; d.Label = ""; break;
                case "stick_l":  d.Symbol = 10; d.Shape = Shape.Stick; d.Label = "L3"; break;
                case "stick_r":  d.Symbol = 10; d.Shape = Shape.Stick; d.Label = "R3"; break;
                case "touchpad": d.Symbol = 11; d.Shape = Shape.Touch; d.Label = ""; break;
                case "options":  d.Label = "OPTIONS"; break;
                case "share":    d.Label = "SHARE"; break;
                case "ps":       d.Label = "PS"; break;
                default:         d.Label = key; break;   // L1 L2 R1 R2 L3 R3 START SELECT
            }
            return d;
        }
        if (plat == "sw")
        {
            switch (key)
            {
                case "A": case "B": case "X": case "Y":
                    d.Shape = Shape.Round; d.Label = key; break;
                case "L": case "R": case "ZL": case "ZR": case "SL": case "SR":
                    d.Label = key; d.Accent = key.StartsWith("Z") ? SwRed : SwBlue; break;
                case "plus":   d.Label = "+"; d.Shape = Shape.Round; break;
                case "minus":  d.Label = "−"; d.Shape = Shape.Round; break;
                case "home":   d.Label = "HOME"; d.Shape = Shape.Round; break;
                case "capture":d.Label = "□"; d.Shape = Shape.Round; break;
                case "stick_l":case "stick_r": d.Symbol = 10; d.Shape = Shape.Stick; d.Label = ""; break;
                case "dpad":   d.Symbol = 9; d.Shape = Shape.Dpad; d.Label = ""; break;
                default:       d.Label = key; break;
            }
            return d;
        }
        if (plat == "xb")
        {
            switch (key)
            {
                case "A": d.Accent = XbA; d.Shape = Shape.Round; d.Label = "A"; break;
                case "B": d.Accent = XbB; d.Shape = Shape.Round; d.Label = "B"; break;
                case "X": d.Accent = XbX; d.Shape = Shape.Round; d.Label = "X"; break;
                case "Y": d.Accent = XbY; d.Shape = Shape.Round; d.Label = "Y"; break;
                case "stick_l": case "stick_r": d.Symbol = 10; d.Shape = Shape.Stick; d.Label = ""; break;
                case "dpad": d.Symbol = 9; d.Shape = Shape.Dpad; d.Label = ""; break;
                case "view": d.Label = "VIEW"; break;
                case "menu": d.Label = "MENU"; break;
                case "guide":d.Label = "XBOX"; d.Shape = Shape.Round; break;
                default: d.Label = key; break;   // LB RB LT RT
            }
            return d;
        }

        // PC 键盘
        switch (key)
        {
            case "Up":    d.Symbol = 5; d.Label = ""; break;
            case "Down":  d.Symbol = 6; d.Label = ""; break;
            case "Left":  d.Symbol = 7; d.Label = ""; break;
            case "Right": d.Symbol = 8; d.Label = ""; break;
            default:      d.Label = Alias(key); break;
        }
        if (d.Label.Length > 2) d.Shape = Shape.Rect;
        return d;
    }

    // ---------- 尺寸 ----------
    public static string[] SplitKeys(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return new string[0];
        return spec.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool IsNone(string id) { return string.IsNullOrEmpty(id) || id == "—"; }
    static bool IsPlus(string id) { return id == "+"; }

    static Font FontFor(string label)
    {
        if (label.Length > 2 && FontSmall != null) return FontSmall;
        return Font != null ? Font : SystemFonts.DefaultFont;
    }

    static int OneWidth(Graphics g, string id, int h)
    {
        if (IsNone(id)) return S(26);
        if (IsPlus(id)) return S(10);
        Def d = Parse(id);
        if (d.Shape == Shape.Round || d.Shape == Shape.Stick) return h;
        if (d.Shape == Shape.Touch) return S(40);
        if (d.Shape == Shape.Dpad) return S(26);
        if (d.Symbol != 0) return S(22);
        return Math.Max(S(22), TextRenderer.MeasureText(g, d.Label, FontFor(d.Label)).Width + S(12));
    }

    public static int GroupWidth(Graphics g, string spec, int h)
    {
        int w = 0;
        foreach (string id in SplitKeys(spec))
        {
            if (w > 0) w += S(3);
            w += OneWidth(g, id, h);
        }
        return w;
    }

    // ---------- 绘制 ----------
    public static void DrawGroup(Graphics g, string spec, int x, int y, int h, bool primary)
    {
        int cx = x;
        foreach (string id in SplitKeys(spec))
        {
            int w = OneWidth(g, id, h);
            if (IsPlus(id))
            {
                TextRenderer.DrawText(g, "+", FontFor("+"), new Rectangle(cx, y, w, h),
                    Gold, Color.Transparent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else if (!IsNone(id))
            {
                Draw(g, id, new Rectangle(cx, y, w, h), primary);
            }
            cx += w + S(3);
        }
    }

    public static void Draw(Graphics g, string id, Rectangle r, bool primary)
    {
        Def d = Parse(id);
        Color face = primary ? Gold : Face;
        Color edge = primary ? GoldDim : Edge;
        Color fore = primary ? OnGold : Ink;
        Color side = primary ? GoldDim : Side;
        Shell(g, r, d.Shape, face, side, edge, primary);

        if (d.Symbol != 0) Symbol(g, d, r, primary, fore);
        else if (d.Label.Length > 0)
        {
            Color c = d.Accent.IsEmpty ? fore : d.Accent;
            TextRenderer.DrawText(g, d.Label, FontFor(d.Label),
                new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), c, Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // 所有平台共用：帽体 + 顶沿高光 + 底部厚度 + 描边（暴力摩托 Keycap 的立体画法）
    static void Shell(Graphics g, Rectangle r, Shape shape, Color face, Color side, Color edge, bool primary)
    {
        using (GraphicsPath p = (shape == Shape.Round || shape == Shape.Stick) ? Ellipse(r) : Round(r, S(4)))
        {
            using (SolidBrush b = new SolidBrush(face)) g.FillPath(b, p);
            using (Pen pen = new Pen(edge, 1f)) g.DrawPath(pen, p);
        }
        using (GraphicsPath hi = Round(new Rectangle(r.X + S(3), r.Y + S(1), r.Width - S(6), S(3)), S(2)))
        using (SolidBrush hb = new SolidBrush(primary ? Gold : Hi)) g.FillPath(hb, hi);
        using (SolidBrush sb = new SolidBrush(side))
            g.FillRectangle(sb, r.X + S(3), r.Bottom - S(3), r.Width - S(6), S(2));
    }

    static void Symbol(Graphics g, Def d, Rectangle r, bool primary, Color fore)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        float s = Math.Min(r.Width, r.Height) * 0.28f;
        Color c = d.Accent.IsEmpty ? (primary ? OnGold : Ink) : d.Accent;
        using (Pen pen = new Pen(c, Math.Max(1.6f, S(2) * 0.8f)))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            switch (d.Symbol)
            {
                case 1: g.DrawRectangle(pen, cx - s, cy - s, s * 2f, s * 2f); return;
                case 2:
                    g.DrawPolygon(pen, new PointF[] {
                        new PointF(cx, cy - s * 1.15f),
                        new PointF(cx + s * 1.15f, cy + s * 0.85f),
                        new PointF(cx - s * 1.15f, cy + s * 0.85f) }); return;
                case 3:
                    g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                    g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s); return;
                case 4: g.DrawEllipse(pen, cx - s, cy - s, s * 2f, s * 2f); return;
                case 5: Arrow(g, pen, cx, cy, s, 0); return;
                case 6: Arrow(g, pen, cx, cy, s, 1); return;
                case 7: Arrow(g, pen, cx, cy, s, 2); return;
                case 8: Arrow(g, pen, cx, cy, s, 3); return;
                case 9:
                    g.DrawRectangle(pen, cx - s * 0.4f, cy - s, s * 0.8f, s * 2f);
                    g.DrawRectangle(pen, cx - s, cy - s * 0.4f, s * 2f, s * 0.8f); return;
                case 10:
                    g.DrawEllipse(pen, cx - s, cy - s, s * 2f, s * 2f);
                    g.DrawEllipse(pen, cx - s * 0.55f, cy - s * 0.55f, s * 1.1f, s * 1.1f);
                    return;
                case 11:
                    using (Pen thin = new Pen(c, Math.Max(1f, S(1))))
                        g.DrawRectangle(thin, cx - r.Width * 0.36f, cy - s * 0.7f,
                            r.Width * 0.72f, s * 1.4f);
                    return;
            }
        }
    }

    static void Arrow(Graphics g, Pen pen, float cx, float cy, float s, int dir)
    {
        float dx = (dir == 3) ? 1f : (dir == 2 ? -1f : 0f);
        float dy = (dir == 1) ? 1f : (dir == 0 ? -1f : 0f);
        PointF tip = new PointF(cx + dx * s, cy + dy * s);
        PointF tail = new PointF(cx - dx * s * 0.6f, cy - dy * s * 0.6f);
        g.DrawLine(pen, tip, tail);
        float px = -dy * s * 0.7f, py = dx * s * 0.7f;
        g.DrawLine(pen, tip, new PointF(cx + px, cy + py));
        g.DrawLine(pen, tip, new PointF(cx - px, cy - py));
    }

    static GraphicsPath Round(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static GraphicsPath Ellipse(Rectangle r)
    {
        GraphicsPath p = new GraphicsPath();
        p.AddEllipse(r);
        return p;
    }
}
