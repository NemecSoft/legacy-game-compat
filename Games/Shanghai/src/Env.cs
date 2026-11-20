using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// 环境读数与显示模式：AGENTS 规则 37 / 40。
// 一律现场实测，读回来的数带断言；不写「读数正常就跳过」的分叉，所有机器走同一条路。
internal static class Env
{
    public const int VREFRESH = 116;              // 不是 0（0 是 DRIVERVERSION）
    const int DM_BITSPERPEL = 0x00040000;
    const int DM_PELSWIDTH = 0x00080000;
    const int DM_PELSHEIGHT = 0x00100000;
    const int DM_DISPLAYFREQUENCY = 0x00400000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType,
                    dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string device, int modeNum, ref DEVMODEW dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettings(ref DEVMODEW dm, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern bool GetDiskFreeSpaceA(string root, out uint sectorsPerCluster,
        out uint bytesPerSector, out uint freeClusters, out uint totalClusters);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern uint GetDriveType(string root);
    [DllImport("kernel32.dll")] static extern uint GetTickCount();

    public class Readings
    {
        public int Width, Height, Refresh, Dpi;
        public uint BytesPerSector, SectorsPerCluster, FreeClusters, TotalClusters;
        public string DriveType = "未知";
        public string DriveRoot = "";
        public bool DiskOk;
        public int DevModeSize;
        public bool DevModeOk;
        public string Nic = "未取到";
        public uint Tick;
    }

    public static DEVMODEW NewDevMode()
    {
        DEVMODEW dm = new DEVMODEW();
        dm.dmDeviceName = new string('\0', 32);
        dm.dmFormName = new string('\0', 32);
        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODEW));
        return dm;
    }

    // 无条件读一遍现场；读到什么都带断言，坏的用兜底值顶上，绝不留 0 给老引擎当除数
    public static Readings Read(Action<string> log, string appDir)
    {
        Readings r = new Readings();
        r.DevModeSize = Marshal.SizeOf(typeof(DEVMODEW));
        r.DevModeOk = r.DevModeSize == 220;                    // 少字段会读出 32×1920 这种鬼数
        log("DEVMODEW Marshal.SizeOf=" + r.DevModeSize + "（必须为 220）→ " + (r.DevModeOk ? "通过" : "不可信"));

        DEVMODEW cur = NewDevMode();
        bool got = EnumDisplaySettings(null, -1, ref cur);
        if (got && cur.dmPelsWidth > 0 && cur.dmPelsHeight > 0)
        {
            r.Width = (int)cur.dmPelsWidth;
            r.Height = (int)cur.dmPelsHeight;
            r.Refresh = (int)cur.dmDisplayFrequency;
        }
        else
        {
            r.Width = 1024; r.Height = 768; r.Refresh = 60;   // 兜底，不许留 0
            log("EnumDisplaySettings 没给可信模式，按 1024×768@60 兜底");
        }
        if (r.Refresh <= 0 || r.Refresh > 1000) { r.Refresh = 60; log("刷新率读数不可信，钉成 60"); }

        IntPtr dc = GetDC(IntPtr.Zero);
        try { log("GetDeviceCaps(VREFRESH=116)=" + GetDeviceCaps(dc, VREFRESH)); }
        finally { ReleaseDC(IntPtr.Zero, dc); }

        try { r.Dpi = (int)GetDpiForSystem(); }
        catch { r.Dpi = 96; }
        if (r.Dpi <= 0) { r.Dpi = 96; log("DPI 读数不可信，钉成 96"); }

        try
        {
            r.DriveRoot = Path.GetPathRoot(Path.GetFullPath(appDir)) ?? "";
            uint kind = GetDriveType(r.DriveRoot);
            string[] names = { "未知", "不存在", "可移动", "本地盘", "网络盘", "光驱", "内存盘" };
            r.DriveType = kind < names.Length ? names[kind] : "未知(" + kind + ")";
            uint spc, bps, freeC, totalC;
            if (GetDiskFreeSpaceA(r.DriveRoot, out spc, out bps, out freeC, out totalC))
            {
                r.SectorsPerCluster = spc; r.BytesPerSector = bps;
                r.FreeClusters = freeC; r.TotalClusters = totalC;
                r.DiskOk = (bps == 512 || bps == 1024 || bps == 2048 || bps == 4096) &&
                           spc > 0 && freeC <= totalC;
                if (!r.DiskOk)
                {
                    // 映射盘/4K 扇区/大簇会给出畸形几何，换成一套安全值，绝不把 0 交给游戏
                    log("磁盘几何断言失败，改用安全值 512/1，可用簇<=总簇");
                    r.BytesPerSector = 512; r.SectorsPerCluster = 1;
                    r.FreeClusters = Math.Min(freeC, totalC);
                }
            }
            else { r.BytesPerSector = 512; r.SectorsPerCluster = 1; log("取磁盘几何失败，用 512/1"); }
        }
        catch (Exception ex) { r.BytesPerSector = 512; r.SectorsPerCluster = 1; log("磁盘几何异常：" + ex.Message); }

        r.Tick = GetTickCount();                               // 高精度计时不可用时的退路
        if (r.Tick == 0) { r.Tick = 1; log("计时器读数 0，钉成 1，绝不当分母"); }

        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                string n = nic.Name.ToLowerInvariant();
                if (n.Contains("virtual") || n.Contains("vmware") || n.Contains("hyper-v") ||
                    n.Contains("tap") || n.Contains("loopback") || n.Contains("radmin") ||
                    n.Contains("oray") || n.Contains("gameviewer")) continue;
                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                { r.Nic = nic.Name; break; }
            }
        }
        catch { }

        log(string.Format("现场读数：桌面 {0}×{1} @ {2}Hz；DPI={3}；盘={4}({5})；" +
                          "每扇区={6}B 每簇={7}扇区 可用簇={8}/总簇={9}；网卡={10}；计时={11}",
                          r.Width, r.Height, r.Refresh, r.Dpi, r.DriveRoot, r.DriveType,
                          r.BytesPerSector, r.SectorsPerCluster, r.FreeClusters, r.TotalClusters,
                          r.Nic, r.Tick));
        return r;
    }

    // 枚举真实可用的 4:3 模式；取不到目标也照样走「切一次 → 还原」这一条路
    public static bool PickMode(int wantW, int wantH, Action<string> log, out int w, out int h)
    {
        w = wantW; h = wantH;
        List<int[]> modes = new List<int[]>();
        DEVMODEW dm = NewDevMode();
        for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
        {
            if (dm.dmPelsWidth < 640 || dm.dmPelsHeight < 480) continue;
            if (dm.dmPelsWidth * 3 == dm.dmPelsHeight * 4)     // 只收 4:3
                modes.Add(new int[] { (int)dm.dmPelsWidth, (int)dm.dmPelsHeight });
            dm = NewDevMode();
        }
        foreach (int[] m in modes)
            if (m[0] == wantW && m[1] == wantH) { w = m[0]; h = m[1]; log("枚举到目标 4:3 模式 " + w + "×" + h); return true; }
        int bestW = 0, bestH = 0;
        foreach (int[] m in modes)
            if (m[0] <= wantW && m[0] >= bestW) { bestW = m[0]; bestH = m[1]; }
        if (bestW > 0) { w = bestW; h = bestH; log("目标模式不可用，退到最接近的 4:3 " + w + "×" + h); return true; }
        log("没有可用的 4:3 模式，保持当前宽高，只把刷新率钉成 60Hz");
        return false;
    }

    // 启动前无条件设一次显示模式（分辨率 + 60Hz），返回原模式供 finally 还原
    public static DEVMODEW Apply(int w, int h, Action<string> log)
    {
        DEVMODEW before = NewDevMode();
        if (!EnumDisplaySettings(null, -1, ref before)) log("取原显示模式失败（还原按兜底模式走）");
        DEVMODEW want = before;
        want.dmPelsWidth = (uint)Math.Max(640, w);
        want.dmPelsHeight = (uint)Math.Max(480, h);
        want.dmDisplayFrequency = 60;                        // 老引擎把刷新率当除数，0 就是 0xC0000094
        want.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
        int rc = ChangeDisplaySettings(ref want, 0);
        log("启动前设显示模式 " + want.dmPelsWidth + "×" + want.dmPelsHeight + "@60：" +
            (rc == 0 ? "成功" : "失败 rc=" + rc + "，按原样继续"));
        return before;
    }

    public static void Restore(DEVMODEW before, Action<string> log)
    {
        try
        {
            DEVMODEW dm = before;
            dm.dmFields = DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
            int rc = ChangeDisplaySettings(ref dm, 0);
            log("退出后还原显示模式：" + (rc == 0 ? "成功" : "失败 rc=" + rc));
        }
        catch (Exception ex) { log("还原显示模式失败：" + ex.Message); }
    }

    // 当前桌面模式（恢复进程判断「是否已还原」用），取不到返回 null
    public static int[] CurrentMode()
    {
        DEVMODEW dm = NewDevMode();
        if (EnumDisplaySettings(null, -1, ref dm))
            return new int[] { (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency };
        return null;
    }

    // 按宽/高/刷新率还原：从系统模式表里找完整参数的匹配项（含色深）套用；
    // 恢复进程（--restore）用——它拿不到主进程内存里的 DEVMODEW，只能靠启动器传参重建。
    // 找不到精确匹配就退而求其次（同宽高任意刷新率），再不行按裸参数顶上，绝不放弃还原。
    public static void RestoreByMode(int w, int h, int rate, Action<string> log)
    {
        try
        {
            DEVMODEW best = new DEVMODEW();
            bool found = false, loose = false;
            DEVMODEW dm = NewDevMode();
            for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
            {
                if ((int)dm.dmPelsWidth == w && (int)dm.dmPelsHeight == h)
                {
                    if ((int)dm.dmDisplayFrequency == rate) { best = dm; found = true; break; }
                    if (!loose) { best = dm; loose = true; }   // 同宽高第一条先记着
                }
                dm = NewDevMode();
            }
            if (!found && !loose)
            {
                best = NewDevMode();
                best.dmPelsWidth = (uint)Math.Max(640, w);
                best.dmPelsHeight = (uint)Math.Max(480, h);
                best.dmDisplayFrequency = (uint)Math.Max(60, rate);
            }
            best.dmFields = DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
            int rc = ChangeDisplaySettings(ref best, 0);
            log("恢复进程还原显示模式 " + best.dmPelsWidth + "×" + best.dmPelsHeight +
                "@" + best.dmDisplayFrequency + "：" + (rc == 0 ? "成功" : "失败 rc=" + rc));
        }
        catch (Exception ex) { log("恢复进程还原显示模式失败：" + ex.Message); }
    }
}
