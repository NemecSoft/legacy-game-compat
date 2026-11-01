# GDI+ 绘制配方（WinForms / .NET Framework）

单文件 `Launcher.cs`，`class Launcher : Form`。以下片段按「抄了就能用」的粒度给。

> **2026-11-01 文字渲染规则**：启动器**全窗口统一 TextRenderer（GDI ClearType）**，左/中/右、
> 页眉页脚、按钮一致；不要整窗改 GDI+ 灰度（会发虚），右栏也不要画进离屏位图
> （ClearType 二次合成会重影）。右栏滚动按整行排版，只画完整落在视口内的行，不做裁剪/位图；
> 一行放不下就拆行，放不下就停止该行，绝不能叠字。参考 `Games\CS16`、`Games\DFBHD` 的
> `DrawRight` / `DrawWrappedVisible` / `DrawLegendRows`。
>
> **拟物键帽只是形状**（圆角矩形 + 高光 + 阴影），键帽里的字同样用普通 TextRenderer；
> 不要因为键帽引入灰度 / 离屏位图 / 特殊抗锯齿。

## 0. 骨架：双缓冲 + 令牌 + DPI 缩放

```csharp
public Launcher()
{
    DoubleBuffered = true;                       // 必须，否则自绘会闪
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
           | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MaximizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    KeyPreview = true;                           // 自绘控件也要吃键盘
    BackColor = BaseBottom;
    ClientSize = new Size(S(900), S(540));
    LayoutRects();
}

static readonly Color BaseTop    = Color.FromArgb(0xD2, 0x17, 0x2A);
static readonly Color BaseBottom = Color.FromArgb(0x8E, 0x0C, 0x17);
static readonly Color Panel      = Color.FromArgb(0xA5, 0x0F, 0x1B);
static readonly Color PanelEdge  = Color.FromArgb(0xE0, 0x57, 0x4F);
static readonly Color Gold       = Color.FromArgb(0xFF, 0xD8, 0x5E);
static readonly Color GoldDim    = Color.FromArgb(0xC9, 0xA2, 0x27);
static readonly Color Ink        = Color.FromArgb(0xFF, 0xF3, 0xE6);
static readonly Color InkDim     = Color.FromArgb(0xF0, 0xC3, 0xA8);
static readonly Color OnGold     = Color.FromArgb(0x5C, 0x08, 0x10);
static readonly Color CapFace    = Color.FromArgb(0x2E, 0x2E, 0x34);
static readonly Color CapTop     = Color.FromArgb(0x54, 0x54, 0x5E);
static readonly Color CapSide    = Color.FromArgb(0x17, 0x17, 0x1B);
static readonly Color Ok         = Color.FromArgb(0x7B, 0xD8, 0x8F);
static readonly Color Warn       = Color.FromArgb(0xFF, 0xB4, 0x54);

// 字体只 new 一次（static readonly），OnPaint 里 new Font 会漏 GDI 句柄
static readonly Font FTitle  = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
static readonly Font FHead   = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
static readonly Font FRowSel = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
static readonly Font FRow    = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
static readonly Font FBody   = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
static readonly Font FCap    = new Font("Segoe UI", 9f, FontStyle.Bold);
static readonly Font FCapZh  = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
static readonly Font FMono   = new Font("Consolas", 9f, FontStyle.Regular);

float dpiScale = 1f;
protected override void OnHandleCreated(EventArgs e)
{
    base.OnHandleCreated(e);
    using (Graphics g = CreateGraphics()) { dpiScale = g.DpiX / 96f; }
    ClientSize = new Size(S(900), S(540));
    LayoutRects();
}
int S(int v) { return (int)Math.Round(v * dpiScale); }
```

## 1. 背景渐变

```csharp
protected override void OnPaint(PaintEventArgs e)
{
    Graphics g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    using (LinearGradientBrush bg = new LinearGradientBrush(
               new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
               BaseTop, BaseBottom, LinearGradientMode.Vertical))
        g.FillRectangle(bg, ClientSize);
    DrawHeader(g); DrawLeft(g); DrawCenter(g); DrawRight(g); DrawFooter(g);
}
```

