// 假的 init.exe：只往自己目录追加一行时间，证明启动器默认执行了它；立即退出 0。
// host-sim.ps1 把它编译成临时目录根下的 init.exe。
class FakeInit
{
    static int Main()
    {
        string dir = System.AppDomain.CurrentDomain.BaseDirectory;
        System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "init-ran.txt"),
            System.DateTime.Now.ToString("HH:mm:ss.fff") + "\r\n",
            new System.Text.UTF8Encoding(false));
        return 0;
    }
}
