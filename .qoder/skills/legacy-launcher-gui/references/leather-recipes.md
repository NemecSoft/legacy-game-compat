# 皮革启动器绘制配方（WinForms / .NET Framework，抄了就能用）

单文件 `Launcher.cs`，`class Launcher : Form`，纯 GDI+ 自绘，**零图片、零第三方库、零内嵌字体**。
本文件是「拟物皮革」的唯一施工依据：新游戏直接照抄下面的代码块，**不要再从旧游戏的中国红版本反推**。

- 已按本配方交付的样板：`Games\OnimushaWotS\src\Launcher.cs`、`Games\GodofWarIII\src\Launcher.cs`、
  `Games\Heroes3\src\Launcher.cs`（六档下拉 + 换层）。
- 已经发出去的**中国红** exe 不返工；新做 / 重做的一律皮革（AGENTS 规则 10）。
- 体积红线：exe ≤ 60 KB（`scripts\check-launcher.ps1` 会量）。
- 文字渲染规则（2026-11-01 定，仍然有效）：**全窗口统一 `TextRenderer`（GDI ClearType）**；
  键帽只是形状（圆角 + 高光 + 阴影），不要为了键帽引入灰度 / 离屏位图 / 特殊抗锯齿。

## 0. 骨架：双缓冲 + 令牌 + DPI

```csharp
// ---------- 设计令牌（拟物皮革） ----------
static readonly Color BgTop        = Color.FromArgb(0x3A, 0x20, 0x15);  // 背景渐变上端
static readonly Color BgMid        = Color.FromArgb(0x2C, 0x18, 0x10);
static readonly Color BgBottom     = Color.FromArgb(0x1C, 0x0F, 0x0A);  // 近黑棕，不是纯黑
static readonly Color CardTop      = Color.FromArgb(0x5A, 0x34, 0x21);  // 卡片渐变上端
static readonly Color CardBottom   = Color.FromArgb(0x3B, 0x21, 0x16);
static readonly Color BandTop      = Color.FromArgb(0x24, 0x13, 0x0D);  // 页眉/页脚带
static readonly Color BandBottom   = Color.FromArgb(0x3A, 0x20, 0x15);
static readonly Color PanelEdge    = Color.FromArgb(0xB9, 0x8A, 0x55);  // 黄铜描边
static readonly Color Gold         = Color.FromArgb(0xE3, 0xC0, 0x7A);  // 栏标题 / 主按钮底
static readonly Color GoldBright   = Color.FromArgb(0xF0, 0xD5, 0x96);  // 烫金：主标题、品牌、焦点
static readonly Color GoldDim      = Color.FromArgb(0x9A, 0x6A, 0x2A);  // 分隔线、主按钮渐变下端
static readonly Color Ink          = Color.FromArgb(0xF4, 0xE3, 0xC6);  // 正文米黄（对比度 >= 4.5:1）
static readonly Color InkDim       = Color.FromArgb(0xD8, 0xBD, 0x96);  // 次要说明
static readonly Color OnGold       = Color.FromArgb(0x2A, 0x16, 0x0D);  // 金底上的字（深棕，不用黑）
static readonly Color CapFace      = Color.FromArgb(0x4A, 0x2A, 0x1B);  // 键帽渐变上端
static readonly Color CapTopHi     = Color.FromArgb(0x9C, 0x6A, 0x3B);  // 键帽上沿高光
static readonly Color CapSide      = Color.FromArgb(0x1C, 0x0F, 0x0A);  // 键帽渐变下端 / 阴影
static readonly Color LeatherDark  = Color.FromArgb(0x1C, 0x0F, 0x0A);  // 卡片外框 1.5px
static readonly Color LeatherLight = Color.FromArgb(0x6F, 0x4A, 0x2C);  // 主按钮描边
static readonly Color Stitch       = Color.FromArgb(0xB9, 0x8A, 0x55);  // 黄铜缝线
static readonly Color Ok           = Color.FromArgb(0xD9, 0xA4, 0x41);  // 就绪（黄铜）
static readonly Color Warn         = Color.FromArgb(0xE0, 0x7A, 0x5F);  // 警告（陶土红）

public Launcher()
{
    DoubleBuffered = true;                       // 必须，否则自绘会闪
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
           | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    KeyPreview = true;                           // 自绘控件也要吃键盘
    BackColor = BgBottom;
    ClientSize = new Size(S(900), S(620));       // 基准 96 DPI，三栏宽度固定
}

// 字体只 new 一次；OnPaint 里 new Font 会漏 GDI 句柄
Font fTitle, fHead, fRowSel, fRow, fBody, fCap, fCapZh, fMono, fBrand;
float dpi = 1f;
protected override void OnHandleCreated(EventArgs e)
{
    base.OnHandleCreated(e);
    using (Graphics g = CreateGraphics()) { dpi = g.DpiX / 96f; }
    MakeFonts();
    LayoutRects();
}
void MakeFonts()
{
    string ui = PickFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun" },
                         SystemFonts.MessageBoxFont.Name);
    string mono = PickFont(new[] { "Consolas", "Courier New" }, "Courier New");
    fTitle  = new Font(ui, 15f  * dpi, FontStyle.Bold);      // 主标题（烫金 + 1px 压印）
    fHead   = new Font(ui, 10.5f * dpi, FontStyle.Bold);     // 栏标题（Gold）
    fRowSel = new Font(ui, 10f  * dpi, FontStyle.Bold);      // 选中项 / 下拉当前行
    fRow    = new Font(ui, 10f  * dpi, FontStyle.Regular);   // 未选中项（InkDim）
    fBody   = new Font(ui, 9.5f * dpi, FontStyle.Regular);   // 正文
    fCap    = new Font(ui, 9f   * dpi, FontStyle.Bold);      // 键帽字符
    fCapZh  = new Font(ui, 8.5f * dpi, FontStyle.Bold);
    fMono   = new Font(mono, 9f * dpi, FontStyle.Regular);   // 版本号、状态行
    fBrand  = new Font(ui, 12f  * dpi, FontStyle.Bold);      // 页脚品牌（比正文大一号）
}
int S(int v) { return (int)Math.Round(v * dpi); }
```

