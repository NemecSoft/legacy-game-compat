# -*- coding: utf-8 -*-
# 给 Heroes3 启动器打补丁：现场快照（抓除零用的 0 值）+ 自动改窗口 + 异常早退重试 + 记住能稳住的方式
import io

p = r'D:\AI\legacy-game-compat\Games\Heroes3\src\Launcher.cs'
t = io.open(p, encoding='utf-8', newline='').read().replace('\r\n', '\n')

# ---------- 1) 新的 P/Invoke ----------
anchor1 = '''    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
'''
add1 = anchor1 + '''
    // ---------- 除零类崩溃的取证用 API ----------
    const int VREFRESH = 0, HORZRES = 8, VERTRES = 10, LOGPIXELSX = 88, LOGPIXELSY = 90;
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool GetDiskFreeSpaceA(string root, out uint bytesPerSector,
        out uint sectorsPerCluster, out uint freeClusters, out uint totalClusters);
'''
assert anchor1 in t
t = t.replace(anchor1, add1, 1)

# ---------- 2) 快照 + 记忆 ----------
anchor2 = '''    // ---------- 启动逻辑 ----------
    void StartGame()'''
helpers = '''    // ---------- 现场快照 ----------
    // 0xC0000094 是整数除零。这游戏是 1999 年的代码，导入表里就有 GetDeviceCaps 和 GetDiskFreeSpaceA：
    // 系统把刷新率报成 0、或某个盘的扇区/簇大小报成 0，它拿去当除数就直接炸。
    // 这里把这两个来源原样记进 运行日志.txt，并据此决定要不要改用窗口模式。
    bool SnapshotEnvironment(out string reason)
    {
        reason = "";
        int refresh = 0, cx = 0, cy = 0, dpiX = 0, dpiY = 0;
        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                refresh = GetDeviceCaps(hdc, VREFRESH);
                cx = GetDeviceCaps(hdc, HORZRES);
                cy = GetDeviceCaps(hdc, VERTRES);
                dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
        }
        catch (Exception ex) { Log("取显示参数失败：" + ex.Message); }
        int screens = 1;
        try { screens = Screen.AllScreens.Length; } catch { }

        string root = "";
        try { root = Path.GetPathRoot(AppDir()); } catch { }
        uint bps = 0, spc = 0, freeC = 0, totalC = 0;
        bool diskOk = false;
        if (root.Length > 0)
        {
            try { diskOk = GetDiskFreeSpaceA(root, out bps, out spc, out freeC, out totalC); }
            catch (Exception ex) { Log("取磁盘参数失败：" + ex.Message); }
        }
        string floppy = "";
        try
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                string n = d.Name;
                if (n != "A:\\\\" && n != "B:\\\\") continue;
                uint a, b, c, e;
                bool ok = GetDiskFreeSpaceA(n, out a, out b, out c, out e);
                floppy += " " + n + (ok ? ("(每扇区" + a + " 每簇" + b + ")") : "(读不到)");
            }
        }
        catch { }

        Log("现场快照：显示 " + cx + "×" + cy + " 刷新率=" + refresh + " DPI=" + dpiX + "/" + dpiY +
            " 屏幕数=" + screens + "；游戏盘 " + root + " 每扇区字节=" + bps + " 每簇扇区=" + spc +
            " 簇 总" + totalC + "/可用" + freeC + (diskOk ? "" : "（GetDiskFreeSpace 失败）") +
            (floppy.Length == 0 ? "" : "；软驱" + floppy) +
            "；AppPath=" + RegValue("AppPath") + " CDDrive=" + RegValue("CDDrive"));

        List<string> zero = new List<string>();
        if (refresh <= 0) zero.Add("刷新率=" + refresh);
        if (bps == 0 || spc == 0) zero.Add("游戏盘每扇区字节/每簇扇区=" + bps + "/" + spc);
        if (cx <= 0 || cy <= 0) zero.Add("桌面尺寸=" + cx + "×" + cy);
        if (zero.Count == 0) return false;
        reason = string.Join("、", zero.ToArray());
        return true;
    }

    static string RegValue(string name)
    {
        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (RegistryKey k = baseKey.OpenSubKey(
                @"SOFTWARE\\New World Computing\\Heroes of Might and Magic?III\\1.0"))
            {
                if (k == null) return "（键不存在）";
                object v = k.GetValue(name);
                return v == null ? "（无此值）" : Convert.ToString(v);
            }
        }
        catch (Exception ex) { return "（读失败:" + ex.Message + "）"; }
    }

    // ---------- 记住这台机器验证过能玩的画面方式 ----------
    static string ModeMemoryPath() { return Path.Combine(AppDir(), "显示方式记忆.txt"); }

    static int ReadModeMemory()
    {
        try
        {
            if (!File.Exists(ModeMemoryPath())) return -1;
            int v;
            if (!int.TryParse(File.ReadAllText(ModeMemoryPath(), Encoding.UTF8).Trim(), out v)) return -1;
            return v;
        }
        catch { return -1; }
    }

    static void SaveModeMemory(int which)
    {
        try { File.WriteAllText(ModeMemoryPath(), which.ToString(), Encoding.UTF8); }
        catch (Exception ex) { Log("记画面方式失败：" + ex.Message); }
    }

''' + anchor2
assert anchor2 in t
t = t.replace(anchor2, helpers, 1)

