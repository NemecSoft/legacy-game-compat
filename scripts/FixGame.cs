using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class FixGame
{
    static readonly string[] DllNames = { "d3d8.dll", "d3d9.dll", "d3d10core.dll", "d3d11.dll", "dxgi.dll", "ddraw.dll", "d3dimm.dll" };
    const string BakDir = "_兼容备份";

    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.WriteLine("[错误] " + ex.Message); }
    }

    static void Run(string[] args)
    {
        string target = null;
        string mode = "auto";
        bool undo = false, dry = false;
        foreach (var a in args)
        {
            string s = a.Trim().Trim('"');
            if (s.StartsWith("/") || s.StartsWith("-"))
            {
                string o = s.Substring(1).ToLower();
                if (o == "undo") undo = true;
                else if (o == "dry") dry = true;
                else if (o == "dgv") mode = "dgv";
                else if (o == "dxvk") mode = "dxvk";
            }
            else target = s;
        }

        if (string.IsNullOrEmpty(target))
        {
            Console.WriteLine("用法: FixGame.exe <game_dir_or_exe> [/dry] [/dgv] [/dxvk] [/undo]");
            return;
        }
        string dir = Directory.Exists(target) ? target : Path.GetDirectoryName(Path.GetFullPath(target));

        Console.WriteLine("游戏目录: " + dir);
        if (!Directory.Exists(dir)) { Console.WriteLine("[错误] 目录不存在"); return; }

        if (undo) { DoUndo(dir, dry); return; }

        string exe = PickExe(dir);
        if (exe == null) { Console.WriteLine("[错误] 目录里找不到 exe"); return; }

        bool is64 = Is64(exe);
        var api = DetectApi(exe);
        Console.WriteLine("主程序  : " + Path.GetFileName(exe) + (is64 ? "  (64 位)" : "  (32 位)"));
        Console.WriteLine("图形接口: " + string.Join(", ", api.ToArray()));
        if (api.Count == 0) api.Add("d3d9.dll");

        string root = AppDomain.CurrentDomain.BaseDirectory;

        var toCopy = new List<string>();   // 源文件绝对路径
        if (mode == "dgv" || api.Contains("ddraw.dll"))
        {
            string baseDir = Resolve(root, "dgvoodoo", is64);
            if (Directory.Exists(baseDir))
            {
                foreach (var f in Directory.GetFiles(baseDir)) toCopy.Add(f);
            }
            if (api.Contains("ddraw.dll"))
                Console.WriteLine("说明: DirectDraw 游戏走 dgVoodoo2(DDraw)；若花屏可改用 cnc-ddraw");
        }
        else
        {
            string baseDir = Resolve(root, "dxvk", is64);
            if (Directory.Exists(baseDir))
            {
                string[] want;
                if (api.Contains("d3d8.dll")) want = new[] { "d3d8.dll", "d3d9.dll" };
                else if (api.Contains("d3d11.dll")) want = new[] { "d3d11.dll", "dxgi.dll" };
                else want = new[] { "d3d9.dll" };
                foreach (var w in want)
                {
                    string p = Path.Combine(baseDir, w);
                    if (File.Exists(p)) toCopy.Add(p);
                }
            }
        }

        if (toCopy.Count == 0) { Console.WriteLine("[错误] 兼容包里找不到对应的 dll"); return; }

        string bak = Path.Combine(dir, BakDir);
        if (!dry && !Directory.Exists(bak)) Directory.CreateDirectory(bak);

        foreach (var src in toCopy)
        {
            string name = Path.GetFileName(src);
            string dst = Path.Combine(dir, name);
            if (File.Exists(dst))
            {
                string bakFile = Path.Combine(bak, name);
                if (!File.Exists(bakFile))
                {
                    Console.WriteLine((dry ? "[试运行] 备份 " : "备份 ") + name + " -> " + BakDir);
                    if (!dry) File.Copy(dst, bakFile, true);
                }
            }
            Console.WriteLine((dry ? "[试运行] 复制 " : "复制 ") + name);
            if (!dry) File.Copy(src, dst, true);
        }

        if (mode != "dgv" && !api.Contains("ddraw.dll"))
        {
            string conf = Path.Combine(dir, "dxvk.conf");
            if (!dry && !File.Exists(conf))
            {
                File.WriteAllText(conf,
                    "d3d9.presentInterval = 1\r\nd3d9.maxFrameLatency = 1\r\nd3d9.numBackBuffers = 3\r\n",
                    Encoding.ASCII);
                Console.WriteLine("写入 dxvk.conf");
            }
        }

        Console.WriteLine("");
        Console.WriteLine(dry ? "试运行结束，未改动任何文件。" : "完成。直接运行游戏即可。");
        Console.WriteLine("回滚: 修复工具.exe 本目录 /undo");
        Console.WriteLine("若仍然黑屏/白屏: 换另一个层 -> 加参数 /dgv 或 /dxvk 再跑一次");
    }

    static string Resolve(string root, string name, bool is64)
    {
        string sub = is64 ? "x64" : "x86";
        string a = Path.Combine(root, "tools", name, sub);
        if (Directory.Exists(a)) return a;
        return Path.Combine(root, name, sub);
    }

    static void DoUndo(string dir, bool dry)
    {
        string bak = Path.Combine(dir, BakDir);
        if (!Directory.Exists(bak)) { Console.WriteLine("没有找到备份目录，无需回滚"); return; }
        foreach (var f in Directory.GetFiles(bak))
        {
            string dst = Path.Combine(dir, Path.GetFileName(f));
            Console.WriteLine((dry ? "[试运行] 还原 " : "还原 ") + Path.GetFileName(f));
            if (!dry) File.Copy(f, dst, true);
        }
        foreach (var n in DllNames)
        {
            string p = Path.Combine(dir, n);
            string b = Path.Combine(bak, n);
            if (File.Exists(p) && !File.Exists(b))
            {
                Console.WriteLine((dry ? "[试运行] 删除 " : "删除 ") + n);
                if (!dry) File.Delete(p);
            }
        }
        Console.WriteLine(dry ? "试运行结束" : "已回滚到原状");
    }

    static string PickExe(string dir)
    {
        var files = new List<string>(Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly));
        string best = null; long bestSize = -1;
        foreach (var f in files)
        {
            string n = Path.GetFileName(f).ToLower();
            if (n == "unins000.exe" || n == "dxsetup.exe" || n == "repair.exe") continue;
            var api = DetectApi(f);
            long sz = new FileInfo(f).Length;
            if (api.Count > 0) return f;
            if (sz > bestSize) { bestSize = sz; best = f; }
        }
        return best;
    }

    static bool Is64(string exe)
    {
        using (var fs = File.OpenRead(exe))
        {
            var buf = new byte[0x400];
            fs.Read(buf, 0, buf.Length);
            int pe = BitConverter.ToInt32(buf, 0x3C);
            if (pe <= 0 || pe + 6 >= buf.Length) return false;
            ushort machine = BitConverter.ToUInt16(buf, pe + 4);
            return machine == 0x8664;
        }
    }

    static List<string> DetectApi(string exe)
    {
        var found = new List<string>();
        byte[] data = File.ReadAllBytes(exe);
        string text = Encoding.ASCII.GetString(data).ToLowerInvariant();
        foreach (var n in new[] { "ddraw.dll", "d3d8.dll", "d3d9.dll", "d3d11.dll" })
            if (text.Contains(n)) found.Add(n);
        return found;
    }
}
