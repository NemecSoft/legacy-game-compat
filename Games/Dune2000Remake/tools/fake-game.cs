using System;
using System.IO;
using System.Text;
using System.Threading;

// host-sim 假游戏：把命令行 / 工作目录 dump 到 args.txt，睡 8 秒后退出码 7。
// 用来验证启动器的托管契约，不需要启动真游戏。
static class FakeGame
{
    static int Main(string[] args)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ARGS=").Append(string.Join(" ", args)).Append("\r\n");
            sb.Append("CWD=").Append(Directory.GetCurrentDirectory()).Append("\r\n");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "args.txt"), sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
        Thread.Sleep(8000);
        return 7;
    }
}