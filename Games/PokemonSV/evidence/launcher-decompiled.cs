using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: AssemblyVersion("0.0.0.0")]
namespace KarPurPortable;

internal static class Integrity
{
	internal static readonly string Root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);

	internal static readonly string Runtime = Path.Combine(Root, "runtime");

	internal static readonly string Title = DecodeTitle();

	private static string DecodeTitle()
	{
		char[] array = new char[BrandData.TitleData.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (char)(BrandData.TitleData[i] ^ ((26963 + i * 97) & 0xFFFF));
		}
		return new string(array);
	}

	internal static void VerifySelf()
	{
		using FileStream fileStream = File.OpenRead(Assembly.GetExecutingAssembly().Location);
		using SHA256 sHA = SHA256.Create();
		if (fileStream.Length < 264)
		{
			throw new InvalidDataException("启动器签名缺失，请恢复原版文件。");
		}
		long num = fileStream.Length - 264;
		byte[] array = new byte[1048576];
		while (num > 0)
		{
			int num2 = fileStream.Read(array, 0, (int)Math.Min(array.Length, num));
			if (num2 == 0)
			{
				throw new EndOfStreamException("启动器文件不完整。");
			}
			sHA.TransformBlock(array, 0, num2, null, 0);
			num -= num2;
		}
		sHA.TransformFinalBlock(new byte[0], 0, 0);
		byte[] array2 = new byte[256];
		byte[] array3 = new byte[8];
		if (fileStream.Read(array2, 0, array2.Length) != array2.Length || fileStream.Read(array3, 0, array3.Length) != array3.Length || Encoding.ASCII.GetString(array3) != "XDKPLA38")
		{
			throw new InvalidDataException("启动器签名损坏，请恢复原版文件。");
		}
		CspParameters cspParameters = new CspParameters(24);
		cspParameters.Flags = CspProviderFlags.CreateEphemeralKey;
		using RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider(cspParameters);
		rSACryptoServiceProvider.PersistKeyInCsp = false;
		rSACryptoServiceProvider.ImportParameters(new RSAParameters
		{
			Modulus = BrandData.Modulus,
			Exponent = new byte[3] { 1, 0, 1 }
		});
		if (!rSACryptoServiceProvider.VerifyHash(sHA.Hash, CryptoConfig.MapNameToOID("SHA256"), array2))
		{
			throw new InvalidDataException("启动器已被修改，请恢复原版文件。");
		}
	}

	internal static void VerifyRuntime()
	{
		Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("integrity");
		if (manifestResourceStream == null)
		{
			throw new InvalidDataException("启动器缺少校验数据。");
		}
		using StreamReader streamReader = new StreamReader(manifestResourceStream, Encoding.UTF8);
		using SHA256 sHA = SHA256.Create();
		string text;
		while ((text = streamReader.ReadLine()) != null)
		{
			if (text.Length != 0)
			{
				string[] array = text.Split('\t');
				if (array.Length != 2)
				{
					throw new InvalidDataException("启动器校验数据损坏。");
				}
				string fullPath = Path.GetFullPath(Path.Combine(Runtime, array[1]));
				if (!fullPath.StartsWith(Runtime + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("启动器校验路径无效。");
				}
				if (!File.Exists(fullPath))
				{
					throw new FileNotFoundException("缺少游戏文件，请重新解压完整目录。\n" + array[1]);
				}
				string a;
				using (FileStream inputStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.SequentialScan))
				{
					a = BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant();
				}
				if (!string.Equals(a, array[0], StringComparison.Ordinal))
				{
					throw new InvalidDataException("游戏文件校验失败，请恢复原版文件。\n" + array[1]);
				}
			}
		}
	}
}
internal static class Program
{
	internal static string Edition = "violet";

	internal static string[] LaunchArguments = new string[0];

	internal static bool QaMode;

	internal static int QaExitCode;

	internal static string Root => Integrity.Root;

	internal static string Runtime => Path.Combine(Integrity.Runtime, Edition);