`PickFont` 用 `InstalledFontCollection` 依次探测，缺字体退到系统 UI 字体——**不要内嵌字体文件**。

## 1. 背景：渐变 + 皮纹 + 四角压暗（不引图片）

```csharp
protected override void OnPaint(PaintEventArgs e)
{
    Graphics g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    Rectangle all = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
    DrawLeatherBg(g, all);
    DrawBand(g, new Rectangle(0, 0, ClientSize.Width, S(76)));               // 页眉
    int footY = S(560);
    DrawBand(g, new Rectangle(0, footY, ClientSize.Width, ClientSize.Height - footY)); // 页脚
    DrawHeader(g); DrawLeft(g); DrawCenter(g); DrawRight(g); DrawFooter(g);
}

void DrawLeatherBg(Graphics g, Rectangle r)
{
    using (LinearGradientBrush b = new LinearGradientBrush(r, BgTop, BgBottom, LinearGradientMode.Vertical))
        g.FillRectangle(b, r);
    using (Pen dark = new Pen(Color.FromArgb(12, 0, 0, 0), 1f))             // 暗皮纹
        for (int x = -r.Height; x < r.Width; x += 9)
            g.DrawLine(dark, x, 0, x + r.Height, r.Height);
    using (Pen light = new Pen(Color.FromArgb(8, 255, 220, 160), 1f))       // 亮皮纹，反向
        for (int x = 0; x < r.Width + r.Height; x += 13)
            g.DrawLine(light, x, 0, x - r.Height, r.Height);
    using (GraphicsPath path = new GraphicsPath())                          // 四角压暗
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
```

