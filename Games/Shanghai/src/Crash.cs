using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// 崩溃取证（AGENTS 规则 35）：只读系统已经生成的 WER 报告，不动注册表、不挂调试器。
// 报告里的 Sig[n].Name 按名字认，中英文系统都要认，不许按序号猜。
internal static class Crash
{
    static bool IsCode(string n)
    {
        return n == "异常代码" || n.IndexOf("Exception Code", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n == "ExceptionCode";
    }
    static bool IsOffset(string n)
    {
        return n == "异常偏移" || n.IndexOf("Exception Offset", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n == "ExceptionOffset";
    }
    static bool IsModule(string n)
    {
        // 「故障模块版本」不是模块名，必须排除（规则 35 点名过）
        if (n.IndexOf("版本", StringComparison.Ordinal) >= 0 ||
            n.IndexOf("Version", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return n == "故障模块名称" || n.IndexOf("Fault Module Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n == "FaultModuleName";
    }
    static bool IsApp(string n)
    {
        return n == "应用程序名" || n.IndexOf("Application Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n == "AppName";
    }

    public static string Parse(string path)
    {
        string code = "", offset = "", module = "", app = "";
        try
        {
            string[] lines = File.ReadAllLines(path);       // File 会自己认 BOM（UTF-16LE / UTF-8 都行）
            string pending = "";
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                if (k.EndsWith(".Name", StringComparison.OrdinalIgnoreCase)) { pending = v; continue; }
                if (!k.EndsWith(".Value", StringComparison.OrdinalIgnoreCase) || pending.Length == 0) continue;
                if (IsCode(pending)) code = v;
                else if (IsOffset(pending)) offset = v;
                else if (IsModule(pending)) module = v;
                else if (IsApp(pending)) app = v;
                pending = "";
            }
        }
        catch (Exception ex) { return "没读到报告：" + ex.Message; }

        if (code.Length == 0 && module.Length == 0) return "没认出异常码（报告里没有异常代码/故障模块名称）";
        string c = code.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? code : "0x" + code;
        string o = offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                   offset.StartsWith("+0x", StringComparison.OrdinalIgnoreCase) ? offset : "+0x" + offset;
        return "异常码 " + c + "，出错地址 " + o + "，故障模块 " +
               (module.Length > 0 ? module : "未知") +
               (app.Length > 0 ? "，程序 " + app : "");
    }

    // 游戏退出后自动抄一份到运行日志；报告本体存 崩溃转储\。用户什么都不用做。
    public static void Capture(string appDir, string exeName, DateTime started, Action<string> log)
    {
        try
        {
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dirs = new List<string> {
                Path.Combine(common, @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(common, @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(local,  @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(local,  @"Microsoft\Windows\WER\ReportArchive")
            };
            // 报告是系统后台写的，立刻读常常读不到：重试几次（不启动游戏、不动系统）
            string best = null; DateTime bestTime = started.AddSeconds(-5);
            for (int attempt = 0; attempt < 6 && best == null; attempt++)
            {
                foreach (string dir in dirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string d in Directory.GetDirectories(dir))
                    {
                        if (d.IndexOf(exeName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string wer = Path.Combine(d, "Report.wer");
                        if (!File.Exists(wer)) continue;
                        DateTime t = File.GetLastWriteTime(wer);
                        if (t >= bestTime) { bestTime = t; best = wer; }
                    }
                }
                if (best == null) System.Threading.Thread.Sleep(600);
            }
            if (best == null)
            {
                log("崩溃报告：没抓到（WER 里没有本游戏 " + exeName + " 的记录，或系统还没写完）");
                return;
            }
            string line = "崩溃捕获：" + Parse(best);
            log(line);
            string dstDir = Path.Combine(appDir, "崩溃转储");
            Directory.CreateDirectory(dstDir);
            File.Copy(best, Path.Combine(dstDir, Path.GetFileName(Path.GetDirectoryName(best)) + ".wer"), true);
        }
        catch (Exception ex) { log("崩溃报告取证失败：" + ex.Message); }
    }
}
