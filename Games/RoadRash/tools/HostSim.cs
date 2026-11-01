using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 启动器「平台托管契约」测试 GUI —— 开发工具，不进游戏目录交付
//
// 验的四条：
//   ① 游戏是 game_starter.exe 的直接子进程（平台按进程树能认）
//   ② 关启动器窗口后：启动器仍活着（隐藏陪跑）、游戏不受影响
//   ③ 游戏退出后 game_starter.exe 跟着退出，且 game_starter.exe 的退出码 == 游戏的退出码
//   ④ 强杀 game_starter.exe 后游戏继续跑，测试程序仍能读到游戏自己的退出码
//
// 编译：
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe ^
//     -platform:anycpu -codepage:65001 -r:System.Windows.Forms.dll -r:System.Drawing.dll ^
//     -out:hostsim.exe HostSim.cs
//
// 用法：
//   hostsim.exe                打开窗口手动走流程
//   hostsim.exe /auto          假游戏自动跑（不占屏），退出码=失败项数，日志 hostsim-auto.log
//   hostsim.exe /auto /real    用真游戏跑（到点自动结束游戏进程）

class HostSim : Form
{
    // ---------- Win32 ----------
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESSENTRY32
    {
        public int dwSize;
        public int cntUsage;
        public int th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public int th32ModuleID;
        public int cntThreads;
        public int th32ParentProcessID;
        public int pcPriClassBase;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);

    const uint TH32CS_SNAPPROCESS = 2;
    const uint PROCESS_QUERY = 0x0400 | 0x1000;
    const uint STILL_ACTIVE = 259;

    // ---------- 状态 ----------
    Process go;
    IntPtr goHandle = IntPtr.Zero;
    IntPtr gameHandle = IntPtr.Zero;
    int gamePid, gameParentPid;
    string gameName = "";
    uint? goCode, gameCode;
    string gameDir, iniPath, iniBackup;
    bool iniSwapped;
    int fakeExit = 7, fakeSeconds = 8, realKillAfter = 20;
    bool autoMode, realMode;
    string autoLog = "";
    readonly Dictionary<int, string> checks = new Dictionary<int, string>();