## 2. 圆角 / 卡片 / 分隔线

```csharp
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
    Rectangle inner = Rectangle.Inflate(r, -S(8), -S(8));                   // 卡内皮纹
    using (Pen grain = new Pen(Color.FromArgb(10, 0, 0, 0), 1f))
        for (int x = inner.Left - inner.Height; x < inner.Right; x += 8)
            g.DrawLine(grain, x, inner.Top, x + inner.Height, inner.Bottom);
    using (GraphicsPath p = Round(r, S(12)))
    {
        using (Pen edge = new Pen(LeatherDark, 1.5f)) g.DrawPath(edge, p);           // 外框
        using (GraphicsPath st = Round(Rectangle.Inflate(r, -S(6), -S(6)), S(9)))    // 黄铜缝线
        using (Pen stitch = new Pen(Color.FromArgb(155, Stitch), 1f))
        {
            stitch.DashStyle = DashStyle.Dash;
            stitch.DashPattern = new float[] { 2.5f, 2.2f };
            g.DrawPath(stitch, st);
        }
    }
    using (Pen hi = new Pen(Color.FromArgb(55, 255, 230, 180), 1f))         // 上沿高光
        g.DrawLine(hi, r.X + S(14), r.Y + 1, r.Right - S(14), r.Y + 1);
    using (Pen sh = new Pen(Color.FromArgb(80, 0, 0, 0), 1f))               // 下沿阴影
        g.DrawLine(sh, r.X + S(14), r.Bottom - 2, r.Right - S(14), r.Bottom - 2);
}

// 分隔线：黄铜一条 + 下面 1px 黑投影，成对出现才像压印
using (Pen p = new Pen(GoldDim, 1f)) g.DrawLine(p, x1, y, x2, y);
using (Pen s = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawLine(s, x1, y + 1, x2, y + 1);
```

圆角三档：卡片 12、按钮 / 下拉 8、键帽 5。间距只用 8 / 12 / 16 / 20（8px 栅格）。
状态指示（`● 已就绪`）全窗口只画一次，放左下角状态带；页眉右上角不放角标。

## 3. 文本

```csharp
TextRenderer.DrawText(g, "关于这款游戏", fHead, new Point(x, y), Gold, Color.Transparent);
DrawWrapped(g, intro, x, y, col.Width - S(32), fBody, Ink);   // 行首 "# " = 小标题，空行 = 单独一行

// 多行正文：自己按换行符断行 + 行距，别用 Label（背景不透明会盖住渐变）
void Body(Graphics g, string text, Rectangle r, Color color)
{
    int lh = TextRenderer.MeasureText(g, "国", fBody).Height + S(3);
    int y = r.Top;
    foreach (string raw in text.Split('\n'))
    {
        if (raw.Length == 0) { y += lh / 2; continue; }
        bool head = raw.StartsWith("# ");
        Font f = head ? fHead : fBody;
        string line = head ? raw.Substring(2) : raw;
        while (line.Length > 0)                       // 中文没空格，按字符折行（只当兜底）
        {
            int take = line.Length;
            while (take > 1 && TextRenderer.MeasureText(g, line.Substring(0, take), f).Width > r.Width)
                take--;
            TextRenderer.DrawText(g, line.Substring(0, take), f, new Point(r.Left, y),
                head ? Gold : color, Color.Transparent);
            y += lh;
            line = line.Substring(take);
        }
    }
}
```

- 按字符折行会断词（`Esc 退` + 换行 + `出`），所以**每条独立信息自带换行**，折行只当兜底。
- 放不下就删文案；不许缩到 9pt 以下、不许用省略号截断。
- 右栏内容多时按整行排版，只画完整落在视口内的行（`InViewport`），配滚轮 + 自绘滚动条（见 §4 末）。
- `TextRenderer.MeasureText` 与 `Graphics.MeasureString` 结果不同，同一处别混用两套。

