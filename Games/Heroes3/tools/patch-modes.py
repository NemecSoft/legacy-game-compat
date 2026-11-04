# -*- coding: utf-8 -*-
# 给 Heroes3 启动器加「双通道」模式表：cnc-ddraw（GDI / DirectX9）+ dgVoodoo2（全屏 / 窗口）
import io

p = r'D:\AI\legacy-game-compat\Games\Heroes3\src\Launcher.cs'
t = io.open(p, encoding='utf-8', newline='').read().replace('\r\n', '\n')

# ---------- 1) 模式表：名字里直接写清走的是哪条通道 ----------
old = '''    static readonly string[] ModeNames =
    {
        "1  稳定全屏（GDI，最兼容）",
        "2  保持 4:3（左右黑边）",
        "3  窗口模式（1280×720）",
        "4  原版 640×480（窗口）"
    };
    static readonly string[] ModePresets =
    {
        "ddraw.ini",
        "ddraw-43.ini",
        "ddraw-window.ini",
        "ddraw-640.ini"
    };
'''
new = '''    static readonly string[] ModeNames =
    {
        "1  兼容模式（cnc-ddraw GDI，全屏）",
        "2  兼容模式（cnc-ddraw GDI，保持 4:3）",
        "3  兼容模式（cnc-ddraw GDI，窗口 1280×720）",
        "4  原版画面（cnc-ddraw GDI，640×480 窗口）",
        "5  直连模式（cnc-ddraw DirectX9，全屏）",
        "6  兼容模式（dgVoodoo2，全屏）",
        "7  兼容模式（dgVoodoo2，窗口）"
    };
    static readonly string[] ModePresets =
    {
        "ddraw.ini",
        "ddraw-43.ini",
        "ddraw-window.ini",
        "ddraw-640.ini",
        "ddraw-d3d9.ini",
        "dgVoodoo-full.conf",
        "dgVoodoo-window.conf"
    };
    // 每个模式顶在 ddraw.dll 位置上的层：cnc = cnc-ddraw，voodoo = dgVoodoo2
    static readonly string[] ModeLayer =
    {
        "cnc", "cnc", "cnc", "cnc", "cnc", "voodoo", "voodoo"
    };
    static readonly string[] LayerDirs =
    {
        "cnc", "_cnc-ddraw",
        "voodoo", "_dgvoodoo2"
    };
'''
assert old in t
t = t.replace(old, new, 1)

