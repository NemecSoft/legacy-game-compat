// 三国群英传 8（KingdomHeroes8 / SG8）启动器
//
// 这个包的离线运行环境（便携 Steam + MiniPipe 注入 + 私有注册表视图）是包内自带且受保护的，
// 实测直接启动 GAME\SG8.exe 会在约 6 秒内崩（0xC0000005），只补环境变量不注入也一样崩，
// 所以这里不复刻那套机制，而是拉起包内自带的 SG8Launcher.exe，并替玩家做掉三件事：
//   1. 把原版窗口拉回屏幕内（它会记住上次位置，可能被挪到屏幕外）；
//   2. 盖住左下角的推广链接（萌芽游戏 / XDGAME），页脚其余部分不动；
//   3. 自动点「启动游戏」，然后陪跑真正的 SG8.exe，退出码原样回传。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Native.EnableDpiAwareness();
        bool autoClick = true, cover = true;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "/go" || a == "-go" || a == "--go") autoClick = true;
            if (a == "/noclick" || a == "-noclick") autoClick = false;
            if (a == "/nopatch" || a == "-nopatch") cover = false;
        }
        Runner r = new Runner();
        int code = r.Run(autoClick, cover);
        return code;
    }
}

class Runner
{
    Config cfg = Config.Load();
    Process offline;
    CoverForm coverForm;
    AutomationElement offlineWin;

    public int Run(bool autoClick, bool cover)
    {
        Log.Open(cfg.Dir, "运行日志.txt");
        Log.Write("三国群英传 8 启动器开始；自动点按钮=" + autoClick + " 遮推广=" + cover);
        Log.Write("工作目录：" + cfg.Dir + "；父进程：" + Native.ParentProcessName());

        string launcherPath = Path.Combine(cfg.Dir, cfg.OfflineLauncher);
        if (!File.Exists(launcherPath))
        {
            Log.Write("找不到包内离线启动器 " + launcherPath);
            return 2;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(launcherPath, "");
            psi.WorkingDirectory = cfg.Dir;
            psi.UseShellExecute = false;
            offline = Process.Start(psi);
            Log.Write("已拉起原版离线启动器 pid=" + offline.Id);
        }
        catch (Exception ex)
        {
            Log.Write("拉起原版启动器失败：" + ex.Message);
            return 3;
        }

        int hwndWaitMs = 0;
        offlineWin = WaitOfflineWindow(20000, out hwndWaitMs);
        if (offlineWin == null)
        {
            Log.Write("等不到原版启动器窗口（" + hwndWaitMs + "ms），放弃");
            return 4;
        }
        Log.Write("原版启动器窗口已出现（" + hwndWaitMs + "ms）");

        if (cover) StartCover();

        int code = 0;
        Process game = WatchAndStartGame(autoClick, 240000);
        if (game == null) code = 5;
        else code = WaitGameExit(game);

        CloseOffline();
        Log.Write("启动器结束，回传退出码 0x" + ((uint)code).ToString("X8"));
        Log.Close();
        return code;
    }