## 4. 下拉：自绘 `SelectPanel` + `ContextMenuStrip`（皮革默认做法）

**为什么不用原生 `ComboBox`**：白底框 + 白底弹层 + 系统蓝高亮，在皮革上非常突兀
（英雄无敌3 先用了原生控件，改皮革时才发现）。历史教训仍然有效：**别用 `OwnerDrawFixed` /
`FlatStyle.Flat` 半自绘**，换主题色必闪；要做就做全套自绘面板 + 菜单。

```csharp
SelectPanel selMode;
ContextMenuStrip modeMenu;

static void PlaceSelector(SelectPanel p, Rectangle slot)
{
    if (p == null) return;
    p.SetBounds(slot.X, slot.Y, slot.Width, slot.Height);   // 框宽/位置由布局决定，之后不再改
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
    using (Pen pen = new Pen(p.Enabled ? Gold : GoldDim, 1.6f))          // 金色箭头
    {
        int cx = p.Width - S(15), cy = p.Height / 2;
        e.Graphics.DrawLine(pen, cx - S(4), cy - S(2), cx, cy + S(2));
        e.Graphics.DrawLine(pen, cx, cy + S(2), cx + S(4), cy - S(2));
    }
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
    static readonly Color MenuHot  = Color.FromArgb(0x6B, 0x42, 0x28);
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
        if (e.Item.Selected)                                             // 悬停条目左侧金条
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
```

### 横竖都要显示全（框宽不许动）

```csharp
void ShowModeMenu()
{
    if (busy) return;
    if (modeMenu == null) BuildModeMenu();
    for (int i = 0; i < modeMenu.Items.Count; i++)
        modeMenu.Items[i].Text = (i == mode ? "✓  " : "    ") + ModeNames[i];
    FitMenuSize(modeMenu, ModeNames);
    modeMenu.Show(selMode, new Point(0, selMode.Height + S(2)));
    Log("下拉高度自检：项数=" + ModeNames.Length + "，弹层高=" + modeMenu.Height +
        "，能装下=" + (modeMenu.Height >= ModeNames.Length * modeMenu.Items[0].Height));
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

// 只加宽弹层：框宽 / 位置保持布局结果
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
    int want = widest + S(46);                                  // 文字 + 左右内边距
    int cap = Screen.FromControl(selMode).WorkingArea.Width - S(24);
    menu.MinimumSize = new Size(Math.Max(selMode.Width, Math.Min(want, cap)), 0);
    Log("下拉宽度自检：框宽=" + selMode.Width + "，最长项文字宽=" + widest +
        "，弹层宽=" + menu.MinimumSize.Width +
        "，能装下=" + (menu.MinimumSize.Width >= widest + S(24)));
}
```

每次展开都记两行自检（宽度 + 高度），报障时一眼看出是不是又被截了。
某游戏坚持用原生 `ComboBox` 时：`DropDownWidth = 最长项 + 滚动条宽 + 30`、
`MaxDropDownItems = 项数`、`DropDownHeight = 项数 × ItemHeight + 4`，且必须在布局完成之后再量
（之前控件还是默认 121 宽，量出来不可信）。

### 键盘可达（自绘不等于放弃键盘）

