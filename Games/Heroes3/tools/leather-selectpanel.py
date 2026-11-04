# -*- coding: utf-8 -*-
# 英雄无敌3 启动器：原生 ComboBox -> 皮革自绘 SelectPanel + ContextMenuStrip（照技能规范里的自绘做法）
import io

P = r'D:\AI\legacy-game-compat\Games\Heroes3\src\Launcher.cs'


def load(p):
    return io.open(p, encoding='utf-8', newline='').read().replace('\r\n', '\n')


def save(p, t):
    io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))


def sub(t, a, b, tag):
    assert a in t, tag
    return t.replace(a, b, 1)


t = load(P)

# ---------- 字段 ----------
t = sub(t, '    ComboBox cbMode;', '    SelectPanel selMode;\n    ContextMenuStrip modeMenu;', '字段')

# ---------- 创建 ----------
t = sub(t, """        if (cbMode == null)
        {
            cbMode = MakeCombo(ModeNames, mode, delegate { mode = cbMode.SelectedIndex; });
            cbMode.TabIndex = 0;
        }""",
"""        if (selMode == null) selMode = MakeModeSelect();""", '创建')

# ---------- 布局 ----------
t = sub(t, """        PlaceCombo(cbMode, rcComboMode);
        // 布局定了之后再算弹层宽度：框宽不变，只把列表加宽到装得下最长的一项
        FitDropDownWidth(cbMode, ModeNames);""",
"""        PlaceSelector(selMode, rcComboMode);""", '布局')

# ---------- 中栏绘制：焦点环 ----------
t = sub(t, """        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
    }""",
"""        DrawButton(g, rcExit, "退出", false, true, hover == 2 || focus == 2, focus == 2);
        if (focus == 3 && selMode != null) FocusRing(g, selMode.Bounds);
    }""", '中栏焦点环')

# ---------- 命中测试 / 重绘区域 ----------
t = sub(t, """        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcFolder.Contains(pt)) return 1;
        if (rcExit.Contains(pt)) return 2;
        return -1;""",
"""        if (!busy && rcStart.Contains(pt)) return 0;
        if (rcFolder.Contains(pt)) return 1;
        if (rcExit.Contains(pt)) return 2;
        if (!busy && selMode != null && selMode.Bounds.Contains(pt)) return 3;
        return -1;""", 'HitTest')

t = sub(t, """        get { return Rectangle.Union(Rectangle.Union(rcStart, rcFolder), rcExit); }""",
"""        get
        {
            Rectangle r = Rectangle.Union(rcStart, rcFolder);
            r = Rectangle.Union(r, rcExit);
            if (selMode != null) r = Rectangle.Union(r, selMode.Bounds);
            return r;
        }""", 'ButtonsRect')

# ---------- 键盘 ----------
t = sub(t, """            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
            case Keys.Escape: Close(); return true;""",
"""            case Keys.Tab: CycleFocus(1); return true;
            case Keys.Shift | Keys.Tab: CycleFocus(-1); return true;
            case Keys.Down:
                if (!busy && focus == 3) { StepMode(1); return true; }
                break;
            case Keys.Up:
                if (!busy && focus == 3) { StepMode(-1); return true; }
                break;
            case Keys.Escape: Close(); return true;""", 'ProcessCmdKey')

t = sub(t, """        int[] order = busy ? new int[] { 1, 2 } : new int[] { 0, 1, 2 };""",
"""        int[] order = busy ? new int[] { 1, 2 } : new int[] { 3, 0, 1, 2 };""", 'CycleFocus')

t = sub(t, """        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenLog(); return; }
        if (i == 2) { Close(); }""",
"""        if (i == 3) { ShowModeMenu(); return; }
        if (i == 0) { StartGame(); return; }
        if (i == 1) { OpenLog(); return; }
        if (i == 2) { Close(); }""", 'Activate')

# ---------- SetBusy ----------
t = sub(t, '        if (cbMode != null) cbMode.Enabled = !value;',
        '        if (selMode != null) selMode.Enabled = !value;', 'SetBusy')

# ---------- 控件工厂：整段替换 ----------
old = t[t.index('    static void PlaceCombo(ComboBox cb, Rectangle slot)'):t.index('    protected override void OnMouseMove(MouseEventArgs e)')]
new = '''    static void PlaceSelector(SelectPanel p, Rectangle slot)
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
            modeMenu.Items[i].Text = (i == mode ? "\\u2713  " : "    ") + ModeNames[i];
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

'''
t = t.replace(old, new, 1)

save(P, t)
print('SelectPanel 接入完成')