    // ---------- 原版窗口 ----------
    // 同一个 pid 往往有多个顶层窗口（WPF 的隐藏消息窗口也在），必须挑主窗口那个，
    // 否则拿到空控件树：既找不到链接，也点不到按钮。
    AutomationElement WaitOfflineWindow(int timeoutMs, out int spentMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            try
            {
                if (!offline.HasExited)
                {
                    IntPtr main = offline.MainWindowHandle;
                    if (main != IntPtr.Zero)
                    {
                        AutomationElement el = FindWindowByHandle(offline.Id, main);
                        if (el != null)
                        {
                            EnsureOnScreen(el);
                            spentMs = waited;
                            return el;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Write("找窗口异常：" + ex.Message); }
            SleepPump(400);
            waited += 400;
        }
        spentMs = waited;
        return null;
    }

    static AutomationElement FindWindowByHandle(int pid, IntPtr want)
    {
        PropertyCondition cond = new PropertyCondition(AutomationElement.ProcessIdProperty, pid);
        AutomationElementCollection col = AutomationElement.RootElement.FindAll(
            TreeScope.Children, cond);
        for (int i = 0; i < col.Count; i++)
        {
            try
            {
                if (col[i].Current.NativeWindowHandle == want.ToInt64()) return col[i];
            }
            catch { }
        }
        return null;
    }

    // 它会记住上次窗口位置，被挪到屏幕外时 WPF 不再排布局，UIA 控件树是空的
    void EnsureOnScreen(AutomationElement el)
    {
        try
        {
            IntPtr h = new IntPtr(el.Current.NativeWindowHandle);
            Native.RECT rc;
            if (!Native.GetWindowRect(h, out rc)) return;
            if (rc.Left > -2000 && rc.Top > -2000 && rc.Right > 40 && rc.Bottom > 40) return;
            int w = rc.Right - rc.Left, ht = rc.Bottom - rc.Top;
            Size screen = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Size;
            int x = (screen.Width - w) / 2, y = Math.Max(40, (screen.Height - ht) / 3);
            Native.SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER);
            Log.Write("原版窗口在屏幕外（" + rc.Left + "," + rc.Top + "），已挪到 " + x + "," + y);
        }
        catch (Exception ex) { Log.Write("挪窗口异常：" + ex.Message); }
    }

    void StartCover()
    {
        try
        {
            Rectangle? area = PromoArea();
            IntPtr owner = Handle();
            Color bg = SampleFooter(owner, area.Value);
            if (area == null)
            {
                Native.RECT rc;
                if (!Native.GetWindowRect(owner, out rc)) { Log.Write("取不到原版窗口矩形，跳过遮罩"); return; }
                area = new Rectangle(rc.Left + 16, rc.Bottom - 56, 300 - 16, 40);
                Log.Write("UIA 没找到左下角链接，退回固定条带");
            }
            coverForm = new CoverForm(bg, area.Value);
            coverForm.Show();
            Native.SetWindowLongPtr(coverForm.Handle, Native.GWLP_HWNDPARENT, owner);
            Native.SetWindowPos(coverForm.Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);
            Log.Write("推广遮罩已贴上：" + area.Value + " 底色 RGB=" + bg.R + "," + bg.G + "," + bg.B);
        }
        catch (Exception ex)
        {
            Log.Write("遮罩异常（不影响启动）：" + ex.Message);
        }
    }

    // 遮罩是原版窗口的从属窗口（层级跟着走），但位置要自己跟：拖动、最大化、UIA 迟到都要重贴
    void TrackCover()
    {
        if (coverForm == null || coverForm.IsDisposed) return;
        try
        {
            Rectangle? again = PromoArea();
            if (again != null && again.Value != coverForm.Bounds) coverForm.MoveTo(again.Value);
        }
        catch (Exception ex)
        {
            // 原版窗口正在关闭时 UIA 会抛这类错，属于正常收尾，不值得刷屏
            Log.WriteOnce("遮罩跟随已停止（原版窗口正在关闭）：" + ex.Message);
        }
    }

    // 左下角那一块推广：取窗口下半部、靠左 45% 以内的所有链接，合并成一个矩形
    Rectangle? PromoArea()
    {
        try
        {
            IntPtr owner = Handle();
            Native.RECT wr;
            if (!Native.GetWindowRect(owner, out wr)) return null;
            int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
            int x1 = int.MaxValue, y1 = int.MaxValue, x2 = int.MinValue, y2 = int.MinValue;
            int found = 0;
            foreach (AutomationElement e in Descendants(offlineWin))
            {
                if (e.Current.ControlType != ControlType.Hyperlink) continue;
                System.Windows.Rect r = e.Current.BoundingRectangle;
                if (r.Width <= 0 || r.Height <= 0) continue;
                if (r.X < wr.Left - 8 || r.Y < wr.Top - 8) continue;
                int relX = (int)r.X - wr.Left, relY = (int)r.Y - wr.Top;
                if (relY < h * 0.6) continue;              // 只管下半部
                if (relX > w * 0.45) continue;             // 只管靠左
                if (relX < x1) x1 = relX;
                if (relY < y1) y1 = relY;
                if (relX + (int)r.Width > x2) x2 = relX + (int)r.Width;
                if (relY + (int)r.Height > y2) y2 = relY + (int)r.Height;
                found++;
                Log.WriteOnce("遮住链接：" + e.Current.Name + " @ " + (int)r.X + "," + (int)r.Y);
            }
            if (found == 0) return null;
            int pad = cfg.CoverPad;
            return new Rectangle(wr.Left + x1 - pad, wr.Top + y1 - pad,
                (x2 - x1) + pad * 2, (y2 - y1) + pad * 2);
        }
        catch (Exception ex)
        {
            Log.Write("取推广区域异常：" + ex.Message);
            return null;
        }
    }

    static Color SampleFooter(IntPtr owner, Rectangle area)
    {
        Native.RECT rc;
        if (!Native.GetWindowRect(owner, out rc)) return Color.White;
        int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
        if (w < 40 || h < 40) return Color.White;
        int areaLeft = area.Left - rc.Left, areaTop = area.Top - rc.Top;
        using (Bitmap bmp = new Bitmap(w, h))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                Native.PrintWindow(owner, hdc, 2);   // PW_RENDERFULLCONTENT
                g.ReleaseHdc(hdc);
            }
            // 取遮罩右侧同一条横幅的像素：那里肯定是页脚底色，不会撞到按钮
            try
            {
                int sx = Math.Min(w - 20, Math.Max(20, areaLeft + area.Width + 30));
                int sy = Math.Min(h - 20, Math.Max(20, areaTop + area.Height / 2));
                return bmp.GetPixel(sx, sy);
            }
            catch { return Color.White; }
        }
    }

    IntPtr Handle()
    {
        return offline.MainWindowHandle;
    }

    static List<AutomationElement> Descendants(AutomationElement root)
    {
        List<AutomationElement> list = new List<AutomationElement>();
        try
        {
            AutomationElementCollection col = root.FindAll(TreeScope.Descendants,
                Condition.TrueCondition);
            for (int i = 0; i < col.Count; i++) list.Add(col[i]);
        }
        catch (Exception ex)
        {
            Log.Write("遍历控件树异常：" + ex.Message);
        }
        return list;
    }

    InvokePattern StartButton()
    {
        try
        {
            foreach (AutomationElement e in Descendants(offlineWin))
            {
                if (e.Current.ControlType != ControlType.Button) continue;
                if (!e.Current.IsEnabled) continue;
                bool hit = false;
                foreach (AutomationElement k in Descendants(e))
                {
                    if (k.Current.Name.Contains(cfg.StartButtonText)) { hit = true; break; }
                }
                if (!hit) continue;
                object pat;
                if (e.TryGetCurrentPattern(InvokePattern.Pattern, out pat)) return (InvokePattern)pat;
            }
        }
        catch (Exception ex)
        {
            Log.Write("找启动按钮异常：" + ex.Message);
        }
        return null;
    }

    // ---------- 等游戏起来，期间该点的按钮要点到为止 ----------
    // 原版启动器要先跑完依赖检查，点早了它不响应，所以要带重试
    Process WatchAndStartGame(bool autoClick, int timeoutMs)
    {
        int waited = 0, clicks = 0, lastClick = -999000;
        while (waited < timeoutMs)
        {
            // 窗口刚出现时 WPF 还没排布局（控件树是空的），这时要重新抓窗口而不是放弃
            if (Descendants(offlineWin).Count == 0) ReacquireWindow();
            TrackCover();
            if (offline.HasExited)
            {
                Log.Write("原版启动器先退出了（" + waited / 1000 + "s），没等到游戏");
                return null;
            }
            Process game = FindGame();
            if (game != null) return game;
            if (autoClick && clicks < 6 && waited - lastClick >= 12000)
            {
                InvokePattern inv = StartButton();
                if (inv != null)
                {
                    try { inv.Invoke(); Log.Write("点「启动游戏」（第 " + (clicks + 1) + " 次，t+" + waited / 1000 + "s）"); }
                    catch (Exception ex) { Log.Write("自动点击失败：" + ex.Message); }
                    clicks++;
                    lastClick = waited;
                }
            }
            SleepPump(500);
            waited += 500;
        }
        Log.Write("等了 " + timeoutMs / 1000 + "s 没等到游戏进程");
        return null;
    }

    void ReacquireWindow()
    {
        try
        {
            IntPtr main = offline.MainWindowHandle;
            if (main == IntPtr.Zero) return;
            AutomationElement el = FindWindowByHandle(offline.Id, main);
            if (el == null) return;
            EnsureOnScreen(el);
            offlineWin = el;
        }
        catch (Exception ex) { Log.WriteOnce("重取窗口异常：" + ex.Message); }
    }

    int WaitGameExit(Process game)
    {
        Log.Write("游戏进程 pid=" + game.Id + "，启动器陪跑中（关本窗口不杀游戏）");
        // 不能对游戏进程做 EnableRaisingEvents：实测它几秒内就自杀（保护检测）。
        // 自己开一个最小权限句柄，进程没了也能从这份句柄读到真正的退出码。
        IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION | Native.SYNCHRONIZE,
            false, (uint)game.Id);
        int code = 0;
        try
        {
            while (!game.HasExited)
            {
                TrackCover();
                SleepPump(400);
                uint live;
                if (h != IntPtr.Zero && Native.GetExitCodeProcess(h, out live) && live != Native.STILL_ACTIVE) code = (int)live;
            }
            uint fin;
            if (h != IntPtr.Zero && Native.GetExitCodeProcess(h, out fin) && fin != Native.STILL_ACTIVE) code = (int)fin;
            else
            {
                try { code = game.ExitCode; }
                catch (Exception ex2) { Log.Write("读游戏退出码失败（按 0 处理）：" + ex2.Message); }
            }
            Log.Write("游戏退出，退出码 0x" + ((uint)code).ToString("X8"));
            return code;
        }
        catch (Exception ex)
        {
            Log.Write("等游戏退出异常：" + ex.Message);
            return 0;
        }
        finally
        {
            if (h != IntPtr.Zero) Native.CloseHandle(h);
        }
    }

    // 游戏进程被原版启动器加了保护（读 StartTime 会拒绝访问），
    // 所以按父进程链认：原版启动器 -> 便携 steam.exe -> SG8.exe，避开上一轮遗留的挂起进程。
    Process FindGame()
    {
        try
        {
            Process[] list = Process.GetProcessesByName(cfg.GameName);
            foreach (Process p in list)
            {
                try
                {
                    uint gpid = (uint)p.Id;
                    uint parent = Native.ParentPid(gpid);
                    uint grand = Native.ParentPid(parent);
                    if (grand == (uint)offline.Id)
                    {
                        Log.Write("按父进程链认到游戏：pid=" + p.Id +
                                 "（steam.exe pid=" + parent + "）");
                        return p;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Log.Write("找游戏进程异常：" + ex.Message); }
        return null;
    }

    // 遮罩是自己画的窗口，等待时必须继续泵消息，否则它不重绘、也跟不上原版窗口移动
    static void SleepPump(int ms)
    {
        int left = ms;
        while (left > 0)
        {
            Application.DoEvents();
            Thread.Sleep(25);
            left -= 25;
        }
    }

    void CloseOffline()
    {
        try
        {
            if (offline == null || offline.HasExited) return;
            IntPtr h = offline.MainWindowHandle;
            if (h != IntPtr.Zero) Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            if (!offline.WaitForExit(12000))
            {
                Log.Write("原版启动器 12s 没自己退，结束它（会话清理在游戏退出时已做完）");
                offline.Kill();
            }
            else Log.Write("已关闭原版启动器窗口，它会自己清理便携 Steam 与临时注册表");
        }
        catch (Exception ex) { Log.Write("关闭原版启动器异常：" + ex.Message); }
        if (coverForm != null && !coverForm.IsDisposed) { try { coverForm.Close(); } catch { } }
    }
}

// ---------- 遮罩窗口 ----------
class CoverForm : Form
{
    Color bg;

    public CoverForm(Color background, Rectangle area)
    {
        bg = background;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = false;
        BackColor = bg;
        Bounds = area;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void MoveTo(Rectangle area)
    {
        if (Bounds == area) return;
        Bounds = area;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(bg);
    }
}

// ---------- 配置 ----------
class Config
{
    public string Dir = "";
    public string OfflineLauncher = "SG8Launcher.exe";
    public string GameName = "SG8";
    public string StartButtonText = "启动游戏";
    public int CoverPad = 6;

    public static Config Load()
    {
        Config c = new Config();
        c.Dir = AppDir();
        string ini = Path.Combine(c.Dir, "launcher.ini");
        if (!File.Exists(ini)) return c;
        try
        {
            foreach (string raw in File.ReadAllLines(ini, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                if (k == "OfflineLauncher" && v.Length > 0) c.OfflineLauncher = v;
                if (k == "GameName" && v.Length > 0) c.GameName = v;
                if (k == "StartButtonText" && v.Length > 0) c.StartButtonText = v;
                int pad;
                if (k == "CoverPad" && int.TryParse(v, out pad) && pad >= 0 && pad < 40) c.CoverPad = pad;
            }
        }
        catch (Exception ex) { Log.Write("读 launcher.ini 失败：" + ex.Message); }
        return c;
    }

    public static string AppDir()
    {
        try { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        catch { return Environment.CurrentDirectory; }
    }
}

// ---------- 日志 ----------
static class Log
{
    static string path;
    static HashSet<string> once = new HashSet<string>();

    public static void Open(string dir, string name)
    {
        try { path = Path.Combine(dir, name); } catch { path = null; }
    }

    public static void Write(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
        Console.WriteLine(line);
        if (path == null) return;
        try { File.AppendAllText(path, line + "\r\n", Encoding.UTF8); }
        catch { }
    }

    public static void WriteOnce(string text)
    {
        if (once.Contains(text)) return;
        once.Add(text);
        Write(text);
    }

    public static void Close() { }
}

// ---------- Win32 ----------
static class Native
{
    public const int GWLP_HWNDPARENT = -8;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WM_CLOSE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr value);
    [DllImport("kernel32.dll")] public static extern bool SetProcessDPIAware();

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint SYNCHRONIZE = 0x00100000;
    public const uint STILL_ACTIVE = 259;

    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool GetExitCodeProcess(IntPtr h, out uint code);

    public static void EnableDpiAwareness()
    {
        try { SetProcessDPIAware(); } catch { }
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
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll")] static extern int Process32First(IntPtr s, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll")] static extern int Process32Next(IntPtr s, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

    public static uint ParentPid(uint pid)
    {
        IntPtr snap = CreateToolhelp32Snapshot(2, pid);
        if (snap == IntPtr.Zero || snap.ToInt64() == -1) return 0;
        try
        {
            PROCESSENTRY32 e = new PROCESSENTRY32();
            e.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            if (Process32First(snap, ref e) == 0) return 0;
            do
            {
                if (e.th32ProcessID == pid) return e.th32ParentProcessID;
            }
            while (Process32Next(snap, ref e) != 0);
        }
        finally { CloseHandle(snap); }
        return 0;
    }

    public static string ParentProcessName()
    {
        try
        {
            uint ppid = ParentPid((uint)Process.GetCurrentProcess().Id);
            if (ppid == 0) return "未知";
            using (Process p = Process.GetProcessById((int)ppid)) return p.ProcessName;
        }
        catch { return "未知"; }
    }
}