```csharp
// 焦点序：下拉 -> 启动 -> 日志 -> 退出（busy 时只剩日志 / 退出）
void CycleFocus(int delta)
{
    int[] order = busy ? new int[] { 1, 2 } : new int[] { 3, 0, 1, 2 };
    int idx = Array.IndexOf(order, focus);
    if (idx < 0) idx = delta > 0 ? order.Length - 1 : 0;
    focus = order[((idx + delta) % order.Length + order.Length) % order.Length];
    RepaintControls();
}

protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    switch (keyData)
    {
        case Keys.Tab: CycleFocus(1); return true;
        case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
        case Keys.Down:  if (!busy && focus == 3) { StepMode(1);  return true; } break;
        case Keys.Up:    if (!busy && focus == 3) { StepMode(-1); return true; } break;
        case Keys.Escape: Close(); return true;
        case Keys.Enter:
            if (busy) { Close(); return true; }
            if (focus >= 0) { Activate(focus); return true; }
            break;
        case Keys.Space: if (!busy && focus >= 0) { Activate(focus); return true; } break;
    }
    return base.ProcessCmdKey(ref msg, keyData);
}

void Activate(int i)
{
    if (i == 3) { ShowModeMenu(); return; }
    if (i == 0) { StartGame(); return; }
    if (i == 1) { OpenLog(); return; }
    if (i == 2) { Close(); }
}

void StepMode(int delta)                 // 上下键直接换档，不用先展开菜单
{
    int n = ModeNames.Length;
    mode = ((mode + delta) % n + n) % n;
    selMode.Text = ModeNames[mode];
    selMode.Invalidate();
    Log("选择显示方式：" + ModeNames[mode]);
}
```

命中测试把下拉也当成一个「按钮」；重绘只刷控件并集，**不要整窗 `Invalidate()`**：

```csharp
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

void FocusRing(Graphics g, Rectangle r)   // 皮革和金按钮上都得看得见
{
    using (GraphicsPath p = Round(Rectangle.Inflate(r, S(2), S(2)), S(8)))
    using (Pen pen = new Pen(Gold, 2f)) g.DrawPath(pen, p);
}
```

焦点环：`GoldBright` 2px（内缩圆角比目标大 3px），外面再套一圈 `alpha 120` 黑 3px；
invalidate 按控件并集外扩 8px，否则环会被裁掉。

### 中栏布局：一叠垂直居中，不留大空洞

```csharp
int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
rcLabelMode = new Rectangle(inner, y, innerW, S(22));
rcComboMode = new Rectangle(inner, y + S(22), innerW, S(30));      // 自绘下拉占这一格
rcStart  = new Rectangle(inner, btnY, innerW, S(44));              // 主按钮通栏
rcFolder = new Rectangle(inner, btnY + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
rcExit   = new Rectangle(rcFolder.Right + S(8), rcFolder.Y,
             innerW - (rcFolder.Right + S(8) - inner), S(34));
```

**不要**再加「将启动」摘要卡：下拉框里那行就是当前选择，重复回显是多余信息。
中栏说明只写一到三句（是什么 / 什么时候换），不上下拉出大段空白。

### 右栏滚动条（内容多时）

```csharp
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
```

## 5. 键帽（本规范的记忆点）

```csharp
void Keycap(Graphics g, string label, int x, int y, int w, int h)
{
    Rectangle r = new Rectangle(x, y, w, h);
    using (GraphicsPath p = Round(r, S(5)))
    {
        using (LinearGradientBrush b = new LinearGradientBrush(r, CapFace, CapSide, LinearGradientMode.Vertical))
            g.FillPath(b, p);
        using (Pen edge = new Pen(PanelEdge, 1f)) g.DrawPath(edge, p);      // 黄铜描边
    }
    using (Pen hi = new Pen(Color.FromArgb(95, CapTopHi), 1f))
        g.DrawLine(hi, r.X + S(3), r.Y + S(2), r.Right - S(3), r.Y + S(2));
    using (Pen sh = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
        g.DrawLine(sh, r.X + S(3), r.Bottom - S(2), r.Right - S(3), r.Bottom - S(2));
    Font f = NeedsZh(label) ? fCapZh : fCap;
    TextRenderer.DrawText(g, label, f,
        new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), Ink, Color.Transparent,  // 米黄，不用纯白
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
}

// 一行「前进 [W]  刹车 [S]」：说明文字 + 键帽混排
void KeyRow(Graphics g, string caption, string[] caps, int x, int y, int labelW)
{
    TextRenderer.DrawText(g, caption, fRow, new Point(x, y + S(3)), Ink, Color.Transparent);
    int cx = x + labelW;
    foreach (string c in caps)
    {
        int w = CapWidth(g, c);                    // max(S(22), 文字宽 + S(12))
        Keycap(g, c, cx, y, w, S(24));
        cx += w + S(4);
    }
}
```