# ---------- 3) 启动线程：快照 -> 必要时改窗口 -> 异常早退重试 -> 记住能稳的 ----------
old_body = '''        string launchProcess = gameProcess.Length > 0 ? gameProcess : "Heroes3";
        Log("点「启动游戏」：" + ModeNames[mode]);
        SetBusy(true, "启动中：" + ModeNames[mode].Substring(ModeNames[mode].IndexOf(' ') + 1));
        Thread t = new Thread(delegate ()
        {
            try
            {
                SetStatus("游戏运行中…", Ok);
                PrepareGame();
                EnsureRegistry();
                File.Copy(presetPath, Path.Combine(AppDir(), "ddraw.ini"), true);
                int code = RunGameWithAffinity(exePath, gameArgs, launchProcess);
                Log("游戏退出，退出码 0x" + ((uint)code).ToString("X8") + "，启动器随之退出");
                FinishWith(code);
            }'''
new_body = '''        string launchProcess = gameProcess.Length > 0 ? gameProcess : "Heroes3";
        Log("点「启动游戏」：" + ModeNames[mode]);
        SetBusy(true, "启动中：" + ModeNames[mode].Substring(ModeNames[mode].IndexOf(' ') + 1));
        Thread t = new Thread(delegate ()
        {
            int usedMode = mode;
            try
            {
                PrepareGame();
                EnsureRegistry();
                string why;
                if (SnapshotEnvironment(out why) && usedMode != WINDOW_MODE && usedMode != 3)
                {
                    Log("检测到能当除数的 0 值（" + why + "），本次改用「" + ModeNames[WINDOW_MODE] + "」");
                    usedMode = WINDOW_MODE;
                    SetStatus("已自动改用窗口画面，避免闪退", Ok);
                }
                int attempt = 0;
                while (true)
                {
                    attempt++;
                    string use = Path.Combine(AppDir(), "_presets", ModePresets[usedMode]);
                    if (!File.Exists(use))
                    {
                        Log("显示预设缺失：" + use + "，改用默认预设");
                        use = presetPath;
                    }
                    File.Copy(use, Path.Combine(AppDir(), "ddraw.ini"), true);
                    SetStatus("游戏运行中…", Ok);
                    DateTime start = DateTime.Now;
                    int code = RunGameWithAffinity(exePath, gameArgs, launchProcess);
                    int secs = (int)(DateTime.Now - start).TotalSeconds;
                    bool bad = (uint)code >= 0xC0000000;
                    Log("游戏退出，用时 " + secs + "s，退出码 0x" + ((uint)code).ToString("X8") +
                        (bad ? "（系统级异常）" : ""));
                    if (bad && secs < 25 && attempt == 1 && usedMode != WINDOW_MODE && usedMode != 3)
                    {
                        Log("开局就异常退出，改用「" + ModeNames[WINDOW_MODE] + "」再试一次");
                        SetStatus("游戏闪退，已自动改用窗口重试", Ok);
                        usedMode = WINDOW_MODE;
                        continue;
                    }
                    if (secs >= 45)
                    {
                        SaveModeMemory(usedMode);
                        if (usedMode != mode) Log("这个画面方式在这台机器上稳住了，已记住，下次直接用它");
                    }
                    FinishWith(code);
                    break;
                }
            }'''
assert old_body in t
t = t.replace(old_body, new_body, 1)

# ---------- 4) 常量 + 启动时读取记忆 ----------
anchor4 = '''    // ---------- 运行状态 ----------
    const int NoFocus = -99;'''
t = t.replace(anchor4, '''    // 窗口模式（1280×720）：除零类闪退的兜底方式
    const int WINDOW_MODE = 2;

''' + anchor4, 1)

anchor5 = '''        Launcher l = new Launcher();
        foreach (string a in args)
        {
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;'''
t = t.replace(anchor5, '''        Launcher l = new Launcher();
        int remembered = ReadModeMemory();
        if (remembered >= 0 && remembered < ModeNames.Length) l.mode = remembered;
        foreach (string a in args)
        {
            if (a == "/go" || a == "-go" || a == "--go" || a == "/auto") l.autoStart = true;''', 1)

io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))
print('Launcher.cs 已打补丁')