    // ---------- 控件 ----------
    TextBox log;
    Label status;
    CheckBox useFake;
    NumericUpDown numExit, numSec;
    Button bAutoA, bAutoB;
    readonly string[] CheckNames =
    {
        "① 游戏父进程 == game_starter.exe",
        "② 关窗后启动器仍活着 / 游戏不受影响",
        "③ game_starter.exe 退出码 == 游戏退出码",
        "④ 强杀 game_starter.exe 后仍读到游戏退出码"
    };

    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        HostSim h = new HostSim();
        foreach (string a in args)
        {
            if (a == "/auto" || a == "--auto") h.autoMode = true;
            if (a == "/real" || a == "--real") h.realMode = true;
        }
        Application.Run(h);
        return h.failed;
    }

    public int failed;

    public HostSim()
    {
        gameDir = FindGameDir();
        iniPath = Path.Combine(gameDir, "launcher.ini");
        iniBackup = Path.Combine(gameDir, "launcher.ini.hostsim-bak");
        for (int i = 0; i < CheckNames.Length; i++) checks[i] = "未测";

        Text = "主 GUI 模拟器 — game_starter.exe 托管契约测试";
        ClientSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Color.FromArgb(0x22, 0x25, 0x2B);
        ForeColor = Color.FromArgb(0xE6, 0xE8, 0xEC);

        int y = 14;
        AddLabel("游戏目录：" + gameDir, 14, y, 720); y += 26;

        useFake = new CheckBox();
        useFake.Text = "用假游戏（临时改 launcher.ini：cmd 睡 N 秒后以指定码退出，退出时还原）";
        useFake.Location = new Point(14, y);
        useFake.Size = new Size(560, 22);
        useFake.Checked = true;
        useFake.ForeColor = ForeColor;
        Controls.Add(useFake);

        AddLabel("退出码", 580, y + 2, 48);
        numExit = new NumericUpDown();
        numExit.Location = new Point(628, y); numExit.Size = new Size(48, 24);
        numExit.Minimum = 1; numExit.Maximum = 254; numExit.Value = fakeExit;
        Controls.Add(numExit);
        AddLabel("秒", 682, y + 2, 24);
        numSec = new NumericUpDown();
        numSec.Location = new Point(706, y); numSec.Size = new Size(44, 24);
        numSec.Minimum = 2; numSec.Maximum = 120; numSec.Value = fakeSeconds;
        Controls.Add(numSec);
        y += 34;

        bAutoA = AddButton("场景A：启动 /go → 关窗 → 等游戏退出", 14, y, 300, delegate { ScenarioA(); });
        bAutoB = AddButton("场景B：启动 /go → 强杀 game_starter.exe → 等游戏退出", 324, y, 300, delegate { ScenarioB(); });
        y += 40;

        AddButton("启动 game_starter.exe（交互）", 14, y, 160, delegate { StartLauncher(false); });
        AddButton("启动 game_starter.exe /go", 180, y, 130, delegate { StartLauncher(true); });
        AddButton("关启动器窗口", 316, y, 120, delegate { CloseLauncherWindow(); });
        AddButton("强杀 game_starter.exe", 442, y, 100, delegate { KillLauncher(); });
        AddButton("结束游戏进程", 548, y, 110, delegate { KillGame(); });
        AddButton("还原 ini", 664, y, 76, delegate { RestoreIni(); });
        y += 40;

        status = new Label();
        status.Location = new Point(14, y);
        status.Size = new Size(726, 76);
        status.Text = RenderChecks();
        Controls.Add(status);
        y += 82;

        log = new TextBox();
        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Both;
        log.WordWrap = false;
        log.Location = new Point(14, y);
        log.Size = new Size(726, ClientSize.Height - y - 14);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        log.BackColor = Color.FromArgb(0x18, 0x1A, 0x1F);
        log.ForeColor = Color.FromArgb(0xD8, 0xDE, 0xE6);
        log.Font = new Font("Consolas", 9f);
        Controls.Add(log);

        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 250;
        t.Tick += delegate { Poll(); status.Text = RenderChecks(); };
        t.Start();

        Shown += delegate
        {
            Log("游戏目录 " + gameDir);
            if (!File.Exists(Path.Combine(gameDir, "game_starter.exe"))) Log("警告：没找到 game_starter.exe，先跑 build.ps1 部署");
            if (autoMode) RunAuto();
            else Log("点「场景A/B」自动跑，或按按钮手动走：启动 → 关窗 → 游戏退出，看四条断言");
        };
        FormClosed += delegate { RestoreIni(); };
    }

    static string FindGameDir()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string[] candidates =
        {
            Environment.GetEnvironmentVariable("ROADRASH_DIR"),
            Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\..\YunGame\Race\RoadRash")),
            @"D:\YunGame\Race\RoadRash"
        };
        foreach (string c in candidates)
            if (!string.IsNullOrEmpty(c) && File.Exists(Path.Combine(c, "game_starter.exe"))) return c;
        return @"D:\YunGame\Race\RoadRash";
    }

    Label AddLabel(string text, int x, int y, int w)
    {
        Label l = new Label();
        l.Text = text;
        l.Location = new Point(x, y);
        l.Size = new Size(w, 20);
        l.ForeColor = Color.FromArgb(0xB8, 0xC2, 0xD0);
        Controls.Add(l);
        return l;
    }

    Button AddButton(string text, int x, int y, int w, EventHandler act)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 30);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(0x4A, 0x52, 0x60);
        b.BackColor = Color.FromArgb(0x2C, 0x31, 0x39);
        b.ForeColor = Color.FromArgb(0xE6, 0xE8, 0xEC);
        b.Click += act;
        Controls.Add(b);
        return b;
    }

    void Log(string m)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + m;
        autoLog += line + Environment.NewLine;
        log.AppendText(line + "\r\n");
        Application.DoEvents();
    }

    string RenderChecks()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < CheckNames.Length; i++)
        {
            sb.Append(CheckNames[i]).Append("  →  ").Append(checks[i]);
            if (i < CheckNames.Length - 1) sb.Append("\r\n");
        }
        sb.Append("\r\n进程：game_starter.exe ")
          .Append(go == null ? "-" : go.Id.ToString())
          .Append("  退出码 ").Append(Fmt(goCode))
          .Append("   |   游戏 ").Append(gamePid == 0 ? "未发现" : gameName + "(" + gamePid + ")")
          .Append("  退出码 ").Append(Fmt(gameCode));
        return sb.ToString();
    }

    static string Fmt(uint? code)
    {
        if (code == null) return "未退出";
        uint u = code.Value;
        return ((int)u).ToString() + " (0x" + u.ToString("X8") + ")";
    }

    // ---------- 生命周期 ----------
    void StartLauncher(bool zeroClick)
    {
        KillAll();
        fakeExit = (int)numExit.Value;
        fakeSeconds = (int)numSec.Value;
        if (useFake.Checked && !realMode) UseFakeGame();
        go = zeroClick
            ? Process.Start(Path.Combine(gameDir, "game_starter.exe"), "/go")
            : Process.Start(Path.Combine(gameDir, "game_starter.exe"));
        goHandle = OpenProcess(PROCESS_QUERY, false, go.Id);
        goCode = null; gameCode = null; gamePid = 0; gameParentPid = 0; gameName = "";
        if (gameHandle != IntPtr.Zero) { CloseHandle(gameHandle); gameHandle = IntPtr.Zero; }
        Log("已启动 game_starter.exe  pid=" + go.Id + (zeroClick ? "  (/go 零交互)" : "  (交互)"));
    }

    void KillAll()
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName("game_starter")) p.Kill();
            foreach (Process p in Process.GetProcessesByName("Roadrash")) p.Kill();
        }
        catch { }
    }

    void CloseLauncherWindow()
    {
        if (go == null || go.HasExited) { Log("game_starter.exe 没在跑"); return; }
        go.CloseMainWindow();
        Log("已请求关窗（期望：game_starter.exe 转隐藏陪跑，游戏不动）");
    }

    void KillLauncher()
    {
        if (go == null || go.HasExited) { Log("game_starter.exe 没在跑"); return; }
        go.Kill();
        Log("已强杀 game_starter.exe（期望：游戏继续跑）");
    }

    void KillGame()
    {
        if (gamePid == 0) { Log("还没认出游戏进程"); return; }
        try { Process.GetProcessById(gamePid).Kill(); Log("已结束游戏进程 " + gamePid); }
        catch (Exception ex) { Log("结束游戏失败：" + ex.Message); }
    }

    // ---------- 轮询 ----------
    void FindGame()
    {
        if (gamePid != 0 || go == null) return;
        foreach (PROCESSENTRY32 e in Snapshot())
        {
            if (e.th32ParentProcessID != go.Id) continue;
            string baseName = Path.GetFileNameWithoutExtension(e.szExeFile);
            if (baseName == "game_starter" || baseName == "conhost" || baseName == "keymap") continue;
            string want = ReadGameExeName();
            if (want.Length > 0 && baseName != want) continue;
            gamePid = e.th32ProcessID;
            gameName = e.szExeFile;
            gameParentPid = e.th32ParentProcessID;
            gameHandle = OpenProcess(PROCESS_QUERY, false, gamePid);
            Log("发现游戏子进程：" + gameName + " pid=" + gamePid + " 父 pid=" + gameParentPid);
            checks[0] = gameParentPid == go.Id ? "PASS" : "FAIL 父 pid=" + gameParentPid;
            return;
        }
    }

    static IEnumerable<PROCESSENTRY32> Snapshot()
    {
        List<PROCESSENTRY32> list = new List<PROCESSENTRY32>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) yield break;
        PROCESSENTRY32 e = new PROCESSENTRY32();
        e.dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32));
        if (Process32First(snap, ref e))
        {
            do { list.Add(e); } while (Process32Next(snap, ref e));
        }
        CloseHandle(snap);
        foreach (PROCESSENTRY32 item in list) yield return item;
    }

    string ReadGameExeName()
    {
        try
        {
            foreach (string line in File.ReadAllLines(iniPath))
            {
                string l = line.Trim();
                if (!l.StartsWith("GameExe", StringComparison.OrdinalIgnoreCase)) continue;
                int eq = l.IndexOf('=');
                if (eq <= 0) continue;
                return Path.GetFileNameWithoutExtension(l.Substring(eq + 1).Trim());
            }
        }
        catch { }
        return "";
    }

    void Poll()
    {
        FindGame();
        if (gamePid != 0 && gameCode == null)
        {
            uint c;
            bool alive = gameHandle != IntPtr.Zero && GetExitCodeProcess(gameHandle, out c) && c == STILL_ACTIVE;
            if (!alive)
            {
                if (gameHandle != IntPtr.Zero && GetExitCodeProcess(gameHandle, out c) && c != STILL_ACTIVE)
                {
                    gameCode = c;
                    Log("游戏已退出，退出码 = " + Fmt(c));
                }
                else if (!ProcessIdExists(gamePid))
                {
                    gameCode = 0xFFFFFFFF;   // 进程没了但句柄没抓到码（极少见），记哨兵值
                    Log("游戏进程已消失（句柄取不到码，记 0xFFFFFFFF）");
                }
                if (gameHandle != IntPtr.Zero && gameCode != null) { CloseHandle(gameHandle); gameHandle = IntPtr.Zero; }
            }
        }
        if (go != null && goCode == null)
        {
            uint c;
            if (goHandle != IntPtr.Zero && GetExitCodeProcess(goHandle, out c) && c != STILL_ACTIVE)
            {
                goCode = c;
                Log("game_starter.exe 已退出，退出码 = " + Fmt(c));
                CloseHandle(goHandle); goHandle = IntPtr.Zero;
            }
        }
    }

    bool ProcessIdExists(int pid)
    {
        foreach (PROCESSENTRY32 e in Snapshot()) if (e.th32ProcessID == pid) return true;
        return false;
    }

    bool Wait(Func<bool> cond, int ms)
    {
        int t = 0;
        while (t < ms)
        {
            Poll();
            if (cond()) return true;
            Thread.Sleep(120);
            t += 120;
            Application.DoEvents();
        }
        Poll();
        return cond();
    }

    bool WaitGameEnd()
    {
        int limit = realMode ? realKillAfter * 1000 : (fakeSeconds + 20) * 1000;
        bool got = Wait(delegate { return gameCode != null; }, limit);
        if (!got && gamePid != 0)
        {
            Log("游戏不自己退，到点结束它 pid=" + gamePid);
            KillGame();
            got = Wait(delegate { return gameCode != null; }, 8000);
        }
        return got;
    }

    // ---------- 场景 ----------
    void ScenarioA()
    {
        Log("—— 场景 A：关窗（隐藏陪跑）→ 游戏退出 → game_starter.exe 应回传游戏退出码 ——");
        StartLauncher(true);
        if (!Wait(delegate { return gamePid != 0; }, 40000)) { checks[0] = "FAIL 没等到游戏"; return; }
        go.CloseMainWindow();
        Thread.Sleep(1500);
        Poll();
        bool launcherAlive = goCode == null && !go.HasExited;
        bool gameAlive = gameCode == null && ProcessIdExists(gamePid);
        checks[1] = (launcherAlive && gameAlive)
            ? "PASS"
            : "FAIL 启动器活着=" + launcherAlive + " 游戏活着=" + gameAlive;
        Log("关窗后：game_starter.exe " + (launcherAlive ? "仍在（隐藏陪跑）" : "已退出") +
            "，游戏 " + (gameAlive ? "仍在跑" : "已退出"));
        if (WaitGameEnd())
        {
            Wait(delegate { return goCode != null; }, 8000);
            bool same = goCode != null && gameCode != null && goCode.Value == gameCode.Value;
            checks[2] = same ? "PASS " + Fmt(gameCode) : "FAIL go=" + Fmt(goCode) + " game=" + Fmt(gameCode);
        }
        else checks[2] = "FAIL 没等到游戏退出";
    }

    void ScenarioB()
    {
        Log("—— 场景 B：强杀 game_starter.exe → 游戏继续 → 仍应读到游戏退出码 ——");
        StartLauncher(true);
        if (!Wait(delegate { return gamePid != 0; }, 40000)) { checks[0] = "FAIL 没等到游戏"; return; }
        int pid = gamePid;
        go.Kill();
        Thread.Sleep(1500);
        Poll();
        bool gameAlive = ProcessIdExists(pid);
        Log("强杀 game_starter.exe 后：游戏 " + (gameAlive ? "仍在跑" : "被带走了"));
        if (!gameAlive) { checks[3] = "FAIL 游戏被带走"; return; }
        bool got = WaitGameEnd();
        checks[3] = got
            ? "PASS 游戏码 " + Fmt(gameCode) + "（game_starter.exe 码 " + Fmt(goCode) + "，别拿它判异常）"
            : "FAIL 没读到游戏退出码";
    }

    void RunAuto()
    {
        useFake.Checked = !realMode;
        ScenarioA();
        ScenarioB();
        if (!realMode && gameCode != null && (int)gameCode.Value != fakeExit)
            Log("FAIL 期望游戏退出码 " + fakeExit + "，实得 " + Fmt(gameCode));
        failed = 0;
        foreach (KeyValuePair<int, string> kv in checks)
        {
            Log("  " + CheckNames[kv.Key] + " → " + kv.Value);
            if (kv.Value.StartsWith("FAIL")) failed++;
        }
        Log(failed == 0 ? "PASS 托管契约四条全通过" : "FAIL 共 " + failed + " 项不通过");
        RestoreIni();
        try { File.WriteAllText(Path.Combine(AppDir(), "hostsim-auto.log"), autoLog, Encoding.UTF8); }
        catch { }
        Close();
    }

    static string AppDir() { return Path.GetDirectoryName(Application.ExecutablePath); }

    // ---------- 假游戏 ----------
    void UseFakeGame()
    {
        if (!File.Exists(iniPath)) { Log("找不到 " + iniPath); return; }
        if (!iniSwapped)
        {
            File.Copy(iniPath, iniBackup, true);
            iniSwapped = true;
        }
        string ini = "[Launcher]\r\n" +
            "Title=托管链路测试（假游戏）\r\n" +
            "GameExe=C:\\Windows\\System32\\cmd.exe\r\n" +
            "GameArgs=/c ping -n " + fakeSeconds + " 127.0.0.1 >nul & exit " + fakeExit + "\r\n" +
            "AutoLoadSave=0\r\n" +
            "LayerDll=ddraw.dll\r\n" +
            "LayerIni=ddraw.ini\r\n" +
            "LayerBackupDir=_cnc-ddraw\r\n" +
            "SavesDir=各选手1-5级存档\r\n";
        File.WriteAllText(iniPath, ini, new UTF8Encoding(false));
        Log("launcher.ini → 假游戏（睡 " + fakeSeconds + " 秒，退出码 " + fakeExit + "）");
    }

    void RestoreIni()
    {
        if (!iniSwapped) return;
        try
        {
            File.Copy(iniBackup, iniPath, true);
            File.Delete(iniBackup);
            iniSwapped = false;
            Log("launcher.ini 已还原");
        }
        catch (Exception ex) { Log("还原 ini 失败：" + ex.Message); }
    }
}
