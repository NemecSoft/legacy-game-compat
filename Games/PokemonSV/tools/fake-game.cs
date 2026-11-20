// 托管契约回归用的假游戏：睡 8 秒后以退出码 7 结束（不是真游戏，不占屏）。
// host-sim.ps1 把它编译成临时目录里的 runtime\scarlet\karpur.exe 与 runtime\violet\karpur.exe。
class FakeGame
{
    static int Main()
    {
        System.Threading.Thread.Sleep(8000);
        return 7;
    }
}