## 2. 圆角矩形 + 卡片

```csharp
static GraphicsPath Round(Rectangle r, int radius)
{
    GraphicsPath p = new GraphicsPath();
    int d = radius * 2;
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

void Card(Graphics g, Rectangle r)
{
    using (GraphicsPath p = Round(r, S(10)))
    using (SolidBrush b = new SolidBrush(Panel))
        g.FillPath(b, p);
    using (GraphicsPath p = Round(Rectangle.Inflate(r, -1, -1), S(10)))
    using (Pen pen = new Pen(PanelEdge, 1f))
        g.DrawPath(pen, p);
    // 顶部 1px 高光线，卡片才不塌
    using (Pen hi = new Pen(Color.FromArgb(40, Gold), 1f))
        g.DrawLine(hi, r.X + S(12), r.Y + 1, r.Right - S(12), r.Y + 1);
}
```

## 3. 文本

```csharp
// 正文一律 TextRenderer（GDI，红底上更稳、有 ClearType 网格对齐）
TextRenderer.DrawText(g, "关于这款游戏", FHead, new Point(x, y), Gold, Color.Transparent);

// 需要精确居中/测量时用 GDI（注意两套测量结果不同，别混用）
Size sz = SizeF.Empty;
using (Graphics gg = CreateGraphics())
    sz = Size.Round(gg.MeasureString(text, FBody, width * dpiScale));

// 多行正文：自己按 \n 断行 + 行距，别用 Label（Label 背景不透明会盖住渐变）
// 约定：行首 "# " = 栏内小标题（金色粗体），段落之间用空行 (\n\n)
void Body(Graphics g, string text, Rectangle r, Color color, int lineGap)
{
    int lh = TextRenderer.MeasureText(g, "国", FBody).Height + S(3);
    int y = r.Top;
    foreach (string raw in text.Split('\n'))
    {
        if (raw.Length == 0) { y += lh / 2; continue; }
        bool head = raw.StartsWith("# ");
        Font f = head ? FHead : FBody;
        string line = head ? raw.Substring(2) : raw;
        while (line.Length > 0)                      // 中文没空格，按字符折行
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

折行函数按**字符**断，中文会断词（`Esc 退\n出` 这种很难看）。所以：**每条独立信息自带
`\n`**（"按任意键开始  Pause 暂停" / "Esc 退出" 分两行写），别用空格把好几条拼成一行让
折行去切；折行只当兜底，段落正文仍然写成一整段。
放不下就删文案，不许缩字号到 9pt 以下、不许滚动。

## 4. 选项：标准 ComboBox（保留模式，别自绘）

**教训**：一开始嫌默认控件不好看，写了 `OwnerDrawFixed` + `FlatStyle.Flat` + 子类化
`OptimizedDoubleBuffer` + 吃 `WM_ERASEBKGND`，结果展开/切换时闪烁，越修越复杂。
回退到标准用法后**不闪了**，而且白底黑字加粗在红底卡片上反而更醒目。

```csharp
ComboBox MakeCombo(string[] items, int selected, EventHandler changed)
{
    ComboBox cb = new ComboBox();
    cb.DropDownStyle = ComboBoxStyle.DropDownList;
    cb.BackColor = Color.FromArgb(0x6B, 0x08, 0x12);
    cb.ForeColor = Ink;
    cb.Font = fRowSel;                 // 加粗：框里那行就是当前选择
    cb.Items.AddRange(items);
    cb.SelectedIndex = selected;
    cb.SelectedIndexChanged += changed;
    Controls.Add(cb);
    return cb;
}
```

明确不要写的东西：`DrawMode = OwnerDrawFixed`、`DrawItem` 里自己刷背景、
`FlatStyle = FlatStyle.Flat`、`class FlatCombo : ComboBox { SetStyle(...) }`、
`WM_ERASEBKGND` 拦截。要改外观只动 `Font` / `BackColor` / `ForeColor`。

配套的一条真有用：父窗体在 hover / 选中变化时**不要整窗 `Invalidate()`**，只刷按钮区：

```csharp
Rectangle ButtonsRect { get { return Rectangle.Union(Rectangle.Union(rcStart, rcFolder), rcExit); } }
void RepaintControls() { Invalidate(ButtonsRect); }   // 别带上别的区域，越小越稳
```

中栏一张卡装下三组「标签 + 下拉框」，整叠垂直居中——卡片变高只是上下留白对称，
不会中间空出一大块。**不要**再加「将启动」摘要卡：框里那行加粗白字就是当前选择，
玩家认定重复回显是多余信息。

```csharp
int inner = colCenter.X + S(16), innerW = colCenter.Width - S(32);
int rowH = S(22), capH = S(30), gap = S(20), gapBtn = S(30);
int stack = 3 * (rowH + capH) + 2 * gap + gapBtn + S(44) + S(10) + S(34);
int y = colCenter.Y + (colCenter.Height - stack) / 2;
rcLabelMode = new Rectangle(inner, y, innerW, rowH);
rcComboMode = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gap;
rcLabelKey = new Rectangle(inner, y, innerW, rowH);
rcComboKey = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gap;
rcLabelSave = new Rectangle(inner, y, innerW, rowH);
rcComboSave = new Rectangle(inner, y + rowH, innerW, capH); y += rowH + capH + gapBtn;
rcStart = new Rectangle(inner, y, innerW, S(44));
rcFolder = new Rectangle(inner, y + S(44) + S(10), (innerW - S(8)) * 9 / 16, S(34));
rcExit = new Rectangle(rcFolder.Right + S(8), rcFolder.Y,
    innerW - (rcFolder.Right + S(8) - inner), S(34));