- 宽度 `max(S(22), MeasureText(label) + S(12))`；96 DPI 经验值：单字符 22、双字符 28、
  `Shift`/`Alt`/`Home` 42、`Page Up`/`Insert` 60；两键并排留 4px，组间留 8px。
- **一行里多个「标签 + 键帽」先量整组宽度，放不下整体换行**，绝不能裁掉键（等于给玩家错键位表，
  `Page Up` 这种宽键最容易顶爆右栏，踩过）。
- 只有真键盘键才画键帽；鼠标 / 手柄输入写普通文字（`视角 鼠标`、`射击 鼠标左键`），
  实现见 `Games\CS16\src\Launcher.cs` 的 `PlainItem`。
- 键帽旁的说明用正常字号 `fRow` + `Ink`，**不要用 `InkDim` 压暗**。

## 6. 按钮

```csharp
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
```

主按钮只留一个（启动游戏），通栏宽、烫金底；次按钮皮革底 + 黄铜描边，尺寸小一档。
金底上的字用 `OnGold`（深棕），不要用黑色。

## 7. 状态点与页脚品牌

```csharp
void StatusDot(Graphics g, string text, Color dot, int x, int y)
{
    using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, x, y + S(4), S(9), S(9));
    TextRenderer.DrawText(g, text, fMono, new Point(x + S(15), y), InkDim, Color.Transparent);
}
```

- 点必须配文字（`● 已就绪` / `● 游戏运行中…`），色盲可读、黑白截图也能懂。
- 页脚中间固定品牌 `YunGame`：比正文大一号、粗体、`GoldBright`；先画 1px 深棕偏移再画字（烫金压印）。
  **exe 内不得出现明文品牌**：用编码字节 + SHA-256 自校验（`BrandGuard`），校验失败提示后退出；
  构建后生成 `game_starter.sha256` 给平台复核。签名 / 哈希只提高门槛，没有绝对防改。
- 右下角技术串用 `fMono` + `InkDim`（如「游戏退出后本窗口自动关闭」）。

## 8. 图标：统一仓库图标，禁止从游戏 exe 抽

```csharp
// 构建：csc -win32icon:<仓库>\assets\ico.ico
// 运行时：取 exe 自身图标，窗口 / 任务栏 / 托盘共用一个
void LoadLauncherIcon()
{
    try
    {
        Icon ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (ic != null) { Icon = ic; return; }
    }
    catch (Exception ex) { Log("取 exe 图标失败：" + ex.Message); }
    // 取不到就退回系统默认图标并写日志；不要去找 D:\ico.ico 兜底（交付机上没有）
}

tray.Icon = Icon;                       // 托盘直接复用
```

- **不要再对游戏 exe 调 `ExtractAssociatedIcon`**：任务管理器 / 平台会把启动器认成游戏进程（AGENTS 规则 21）。
- `assets\ico.ico` 缺失时构建失败并提示，不得静默退回游戏图标或默认图标。
- `ExtractAssociatedIcon` 只给 32×32，对没有图标资源的 exe 会静默返回通用图标，别拿它当「取到了」的证据。
- `Form.ShowIcon` 是 bool（标题栏显不显示），不是 Icon 属性，赋值会编译不过。
- `.ico` / `.png` / `.resx` / `.cs` 出现在游戏目录就是违规，用 `scripts\check-launcher.ps1` 检查。

## 9. 取证（只做 PrintWindow，绝不截全屏）