# ---------- 2) 换层 + 铺配置的助手 ----------
anchor = '''    // ---------- 记住这台机器验证过能玩的画面方式 ----------'''
helpers = '''    // ---------- 换层：把所选模式要用的 ddraw.dll 顶上去，并铺好它的配置 ----------
    static string LayerDirOf(string tag)
    {
        for (int i = 0; i < LayerDirs.Length; i += 2)
            if (LayerDirs[i] == tag) return LayerDirs[i + 1];
        return "_cnc-ddraw";
    }

    // 返回 false 表示这个模式的层或配置不在，调用方要退回默认模式
    bool ApplyLayer(int which, out string fail)
    {
        fail = "";
        string tag = ModeLayer[which];
        string dir = LayerDirOf(tag);
        string dllSrc = Path.Combine(AppDir(), dir, "ddraw.dll");
        string confSrc = Path.Combine(AppDir(), "_presets", ModePresets[which]);
        if (!File.Exists(dllSrc)) { fail = dir + "\\\\ddraw.dll 缺失"; return false; }
        if (!File.Exists(confSrc)) { fail = "_presets\\\\" + ModePresets[which] + " 缺失"; return false; }
        try
        {
            File.Copy(dllSrc, Path.Combine(AppDir(), "ddraw.dll"), true);
            if (tag == "voodoo")
            {
                File.Copy(confSrc, Path.Combine(AppDir(), "dgVoodoo.conf"), true);
                // dgVoodoo2 和 cnc-ddraw 都靠 ddraw.ini / dgVoodoo.conf 读配置，
                // 用哪一层就只留哪一份，免得上一份的残留把这一层带歪
                try { if (File.Exists(Path.Combine(AppDir(), "ddraw.ini"))) File.Delete(Path.Combine(AppDir(), "ddraw.ini")); } catch { }
            }
            else
            {
                File.Copy(confSrc, Path.Combine(AppDir(), "ddraw.ini"), true);
                try { if (File.Exists(Path.Combine(AppDir(), "dgVoodoo.conf"))) File.Delete(Path.Combine(AppDir(), "dgVoodoo.conf")); } catch { }
            }
            Log("已挂载 " + (tag == "voodoo" ? "dgVoodoo2" : "cnc-ddraw") + " + " + ModePresets[which]);
            return true;
        }
        catch (Exception ex) { fail = ex.Message; return false; }
    }

    // 收尾一律回到最兼容的 cnc-ddraw GDI，不在这台机器上留两层叠着的现场
    void RestoreDefaultLayer()
    {
        try
        {
            string dll = Path.Combine(AppDir(), "_cnc-ddraw", "ddraw.dll");
            string conf = Path.Combine(AppDir(), "_presets", "ddraw.ini");
            if (File.Exists(dll)) File.Copy(dll, Path.Combine(AppDir(), "ddraw.dll"), true);
            if (File.Exists(conf)) File.Copy(conf, Path.Combine(AppDir(), "ddraw.ini"), true);
            string left = Path.Combine(AppDir(), "dgVoodoo.conf");
            if (File.Exists(left)) File.Delete(left);
        }
        catch (Exception ex) { Log("恢复默认层失败：" + ex.Message); }
    }

''' + anchor
assert anchor in t
t = t.replace(anchor, helpers, 1)

# ---------- 3) 启动流程改用 ApplyLayer ----------
old3 = '''        string presetPath = Path.Combine(AppDir(), "_presets", ModePresets[mode]);
        if (!File.Exists(presetPath))
        {
            Log("缺少显示预设：" + presetPath);
            status = "缺少显示文件，请先运行 兼容修复.bat";
            statusColor = Warn; Invalidate();
            return;
        }'''
new3 = '''        string presetPath = Path.Combine(AppDir(), "_presets", ModePresets[mode]);
        string layerSrc = Path.Combine(AppDir(), LayerDirOf(ModeLayer[mode]), "ddraw.dll");
        if (!File.Exists(presetPath) || !File.Exists(layerSrc))
        {
            Log("缺少显示预设或兼容层：" + presetPath + " / " + layerSrc);
            status = "缺少显示文件，请先运行 兼容修复.bat";
            statusColor = Warn; Invalidate();
            return;
        }'''
assert old3 in t
t = t.replace(old3, new3, 1)

old4 = '''                    string use = Path.Combine(AppDir(), "_presets", ModePresets[usedMode]);
                    if (!File.Exists(use))
                    {
                        Log("显示预设缺失：" + use + "，改用默认预设");
                        use = presetPath;
                    }
                    File.Copy(use, Path.Combine(AppDir(), "ddraw.ini"), true);'''
new4 = '''                    string failLayer;
                    if (!ApplyLayer(usedMode, out failLayer))
                    {
                        Log("该模式不可用（" + failLayer + "），退回兼容模式（cnc-ddraw GDI）");
                        usedMode = 0;
                        if (attempt > 1) { FinishWith(1); break; }
                        continue;
                    }'''
assert old4 in t
t = t.replace(old4, new4, 1)

# ---------- 4) finally 改成统一恢复 ----------
old5 = '''                try
                {
                    string def = Path.Combine(AppDir(), "_presets", "ddraw.ini");
                    if (File.Exists(def)) File.Copy(def, Path.Combine(AppDir(), "ddraw.ini"), true);
                }
                catch { }'''
new5 = '''                RestoreDefaultLayer();'''
assert old5 in t
t = t.replace(old5, new5, 1)

io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))
print('Launcher.cs 已改为双通道模式表')