```

状态指示（`● 已就绪`）全窗口只画一次，放在左下角状态带；标题带右上角不要再放角标。

Tab 串场：自绘按钮不是控件，`Tab` 不会自动从下拉框走到按钮，得在窗体层接管：

```csharp
protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    switch (keyData)
    {
        case Keys.Tab: CycleFocus(1); return true;
        case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
        case Keys.Escape: Close(); return true;
        case Keys.Enter:
            if (busy) { Close(); return true; }
            if (focus >= 0) { Activate(focus); return true; }   // 焦点在按钮上
            break;                                              // 在下拉框上：留给它自己
        case Keys.Space:
            if (!busy && focus >= 0) { Activate(focus); return true; }
            break;
    }
    return base.ProcessCmdKey(ref msg, keyData);
}
```

## 5. 键帽（本规范的记忆点）

```csharp
// 单个键帽：宽按字符数给，中文/功能键更宽
void Keycap(Graphics g, string label, int x, int y, int w, int h)
{
    Rectangle r = new Rectangle(x, y, w, h);
    using (GraphicsPath p = Round(r, S(4)))
    using (SolidBrush b = new SolidBrush(CapFace))
        g.FillPath(b, p);
    using (GraphicsPath top = Round(new Rectangle(r.X, r.Y, r.Width, S(3)), S(2)))
    using (SolidBrush tb = new SolidBrush(CapTop))
        g.FillPath(tb, top);                                   // 上沿高光
    using (SolidBrush sb = new SolidBrush(CapSide))            // 下沿阴影
        g.FillRectangle(sb, r.X + S(2), r.Bottom - S(3), r.Width - S(4), S(2));
    Font f = NeedsZh(label) ? FCapZh : FCap;
    Size sz = TextRenderer.MeasureText(g, label, f);
    TextRenderer.DrawText(g, label, f,
        new Rectangle(r.X, r.Y - S(1), r.Width, r.Height), Color.White,
        Color.Transparent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
}

// 一行「前进 [W]  刹车 [S]」：说明文字 + 键帽混排
// 键帽只给真正的键盘键；鼠标/手柄等输入用普通文字，不要套键帽
void KeyRow(Graphics g, string caption, string[] caps, int x, int y, int labelW)
{
    TextRenderer.DrawText(g, caption, FRow, new Point(x, y + S(3)), Ink, Color.Transparent);
    int cx = x + labelW;
    foreach (string c in caps)
    {
        int w = CapWidth(g, c);                      // max(S(22), 文字宽 + S(12))
        Keycap(g, c, cx, y, w, S(24));
        cx += w + S(4);
    }
}
```

键帽宽度：`max(S(22), MeasureText(label) + S(12))`。一行里多个「标签+键帽」组时，
**先量整组宽度，放不下就整体换行**——绝不能把键裁掉，少一个键等于给玩家错的键位表
（`Page Up` 这种宽键最容易顶爆右栏，踩过）。

说明文字用正常字号（10pt `FRow`）和亮色 `Ink`，**不要用 `InkDim` 压暗**——键帽是视觉记忆点，
旁边的说明必须一眼看清。鼠标/左键这类不是键盘键的输入直接写普通文字
（「视角 鼠标」「射击 鼠标左键」），不画键帽；实现见 `Games\CS16\src\Launcher.cs` 的 `PlainItem`。

键帽宽度经验值（96 DPI）：单字符 22、双字符 28、`Shift`/`Alt`/`Home` 42、
`Page Up`/`Insert` 60；两个键并排中间留 4px，组间留 8px。

## 6. 按钮

```csharp
void DrawButton(Graphics g, Rectangle r, string text, bool primary, bool enabled,
                bool hov, bool focused)
{
    Color back = !enabled ? Color.FromArgb(0x6E, 0x6E, 0x6E)
               : primary ? (hov ? Color.FromArgb(0xFF, 0xE4, 0x86) : Gold)
               : (hov ? Color.FromArgb(60, Gold) : Color.Transparent);
    using (GraphicsPath p = Round(r, S(6)))
    {
        if (back != Color.Transparent)
            using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, p);
        using (Pen pen = new Pen(primary ? GoldDim : GoldDim, 1f))
            if (!primary || !enabled) g.DrawPath(pen, p);
    }
    Color fore = !enabled ? Color.FromArgb(0xC8, 0xC8, 0xC8)
             : primary ? OnGold : Ink;
    TextRenderer.DrawText(g, text, primary ? FRowSel : FRow, r, fore, Color.Transparent,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    if (focused) FocusRing(g, r);
}
```

主按钮只留一个（启动游戏），通栏宽；次按钮描边式，尺寸小一档。金色底上的字用深红 `OnGold`，
不要用黑色。

## 7. 状态点 + 状态行

```csharp
void StatusDot(Graphics g, string text, Color dot, int x, int y)
{
    using (SolidBrush b = new SolidBrush(dot))
        g.FillEllipse(b, x, y + S(4), S(9), S(9));
    TextRenderer.DrawText(g, text, FMono, new Point(x + S(15), y), InkDim, Color.Transparent);
}
```

点必须配文字（色盲可读、截图黑白也能懂）。

## 8. 键位表从 reg 现算（别写死）

`Controls` 是 22 字节 = 11 个 WORD，低字节恒 `00`，高字节是游戏内部键码。
槽序：`前进 刹车 转向左 倾斜左 加速 拳打 脚踢 重击 下车 转向右 倾斜右`。

```csharp
static readonly string[] ActionNames =
    { "前进", "刹车", "转向", "倾斜", "加速", "拳打", "脚踢", "重击", "下车" };

// 内部键码 → 显示标签（字母/数字就是 ASCII；特殊键是 0x80+ 内部码）
static string CapLabel(byte code)
{
    if (code >= 0x61 && code <= 0x7A) return ((char)code).ToString().ToUpperInvariant();
    if (code >= 0x30 && code <= 0x39) return ((char)code).ToString();
    switch (code)
    {
        case 0x90: return "Shift";  case 0x91: return "Ctrl";   case 0x92: return "Alt";
        case 0x93: return "←";      case 0x94: return "↑";      case 0x95: return "→";
        case 0x96: return "↓";      case 0x81: return "Page Up"; case 0x82: return "End";
        case 0x83: return "Page Down"; case 0x84: return "Home"; case 0x85: return "Insert";
        case 0x86: return "Delete"; case 0x0D: return "Enter";  case 0x20: return "Space";
        case 0x1B: return "Esc";    case 0x09: return "Tab";    case 0x08: return "Bksp";
    }
    return "?" + code.ToString("X2");
}

// 读 reg 里的 "Controls"=hex:（含 `\` 续行）
static byte[] ReadControls(string regPath)
{
    if (!File.Exists(regPath)) return null;
    const string marker = "\"Controls\"=hex:";      // 15 个字符，别手写偏移
    string text = File.ReadAllText(regPath, Encoding.Default);
    int i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (i < 0) return null;
    i += marker.Length;
    List<byte> b = new List<byte>();
    while (i < text.Length)
    {
        char c = text[i];
        if (char.IsWhiteSpace(c) || c == ',' || c == '\\') { i++; continue; }
        if (i + 1 < text.Length && Uri.IsHexDigit(c) && Uri.IsHexDigit(text[i + 1]))
        { b.Add(Convert.ToByte(text.Substring(i, 2), 16)); i += 2; continue; }
        break;
    }
    return b.Count >= 22 ? b.ToArray() : null;
}
```

转向/倾斜是双键：槽 2 与槽 9 是一对（左/右），槽 3 与槽 10 是一对。渲染成
`转向 [A][D]`、`倾斜 [Q][E]` 两组键帽。

**为什么不能写死**：内部键码不是直觉值（`0x92`=Alt、`0x96`=↓、`0x90`=Shift），
写死的说明和实际写入注册表的值迟早对不上。反查表 + 读 reg 才是自证的。

## 9. 图标：取游戏自带的，别自绘

```csharp
// 运行时取游戏 exe 的图标；窗口、任务栏、托盘共用一个
void LoadGameIcon()
{
    try
    {
        string exe = Path.Combine(AppDir(), gameExe);
        if (File.Exists(exe))
        {
            Icon ic = Icon.ExtractAssociatedIcon(exe);
            if (ic != null) { Icon = ic; return; }
        }
    }
    catch { }
    Icon = SystemIcons.Application;
}

// 托盘直接复用
tray.Icon = Icon;
```

坑：`ExtractAssociatedIcon` 只给 32×32（16px 处由系统缩），且对**没有图标资源**的 exe
会静默返回通用程序图标（不报错），别拿它当"取到了游戏图标"的证据；`Form.ShowIcon` 是 bool
（标题栏显不显示图标），不是 Icon 属性，赋值会编译不过。
运行时设 `Icon` 只改窗口/任务栏，**exe 文件本身仍是白纸**；构建时要把同一个图标
`Save` 成 `out\game_starter.ico`，再用 `csc -win32icon:` 嵌入。
自绘 `Bitmap.GetHicon()` 路线试过：小尺寸发糊、还要自己管句柄，不如直接用现成图标。

## 10. 坑

- `OnPaint` 里 `new Font` / `new SolidBrush` 不 dispose → GDI 句柄涨，界面卡死。
  字体用 `static readonly`，画刷用 `using`。
- `TextRenderer.MeasureText` 与 `Graphics.MeasureString` 结果不同，同一处别混用两套。
- `Label` / `LinkLabel` 背景不透明，会盖住渐变底 → 正文一律自绘。
- 真 `Button` 想圆角要 `Region = new Region(path)`，但 `Region` 会让描边被裁掉；
  干脆全自绘 + 自己做命中测试。
- `DoubleBuffered = true` 不够时补 `SetStyle(OptimizedDoubleBuffer | AllPaintingInWmPaint, true)`。
- 高 DPI 下不缩放 → 文字被系统拉伸发糊；所有常量尺寸走 `S()`。
- 别用 `MessageBox` 报状态（玩家会被模态框卡住）；状态写进底栏，错误写清下一步。
