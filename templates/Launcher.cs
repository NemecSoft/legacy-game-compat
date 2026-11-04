using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Simple .NET Framework launcher (no third-party dependency).
// Reads launcher.ini next to the exe, starts the game, waits for it to exit.
//
// Build:
//   csc -nologo -target:winexe -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:Launcher.exe Launcher.cs

class Launcher : Form
{
    string gameTitle = "老游戏启动器";
    string gameExe = "";
    string loaderExe = "";
    string gameProcess = "";
    string preLaunch = "";
    string notes = "";

    Label lblTitle, lblNotes, lblStatus;
    Button btnStart, btnFolder, btnExit;

    [STAThread]
    static void Main() { Application.Run(new Launcher()); }

    public Launcher()
    {
        LoadIni();
        Text = gameTitle;
        ClientSize = new Size(580, 340);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        BackColor = Color.FromArgb(0x2C, 0x18, 0x10);   // 皮革深棕（拟物皮革主题）
        ForeColor = Color.FromArgb(0xF4, 0xE3, 0xC6);    // 米黄正文
        Font = new Font("Microsoft YaHei", 9f);

        lblTitle = new Label();
        lblTitle.Text = gameTitle;
        lblTitle.Font = new Font("Microsoft YaHei", 16f, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(0xF0, 0xD5, 0x96); // 烫金标题
        lblTitle.AutoSize = false;
        lblTitle.Location = new Point(24, 20);
        lblTitle.Size = new Size(532, 40);

        lblNotes = new Label();
        lblNotes.Text = notes;
        lblNotes.ForeColor = Color.FromArgb(0xD8, 0xBD, 0x96);
        lblNotes.Location = new Point(26, 68);
        lblNotes.Size = new Size(530, 150);

        btnStart = MakeButton("启动游戏", 24, 232, 150);
        btnStart.Click += delegate { StartGame(); };

        btnFolder = MakeButton("打开游戏目录", 186, 232, 130);
        btnFolder.Click += delegate { Process.Start("explorer.exe", "\"" + AppDir() + "\""); };

        btnExit = MakeButton("退出", 328, 232, 90);
        btnExit.Click += delegate { Close(); };

        lblStatus = new Label();
        lblStatus.Text = "就绪";
        lblStatus.ForeColor = Color.FromArgb(0xD9, 0xA4, 0x41);
        lblStatus.Location = new Point(26, 290);
        lblStatus.AutoSize = true;

        Controls.AddRange(new Control[] { lblTitle, lblNotes, btnStart, btnFolder, btnExit, lblStatus });
    }

    Button MakeButton(string text, int x, int y, int w)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, 38);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(0xB9, 0x8A, 0x55);
        b.BackColor = Color.FromArgb(0x5A, 0x34, 0x21);
        b.ForeColor = Color.FromArgb(0xF4, 0xE3, 0xC6);
        return b;
    }

    static string AppDir()
    {
        return Path.GetDirectoryName(Application.ExecutablePath);
    }

    void LoadIni()
    {
        string path = Path.Combine(AppDir(), "launcher.ini");
        if (!File.Exists(path)) return;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string k = line.Substring(0, eq).Trim().ToLowerInvariant();
            string v = line.Substring(eq + 1).Trim();
            switch (k)
            {
                case "title": gameTitle = v; break;
                case "gameexe": gameExe = v; break;
                case "loaderexe": loaderExe = v; break;
                case "gameprocess": gameProcess = v; break;
                case "prelaunch": preLaunch = v; break;
                case "notes": notes = v.Replace("\\n", "\r\n"); break;
            }
        }
    }

    void StartGame()
    {
        string dir = AppDir();
        if (!string.IsNullOrEmpty(loaderExe) && !File.Exists(Path.Combine(dir, loaderExe)))
        {
            MessageBox.Show(this, "找不到启动器: " + loaderExe, "错误");
            return;
        }
        if (!string.IsNullOrEmpty(loaderExe) && string.IsNullOrEmpty(gameProcess))
        {
            MessageBox.Show(this, "launcher.ini 里配置了 loaderExe 时必须同时配置 gameProcess", "配置错误");
            return;
        }
        if (string.IsNullOrEmpty(loaderExe) && !File.Exists(Path.Combine(dir, gameExe)))
        {
            MessageBox.Show(this, "找不到游戏程序: " + gameExe, "错误");
            return;
        }

        SetBusy(true, "启动中...");

        Thread t = new Thread(delegate ()
        {
            try
            {
                if (!string.IsNullOrEmpty(preLaunch))
                    RunAndWait(dir, "cmd.exe", "/c \"" + preLaunch + "\"");

                if (string.IsNullOrEmpty(loaderExe))
                {
                    RunAndWait(dir, Path.Combine(dir, gameExe), "");
                }
                else
                {
                    RunAndWait(dir, Path.Combine(dir, loaderExe), "");
                    int waited = 0;
                    while (waited < 120000 && countProc(gameProcess) == 0)
                    {
                        Thread.Sleep(500);
                        waited += 500;
                    }
                    while (countProc(gameProcess) > 0) Thread.Sleep(1000);
                }
                SetStatus("游戏已退出");
            }
            catch (Exception ex)
            {
                SetStatus("出错了");
                MessageBox.Show(ex.Message, "错误");
            }
            finally
            {
                SetBusy(false, null);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    static void RunAndWait(string dir, string file, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(file, args);
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false;
        Process p = Process.Start(psi);
        p.WaitForExit();
    }

    static int countProc(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        try { return Process.GetProcessesByName(name).Length; }
        catch { return 0; }
    }

    void SetBusy(bool busy, string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), busy, text); return; }
        btnStart.Enabled = !busy;
        btnFolder.Enabled = !busy;
        btnStart.Text = busy ? "游戏运行中..." : "启动游戏";
        if (text != null) lblStatus.Text = text;
    }

    void SetStatus(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(SetStatus), text); return; }
        lblStatus.Text = text;
    }
}