	internal static string Caption => Integrity.Title;

	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length > 1 && (args[1] == "violet" || args[1] == "scarlet"))
		{
			Edition = args[1];
			string[] array = new string[args.Length - 1];
			array[0] = args[0];
			Array.Copy(args, 2, array, 1, args.Length - 2);
			args = array;
		}
		bool flag = args.Length > 0;
		try
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
			Integrity.VerifySelf();
			if (args.Length == 1 && args[0] == "--verify-self")
			{
				return 0;
			}
			if (args.Length == 1 && args[0] == "--verify")
			{
				CheckFiles();
				Integrity.VerifyRuntime();
				return 0;
			}
			if (args.Length == 2 && args[0] == "--save-path")
			{
				File.WriteAllText(args[1], SaveFolder(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				return 0;
			}
			if (args.Length > 0 && args[0] == "--launch")
			{
				LaunchArguments = Tail(args, 1);
				using Process process = StartGame();
				process.WaitForExit();
				return process.ExitCode;
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			LauncherForm form = new LauncherForm();
			try
			{
				if (args.Length == 2 && args[0] == "--self-test")
				{
					CheckFiles();
					Integrity.VerifyRuntime();
					form.Shown += (object param0, EventArgs param1) =>
					{
						Native.SetWindowText(form.Handle, "修改标题测试");
						StringBuilder stringBuilder = new StringBuilder(256);
						Native.GetWindowText(form.Handle, stringBuilder, stringBuilder.Capacity);
						if (stringBuilder.ToString() != Caption)
						{
							throw new InvalidDataException("标题保护检查失败。");
						}
						Capture(form, args[1]);
						using (SettingsForm settingsForm = new SettingsForm())
						{
							settingsForm.Show(form);
							settingsForm.Update();
							Capture(settingsForm, args[1] + ".settings.png");
							settingsForm.Close();
						}
						form.Close();
					};
				}
				if (args.Length >= 2 && args[0] == "--qa-entry")
				{
					QaMode = true;
					LaunchArguments = Tail(args, 2);
					form.Shown += (object param0, EventArgs param1) =>
					{
						Capture(form, args[1]);
						form.StartForQa();
					};
				}
				Application.Run(form);
			}
			finally
			{
				if (form != null)
				{
					((IDisposable)form).Dispose();
				}
			}
			return QaExitCode;
		}
		catch (Exception ex)
		{
			if (flag)
			{
				Console.Error.WriteLine(ex.Message);
			}
			if (!flag)
			{
				MessageBox.Show(ex.Message, "启动提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			return 3;
		}
	}

	private static string[] Tail(string[] values, int start)
	{
		string[] array = new string[values.Length - start];
		Array.Copy(values, start, array, 0, array.Length);
		return array;
	}

	private static void Capture(Form form, string path)
	{
		using Bitmap bitmap = new Bitmap(form.Width, form.Height);
		form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
		bitmap.Save(path);
	}

	internal static void CheckFiles()
	{
		string[] array = new string[9] { "karpur.exe", "SDL3.dll", "libopus-0.dll", "libiconv-2.dll", "totk_xlate.exe", "romfs-index.bin", "exefs/main", "exefs/main.npdm", "romfs/romfs.bin" };
		foreach (string text in array)
		{
			if (!File.Exists(Path.Combine(Runtime, text)))
			{
				throw new FileNotFoundException("缺少游戏文件，请重新解压完整目录。\r\n" + text);
			}
		}
	}

	internal static string SaveFolder()
	{
		string text = Environment.GetEnvironmentVariable("TOTK_PROFILE_DIR");
		if (string.IsNullOrEmpty(text))
		{
			text = "profile";
		}
		return Path.Combine(Path.GetFullPath(Path.Combine(Runtime, text)), "save");
	}

	private static string EncodeArguments(string[] arguments)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string text in arguments)
		{
			if (text.IndexOf('\0') >= 0)
			{
				throw new ArgumentException("启动参数中包含无效字符。");
			}
			if (stringBuilder.Length != 0)
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append('"');
			int num = 0;
			string text2 = text;
			foreach (char c in text2)
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
			stringBuilder.Append('"');
		}
		return stringBuilder.ToString();
	}

	private static void LanguageDefaults(ProcessStartInfo start)
	{
		bool flag = false;
		bool flag2 = false;
		string path = Path.Combine(Runtime, "karpur.ini");
		string[] array = (File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : new string[0]);
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.StartsWith("#") || text2.StartsWith(";"))
			{
				continue;
			}
			int num = text2.IndexOf('=');
			if (num >= 0)
			{
				string text3 = text2.Substring(0, num).Trim();
				if (text3.StartsWith("TOTK_", StringComparison.OrdinalIgnoreCase))
				{
					text3 = text3.Substring(5);
				}
				text2.Substring(num + 1).Trim().Trim('"');
				if (string.Equals(text3, "lang", StringComparison.OrdinalIgnoreCase))
				{
					flag = true;
				}
				if (string.Equals(text3, "menu_lang", StringComparison.OrdinalIgnoreCase))
				{
					flag2 = true;
				}
			}
		}
		if (!flag && !start.EnvironmentVariables.ContainsKey("TOTK_LANG"))
		{
			start.EnvironmentVariables["TOTK_LANG"] = "zh-Hans";
		}
		if (!flag2 && !start.EnvironmentVariables.ContainsKey("TOTK_MENU_LANG"))
		{
			start.EnvironmentVariables["TOTK_MENU_LANG"] = "zh-Hans";
		}
	}

	internal static Process StartGame()
	{
		Integrity.VerifySelf();
		CheckFiles();
		Integrity.VerifyRuntime();
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = Path.Combine(Runtime, "karpur.exe");
		processStartInfo.WorkingDirectory = Runtime;
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.Arguments = EncodeArguments(LaunchArguments);
		ProcessStartInfo processStartInfo2 = processStartInfo;
		LanguageDefaults(processStartInfo2);
		Directory.CreateDirectory(SaveFolder());
		Process process = Process.Start(processStartInfo2);
		if (process == null)
		{
			throw new IOException("游戏进程启动失败。");
		}
		return process;
	}
}
internal static class Native
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowTextW")]
	internal static extern bool SetWindowText(IntPtr window, string text);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
	internal static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
}
internal sealed class LauncherForm : Form
{
	private readonly Button start;