```powershell
# 1) 按 pid 找主窗口：EnumWindows + GetWindowText 非空 + 面积最大
# 2) 自绘下拉是启动器唯一的子窗口：EnumChildWindows 取 Kids[0]
# 3) 展开菜单：走键盘这条路最可靠 —— PostMessageW 给主窗口 WM_KEYDOWN/WM_KEYUP：
#       VK_TAB(9) 把焦点送进自绘下拉，VK_RETURN(13) 展开（ProcessCmdKey 里 Activate(3) -> ShowModeMenu）
#    鼠标路线不可靠：SendMessage 打不开（WinForms 要看真实鼠标状态），PostMessage 点面板时好时坏，
#    合成鼠标点击还会因失焦把菜单关掉、坐标一偏就点到「启动游戏」上（都踩过）
# 4) 展开前后各 EnumWindows 一次，新增的可见窗口就是弹层
# 5) 每个窗口都 PrintWindow(hdc, 2 /*PW_RENDERFULLCONTENT*/)，按相对坐标合成到主窗口位图上
```

- **禁止 `Graphics.CopyFromScreen` 截全屏再裁**：会把用户桌面上的其他窗口（浏览器等）一起带进仓库证据，
  已经踩过两次；发现即删除。
- WinForms 类名带后缀（`WindowsForms10.COMBOBOX.app.0.xxx`），按关键字匹配
  （`ToUpperInvariant().Contains("COMBOBOX")`），别写死 `ComboBox` / `ComboLBox`。
- 样板脚本：`Games\Heroes3\tools\shot-dropdown.ps1`（截图）、
  `Games\Heroes3\tools\test-focus-keys.ps1`（`Tab` / 上下键换档，只看日志不启动游戏）。
- 画面 / 档位类结论要有客观证据：游戏自带分辨率测试、进游戏截图、下拉自检日志；
  不接受「进程没退」当证据。**同一档一次实测 + 存档证据就够，别反复重启已经报错的配置去「再确认」**。
- 别拿「录屏量亮度」这种自证：逐帧平均亮度只有 ±3 波动（指针、重复帧就吃掉了），分辨不了闪烁。

## 10. 坑

- `OnPaint` 里 `new Font` / `new SolidBrush` 不 dispose → GDI 句柄涨，界面卡死。字体只 new 一次，画刷用 `using`。
- `Label` / `LinkLabel` 背景不透明，会盖住渐变底 → 正文一律自绘。
- 真 `Button` 想圆角要 `Region`，但 `Region` 会裁掉描边 → 干脆全自绘 + 自己做命中测试。
- `DoubleBuffered = true` 不够时补 `SetStyle(OptimizedDoubleBuffer | AllPaintingInWmPaint, true)`。
- 高 DPI 不缩放 → 文字被系统拉伸发糊；所有常量尺寸走 `S()`。
- 别用 `MessageBox` 报状态（玩家被模态框卡住）；状态写底栏，错误写清下一步。
- 窗口**永不置顶**；必须有「打开运行日志」，不要「打开游戏目录」；左栏只讲游戏本身。
- 界面每一句要么游戏特有信息，要么玩家下一步动作；平台已经负责的通用提示（会自动存档、请勿关电源）不写。
- 禁止在选项名里写「最稳 / 最兼容 / 不会崩」这类保证词，也禁止自创「稳定全屏 / 直连模式」这种抽象词
  （命名规则见 `SKILL.md`）。

## 11. 个别游戏才需要的片段

键位表从 `.reg` 现算（别写死内部键码）见 `Games\RoadRash\src\Launcher.cs`：
`Controls` 是 22 字节 = 11 个 WORD，低字节恒 `00`，高字节是游戏内部键码
（`0x92`=Alt、`0x96`=下箭头、`0x90`=Shift 这类反直觉值，写死的说明迟早和实际写入的值对不上）。
槽序：`前进 刹车 转向左 倾斜左 加速 拳打 脚踢 重击 下车 转向右 倾斜右`；
转向 / 倾斜是双键（槽 2 与 9、槽 3 与 10 成对），渲染成 `转向 [A][D]`、`倾斜 [Q][E]`。
