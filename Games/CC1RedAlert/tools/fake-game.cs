// 托管契约回归用的假游戏：先把命令行与工作目录dump到同目录 args.txt，
// 再睡 8 秒后以退出码 7 结束（不是真游戏，不占屏、不渲染）。
// host-sim.ps1 把它编译成临时目录里的 Engine\RedAlert.exe。
class FakeGame
{
    static int Main()
    {
        string dir = System.AppDomain.CurrentDomain.BaseDirectory;
        string dump = "CMD=" + System.Environment.CommandLine + "\n" +
                      "CWD=" + System.Environment.CurrentDirectory + "\n";
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "args.txt"), dump,
            new System.Text.UTF8Encoding(false));
        System.Threading.Thread.Sleep(8000);
        return 7;
    }
}