	private readonly Button settings;

	private readonly Label status;

	private readonly RadioButton scarlet;

	private readonly RadioButton violet;

	private readonly PictureBox banner;

	private Process game;

	internal LauncherForm()
	{
		Text = Program.Caption;
		ClientSize = new Size(760, 500);
		AutoScaleMode = AutoScaleMode.Dpi;
		Font = new Font("Microsoft YaHei UI", 10f);
		BackColor = Color.FromArgb(246, 244, 249);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		StartPosition = FormStartPosition.CenterScreen;
		using (Icon icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location))
		{
			Icon = (Icon)icon.Clone();
		}
		banner = new PictureBox
		{
			Location = new Point(0, 0),
			Size = new Size(760, 206),
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(32, 26, 49)
		};
		Controls.Add(banner);
		Controls.Add(new Label
		{
			Text = "宝可梦：朱 / 紫",
			Location = new Point(32, 225),
			Size = new Size(470, 40),
			Font = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold),
			ForeColor = Color.FromArgb(42, 30, 61)
		});
		Controls.Add(new Label
		{
			Text = "KarPur 0.3.8  ·  游戏 4.0.0  ·  含零之秘宝 DLC",
			Location = new Point(35, 276),
			AutoSize = true,
			ForeColor = Color.FromArgb(90, 78, 107)
		});
		scarlet = new RadioButton
		{
			Text = "宝可梦：朱",
			Location = new Point(35, 316),
			Size = new Size(180, 28)
		};
		violet = new RadioButton
		{
			Text = "宝可梦：紫",
			Location = new Point(226, 316),
			Size = new Size(180, 28)
		};
		scarlet.CheckedChanged += EditionChanged;
		violet.CheckedChanged += EditionChanged;
		Controls.Add(scarlet);
		Controls.Add(violet);
		start = MakeButton("启动游戏", 35, 373, 202, primary: true);
		start.Click += StartClicked;
		settings = MakeButton("启动设置", 249, 373, 144, primary: false);
		settings.Click += (object param0, EventArgs param1) =>
		{
			try
			{
				using SettingsForm settingsForm = new SettingsForm();
				settingsForm.ShowDialog(this);
			}
			catch (Exception error)
			{
				ShowError(error);
			}
		};
		Button button = MakeButton("存档目录", 405, 373, 144, primary: false);
		button.Click += (object param0, EventArgs param1) =>
		{
			try
			{
				string text = Program.SaveFolder();
				Directory.CreateDirectory(text);
				Process.Start(new ProcessStartInfo(text)
				{
					UseShellExecute = true
				});
			}
			catch (Exception error)
			{
				ShowError(error);
			}
		};
		Button button2 = MakeButton("使用说明", 561, 373, 164, primary: false);
		button2.Click += (object param0, EventArgs param1) =>
		{
			try
			{
				string text = Path.Combine(Program.Root, "游戏启动说明.txt");
				if (!File.Exists(text))
				{
					throw new FileNotFoundException("找不到游戏启动说明.txt。");
				}
				Process.Start(new ProcessStartInfo(text)
				{
					UseShellExecute = true
				});
			}
			catch (Exception error)
			{
				ShowError(error);
			}
		};
		status = new Label
		{
			Text = "默认简体中文，游戏内按 F1 打开中文设置。",
			Location = new Point(35, 445),
			Size = new Size(690, 29),
			ForeColor = Color.FromArgb(90, 78, 107)
		};
		Controls.Add(status);
		if (Program.Edition == "scarlet")
		{
			scarlet.Checked = true;
		}
		else
		{
			violet.Checked = true;
		}
		AcceptButton = start;
	}

	private Button MakeButton(string text, int x, int y, int width, bool primary)
	{
		Button button = new Button();
		button.Text = text;
		button.Location = new Point(x, y);
		button.Size = new Size(width, 48);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = (primary ? Color.FromArgb(96, 55, 150) : Color.White);
		button.ForeColor = (primary ? Color.White : Color.FromArgb(55, 39, 73));
		button.Cursor = Cursors.Hand;
		button.Font = new Font("Microsoft YaHei UI", primary ? 12f : 10f, primary ? FontStyle.Bold : FontStyle.Regular);
		Button button2 = button;
		button2.FlatAppearance.BorderColor = Color.FromArgb(209, 200, 222);
		button2.FlatAppearance.BorderSize = ((!primary) ? 1 : 0);
		Controls.Add(button2);
		return button2;
	}

	private void EditionChanged(object sender, EventArgs args)
	{
		if (!((RadioButton)sender).Checked)
		{
			return;
		}
		Program.Edition = (scarlet.Checked ? "scarlet" : "violet");
		string name = "banner-" + Program.Edition + ".jpg";
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
		using Image original = Image.FromStream(stream);
		Image image = banner.Image;
		banner.Image = new Bitmap(original);
		image?.Dispose();
	}

	internal void StartForQa()
	{
		StartClicked(this, EventArgs.Empty);
	}

	private void StartClicked(object sender, EventArgs args)
	{
		try
		{
			if (game != null && !game.HasExited)
			{
				return;
			}
			status.Text = "正在校验并启动游戏…";
			status.Refresh();
			game = Program.StartGame();
			Button button = start;
			Button button2 = settings;
			RadioButton radioButton = scarlet;
			bool flag = (violet.Enabled = false);
			bool flag3 = (radioButton.Enabled = flag);
			bool enabled = (button2.Enabled = flag3);
			button.Enabled = enabled;
			status.Text = "游戏运行中，首次进入时请等待资源加载。";
			game.EnableRaisingEvents = true;
			game.Exited += (object param0, EventArgs param1) =>
			{
				if (IsDisposed || !IsHandleCreated)
				{
					return;
				}
				try
				{
					BeginInvoke(new Action(GameExited));
				}
				catch (InvalidOperationException)
				{
				}
			};
			if (game.HasExited)
			{
				GameExited();
			}
		}
		catch (Exception error)
		{
			status.Text = "启动失败";
			ShowError(error);
		}
	}

	private void GameExited()
	{
		if (game != null)
		{
			int exitCode = game.ExitCode;
			game.Dispose();
			game = null;
			Button button = start;
			Button button2 = settings;
			RadioButton radioButton = scarlet;
			bool flag = (violet.Enabled = true);
			bool flag3 = (radioButton.Enabled = flag);
			bool enabled = (button2.Enabled = flag3);
			button.Enabled = enabled;
			if (Program.QaMode)
			{
				Program.QaExitCode = exitCode;
				Close();
			}
			else
			{
				status.Text = ((exitCode == 0) ? "游戏已退出" : "游戏已退出，运行日志位于当前版本的 logs 文件夹。");
			}
		}
	}

	private void ShowError(Exception error)
	{
		MessageBox.Show(this, error.Message, "启动提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == 12 && !string.IsNullOrEmpty(Program.Caption) && Marshal.PtrToStringUni(message.LParam) != Program.Caption)
		{
			IntPtr intPtr = Marshal.StringToHGlobalUni(Program.Caption);
			try
			{
				message.LParam = intPtr;
				base.WndProc(ref message);
				return;
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
		base.WndProc(ref message);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (game != null)
			{
				game.Dispose();
			}
			if (banner.Image != null)
			{
				banner.Image.Dispose();
			}
		}
		base.Dispose(disposing);
	}
}
internal sealed class SettingsForm : Form
{
	private sealed class Option
	{
		internal readonly string Value;

		private readonly string label;

		internal Option(string value, string label)
		{
			Value = value;
			this.label = label;
		}

		public override string ToString()
		{
			return label;
		}
	}

	private readonly Dictionary<string, ComboBox> fields = new Dictionary<string, ComboBox>();

	private readonly string path = Path.Combine(Program.Runtime, "karpur.ini");

	private readonly List<string> lines;

	internal SettingsForm()
	{
		Text = "启动设置";
		ClientSize = new Size(540, 558);
		Font = new Font("Microsoft YaHei UI", 10f);
		BackColor = Color.FromArgb(246, 244, 249);
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = (MinimizeBox = false);
		lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : new string[0]);
		AddOption("fullscreen", "显示模式", new string[2] { "0|窗口", "1|全屏" }, "0");
		AddOption("window_size", "窗口分辨率", new string[5] { "1280x720|1280 × 720", "1600x900|1600 × 900", "1920x1080|1920 × 1080", "2560x1440|2560 × 1440", "3840x2160|3840 × 2160" }, "1280x720");
		AddOption("fps_limit", "帧率上限", new string[8] { "30|30 帧", "40|40 帧", "45|45 帧", "60|60 帧", "90|90 帧（实验）", "120|120 帧（实验）", "144|144 帧（实验）", "display|显示器刷新率（实验）" }, "60");
		AddOption("operation_mode", "主机模式", new string[2] { "docked|底座模式（1080p）", "handheld|掌机模式（720p）" }, "docked");
		AddOption("submit_defer", "兼容模式", new string[2] { "0|开启", "1|关闭" }, "0");
		AddOption("perf_overlay", "性能显示", new string[3] { "off|关闭", "fps|仅帧率", "full|完整信息" }, "off");
		AddOption("shadow_res", "阴影质量", new string[5] { "0|原始", "2048|2 倍", "3072|3 倍", "4096|4 倍", "off|关闭" }, "0");
		AddOption("population", "野生宝可梦数量", new string[4] { "switch|原版（15）", "switch2|主机增强（40）", "enhanced|增强（80）", "extreme|极高（120）" }, "switch");
		AddOption("lang", "游戏默认语言", new string[9] { "zh-Hans|简体中文", "zh-Hant|繁體中文", "en-US|English", "ja|日本語", "ko|???", "fr|Fran?ais", "de|Deutsch", "it|Italiano", "es|Espa?ol" }, "zh-Hans");
		Button button = new Button
		{
			Text = "保存设置",
			Location = new Point(344, 499),
			Size = new Size(164, 38)
		};
		button.Click += (object param0, EventArgs param1) =>
		{
			try
			{
				Save();
				DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "保存提示");
			}
		};
		Controls.Add(button);
		Controls.Add(new Label
		{
			Text = "设置分别应用于朱、紫。更多选项可在游戏内按 F1 调整。",
			Location = new Point(30, 457),
			Size = new Size(480, 30)
		});
		AcceptButton = button;
	}

	private string Read(string key, string fallback)
	{
		string result = fallback;
		foreach (string line in lines)
		{
			int num = line.IndexOf('=');
			if (num > 0 && line.Substring(0, num).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				result = line.Substring(num + 1).Trim();
			}
		}
		return result;
	}

	private void AddOption(string key, string label, string[] options, string fallback)
	{
		int num = 24 + fields.Count * 46;
		Controls.Add(new Label
		{
			Text = label,
			Location = new Point(30, num + 5),
			Size = new Size(154, 30)
		});
		ComboBox comboBox = new ComboBox();
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Location = new Point(190, num);
		comboBox.Size = new Size(318, 30);
		ComboBox comboBox2 = comboBox;
		string text = Read(key, fallback);
		foreach (string text2 in options)
		{
			string[] array = text2.Split('|');
			comboBox2.Items.Add(new Option(array[0], array[1]));
			if (array[0] == text)
			{
				comboBox2.SelectedIndex = comboBox2.Items.Count - 1;
			}
		}
		if (comboBox2.SelectedIndex < 0)
		{
			comboBox2.Items.Add(new Option(text, "自定义（" + text + "）"));
			comboBox2.SelectedIndex = comboBox2.Items.Count - 1;
		}
		fields.Add(key, comboBox2);
		Controls.Add(comboBox2);
	}

	private void Save()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, ComboBox> field in fields)
		{
			dictionary.Add(field.Key, ((Option)field.Value.SelectedItem).Value);
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < lines.Count; i++)
		{
			int num = lines[i].IndexOf('=');
			if (num > 0)
			{
				string text = lines[i].Substring(0, num).Trim();
				if (dictionary.ContainsKey(text))
				{
					lines[i] = text + " = " + dictionary[text];
					hashSet.Add(text);
				}
			}
		}
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			if (!hashSet.Contains(item.Key))
			{
				lines.Add(item.Key + " = " + item.Value);
			}
		}
		File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}
}
internal static class BrandData
{
	internal static readonly ushort[] TitleData = new ushort[26]
	{
		13006, 14939, 691, 38252, 3558, 27416, 27574, 27610, 4464, 27804,
		27952, 27998, 28040, 28183, 28406, 28460, 28475, 28544, 28770, 28871,
		28842, 28941, 29063, 29257, 29220, 29313
	};

