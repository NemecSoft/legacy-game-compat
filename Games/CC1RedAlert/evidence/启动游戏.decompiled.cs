using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
internal static class PortableGame
{
	internal const string Name = "命令与征服：红色警戒";

	internal const string Mod = "ra";

	internal const string Executable = "RedAlert.exe";

	internal const string Title = "命令与征服：红色警戒 - WWW.XDGAME.COM";

	internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;

	internal static readonly string Engine = Path.Combine(Root, "Engine");

	internal static readonly string Support = Path.Combine(Engine, "Support");

	internal static readonly string GamePath = Path.Combine(Engine, "RedAlert.exe");

	[STAThread]
	private static int Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		using SHA256 sHA = SHA256.Create();
		using Mutex mutex = new Mutex(initiallyOwned: false, "Local\\XDGAME-OpenRA-" + BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(Root.ToUpperInvariant()))).Replace("-", ""));
		if (!mutex.WaitOne(0))
		{
			MessageBox.Show("本目录的游戏启动器已经在运行。", "命令与征服：红色警戒 - WWW.XDGAME.COM", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return 0;
		}
		try
		{
			Application.Run(new Launcher(args));
			return 0;
		}
		finally
		{
			mutex.ReleaseMutex();
		}
	}

	internal static void Prepare()
	{
		if (!File.Exists(GamePath))
		{
			throw new FileNotFoundException("游戏程序缺失，请完整解压整个游戏文件夹。", GamePath);
		}
		Directory.CreateDirectory(Support);
		string path = Path.Combine(Support, "settings.yaml");
		if (!File.Exists(path))
		{
			using FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
			using StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			streamWriter.Write("Graphics:\n\tMode: Windowed\n\tWindowedSize: 1280, 800\n\tCapFramerate: true\n\tMaxFramerate: 60\nPlayer:\n\tName: 指挥官\nGame:\n\tMouseControlStyle: Modern\n");
		}
		string path2 = Path.Combine(Engine, "mods", "ra", "fluent", "ra.ftl");
		string text = File.ReadAllText(path2, Encoding.UTF8);
		string text2 = Regex.Replace(text, "(?m)^mod-windowtitle\\s*=.*(?:\\r?\\n[ \\t]+[^\\r\\n]*)*", "mod-windowtitle = 命令与征服：红色警戒 - WWW.XDGAME.COM");
		if (!Regex.IsMatch(text2, "(?m)^mod-windowtitle\\s*="))
		{
			text2 += "\nmod-windowtitle = 命令与征服：红色警戒 - WWW.XDGAME.COM\n";
		}
		if (text != text2)
		{
			File.WriteAllText(path2, text2, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		string path3 = Path.Combine(Engine, "mods", "ra", "mod.yaml");
		text = File.ReadAllText(path3, Encoding.UTF8);
		text2 = Regex.Replace(text, "(?m)^\\tWindowTitle:[^\\r\\n]*", "\tWindowTitle: mod-windowtitle");
		if (!Regex.IsMatch(text2, "(?m)^\\tWindowTitle:"))
		{
			text2 = Regex.Replace(text2, "(?m)^Metadata:[^\\r\\n]*", "Metadata:\n\tWindowTitle: mod-windowtitle");
		}
		if (text != text2)
		{
			File.WriteAllText(path3, text2, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
	}

	internal static string Quote(string text)
	{
		StringBuilder stringBuilder = new StringBuilder("\"");
		int num = 0;
		foreach (char c in text)
		{
			if (c == '\\')
			{
				num++;
				continue;
			}
			stringBuilder.Append('\\', (c == '"') ? (num * 2 + 1) : num);
			stringBuilder.Append(c);
			num = 0;
		}
		stringBuilder.Append('\\', num * 2);
		return stringBuilder.Append('"').ToString();
	}
}
internal sealed class Launcher : Form
{
	private readonly Button start = new Button();

	private readonly Label status = new Label();

	private readonly System.Windows.Forms.Timer monitor = new System.Windows.Forms.Timer();

	private readonly string[] arguments;

	private Process game;

	internal Launcher(string[] args)
	{
		arguments = args;
		Text = "命令与征服：红色警戒 - WWW.XDGAME.COM";
		Font = new Font("Microsoft YaHei UI", 10f);
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(620, 360);
		MinimumSize = (MaximumSize = Size);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(27, 32, 40);
		ForeColor = Color.FromArgb(235, 238, 242);
		string text = Path.Combine(PortableGame.Engine, "ra.ico");
		if (File.Exists(text))
		{
			Icon = new Icon(text);
		}
		Label label = new Label
		{
			Text = "命令与征服：红色警戒",
			Font = new Font(Font.FontFamily, 19f, FontStyle.Bold),
			AutoSize = false,
			Bounds = new Rectangle(30, 28, 560, 45)
		};
		Label label2 = new Label
		{
			Text = "简体中文便携整合版  ·  OpenRA",
			Bounds = new Rectangle(32, 82, 550, 28)
		};
		Label label3 = new Label
		{
			Text = "中文菜单、单位说明、任务目标与战役简报\n首次启动自动创建本目录的设置和存档\n移动或备份时，请保留整个游戏文件夹",
			Bounds = new Rectangle(32, 126, 550, 84)
		};
		start.Text = "开始游戏";
		start.Bounds = new Rectangle(32, 232, 172, 46);
		start.Click += (object param0, EventArgs param1) =>
		{
			StartGame();
		};
		Button button = new Button
		{
			Text = "打开存档目录",
			Bounds = new Rectangle(224, 232, 172, 46)
		};
		button.Click += (object param0, EventArgs param1) =>
		{
			OpenPath(PortableGame.Support, directory: true);
		};
		Button button2 = new Button
		{
			Text = "阅读使用说明",
			Bounds = new Rectangle(416, 232, 172, 46)
		};
		button2.Click += (object param0, EventArgs param1) =>
		{
			OpenPath(Path.Combine(PortableGame.Root, "游戏启动说明.txt"), directory: false);
		};
		Button[] array = new Button[3] { start, button, button2 };
		foreach (Button button3 in array)
		{
			button3.FlatStyle = FlatStyle.Flat;
			button3.BackColor = Color.FromArgb(49, 64, 80);
			button3.ForeColor = ForeColor;
			button3.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 130);
		}
		status.Text = "就绪  ·  游戏内按 Esc 打开菜单，快捷键详见使用说明";
		status.Bounds = new Rectangle(32, 300, 556, 40);
		Controls.AddRange(new Control[7] { label, label2, label3, start, button, button2, status });
		AcceptButton = start;
		monitor.Interval = 500;
		monitor.Tick += (object param0, EventArgs param1) =>
		{
			MonitorGame();
		};
		monitor.Start();
		FormClosing += (object sender, FormClosingEventArgs e) =>
		{
			if (game != null && !game.HasExited)
			{
				e.Cancel = true;
				WindowState = FormWindowState.Minimized;
			}
		};
		Shown += (object param0, EventArgs param1) =>
		{
			if (arguments.Length > 0)
			{
				StartGame();
			}
		};
	}

	private void OpenPath(string path, bool directory)
	{
		try
		{
			if (directory)
			{
				Directory.CreateDirectory(path);
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(path);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "打开失败。\n" + ex.Message, "命令与征服：红色警戒 - WWW.XDGAME.COM", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void StartGame()
	{
		try
		{
			PortableGame.Prepare();
			StringBuilder stringBuilder = new StringBuilder();
			string[] array = arguments;
			foreach (string text in array)
			{
				if (!text.StartsWith("Engine.", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("Game.Mod=", StringComparison.OrdinalIgnoreCase))
				{
					stringBuilder.Append(PortableGame.Quote(text)).Append(' ');
				}
			}
			stringBuilder.Append("Game.Mod=").Append("ra").Append(' ');
			stringBuilder.Append(PortableGame.Quote("Engine.LaunchPath=" + PortableGame.GamePath));
			game = Process.Start(new ProcessStartInfo(PortableGame.GamePath, stringBuilder.ToString())
			{
				WorkingDirectory = PortableGame.Engine,
				UseShellExecute = false,
				CreateNoWindow = true
			});
			start.Enabled = false;
			status.Text = "游戏运行中。退出游戏后可再次启动。";
			WindowState = FormWindowState.Minimized;
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "游戏启动失败。\n" + ex.Message, "命令与征服：红色警戒 - WWW.XDGAME.COM", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void MonitorGame()
	{
		TitleProtection.RestoreCaption(Handle);
		if (game != null)
		{
			if (game.HasExited)
			{
				int exitCode = game.ExitCode;
				game.Dispose();
				game = null;
				start.Enabled = true;
				WindowState = FormWindowState.Normal;
				status.Text = ((exitCode == 0) ? "游戏已退出。" : "游戏异常退出，请查看存档目录内的 Logs 日志。");
			}
			else
			{
				TitleProtection.Restore(game.Id);
			}
		}
	}
}
internal static class TitleProtection
{
	private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool SetWindowText(IntPtr window, string text);

	internal static void Restore(int gamePid)
	{
		EnumWindows((IntPtr window, IntPtr parameter) =>
		{
			GetWindowThreadProcessId(window, out var pid);
			if (pid != gamePid)
			{
				return true;
			}
			StringBuilder stringBuilder = new StringBuilder(100);
			GetClassName(window, stringBuilder, stringBuilder.Capacity);
			if (stringBuilder.ToString() != "SDL_app")
			{
				return true;
			}
			RestoreCaption(window);
			return true;
		}, IntPtr.Zero);
	}

	internal static void RestoreCaption(IntPtr window)
	{
		StringBuilder stringBuilder = new StringBuilder(512);
		GetWindowText(window, stringBuilder, stringBuilder.Capacity);
		if (stringBuilder.ToString() != "命令与征服：红色警戒 - WWW.XDGAME.COM")
		{
			SetWindowText(window, "命令与征服：红色警戒 - WWW.XDGAME.COM");
		}
	}
}