	internal static readonly byte[] Modulus = new byte[256]
	{
		174, 19, 91, 221, 170, 140, 32, 93, 163, 103,
		246, 54, 80, 230, 119, 32, 149, 94, 159, 2,
		105, 120, 184, 162, 32, 12, 195, 66, 195, 119,
		86, 231, 40, 239, 106, 76, 58, 67, 255, 84,
		24, 95, 161, 201, 154, 28, 19, 8, 184, 210,
		6, 48, 165, 20, 173, 80, 191, 2, 120, 151,
		205, 85, 203, 226, 33, 146, 234, 214, 242, 27,
		47, 65, 227, 15, 249, 146, 90, 96, 111, 79,
		147, 200, 43, 89, 123, 131, 24, 83, 70, 14,
		78, 254, 186, 148, 237, 152, 230, 6, 222, 152,
		91, 220, 227, 73, 85, 235, 61, 111, 128, 191,
		21, 10, 24, 231, 250, 124, 255, 192, 231, 20,
		47, 248, 226, 13, 18, 133, 242, 216, 75, 70,
		189, 126, 165, 156, 37, 18, 114, 62, 105, 95,
		34, 50, 73, 104, 57, 68, 87, 130, 188, 81,
		124, 91, 221, 247, 79, 94, 67, 235, 39, 195,
		82, 183, 46, 216, 1, 50, 235, 237, 35, 192,
		202, 175, 205, 250, 217, 56, 57, 222, 200, 184,
		174, 238, 146, 43, 29, 47, 202, 109, 36, 229,
		96, 227, 218, 85, 228, 167, 154, 133, 39, 153,
		57, 120, 61, 50, 109, 149, 153, 23, 107, 80,
		59, 38, 122, 252, 163, 112, 63, 104, 161, 167,
		241, 112, 170, 21, 105, 215, 244, 40, 151, 196,
		243, 168, 225, 111, 149, 55, 44, 97, 87, 28,
		94, 247, 156, 70, 176, 135, 251, 243, 146, 125,
		195, 93, 74, 128, 78, 209
	};
}
