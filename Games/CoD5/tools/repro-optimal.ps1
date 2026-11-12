# 试验：验证「设置最优设置?」弹框机制与封法（坑 111 教训：本脚本会启动游戏、自动点框、结束后优雅关闭游戏，绝不裸 Kill）
# 用法：
#   pwsh -NoProfile -File tools\repro-optimal.ps1 -Mode badsum    # 第 1 轮：把 sys_configSum 改成错值，预期弹框（确认机制）
#   pwsh -NoProfile -File tools\repro-optimal.ps1 -Mode wipe      # 第 2 轮：抹掉 sys_* 硬件指纹组 + 垫真实 AppData 档案，预期不弹（验证"第一次运行"封法）
# 副作用清单（全部自动恢复）：
#   1. 会改写游戏目录 config.cfg（脚本结尾从备份恢复）
#   2. 会往真实 AppData 垫档案（wipe 模式；垫是交付行为本身，不恢复）
#   3. 会启动 CoDWaW.exe 最多 30 秒，期间弹出的 #32770 模态框按按钮 ID 自动点"否"(IDNO=7)
#   4. 结束时对游戏主窗口发 WM_CLOSE 优雅退出；15 秒退不掉才 Kill（会注明）
param(
    [Parameter(Mandatory=$true)][ValidateSet('badsum','wipe')][string]$Mode
)
$ErrorActionPreference = 'Stop'
$gameDir = 'D:\YunGame\CoD\CoD5'
$exe = Join-Path $gameDir 'CoDWaW.exe'
$cfg = Join-Path $gameDir 'players\profiles\YunGame\config.cfg'
$activeReal = "$env:LOCALAPPDATA\Activision\CoDWaW\players\profiles\active.txt"
$profReal   = "$env:LOCALAPPDATA\Activision\CoDWaW\players\profiles"
Add-Type -AssemblyName System.Windows.Forms

# ---- Win32 ----
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class W {
    public delegate bool CB(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(CB cb, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, CB cb, IntPtr lp);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    public static List<IntPtr> tops;
    public static uint target;
    public static void Scan() {
        tops = new List<IntPtr>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == target && IsWindowVisible(h)) { StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256); if (c.ToString() == "#32770") tops.Add(h); }
            return true;
        }, IntPtr.Zero);
    }
    public static List<IntPtr> btns;
    public static void ScanBtns(IntPtr dlg) {
        btns = new List<IntPtr>();
        EnumChildWindows(dlg, (h, l) => { StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256); if (c.ToString() == "Button" && IsWindowVisible(h)) btns.Add(h); return true; }, IntPtr.Zero);
    }
}
"@

function Log($s) { Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $s) }

# ---- config 备份 ----
$bak = "$cfg.repro-bak"
Copy-Item $cfg $bak -Force
Log "config 已备份 → $bak"

try {
    if ($Mode -eq 'badsum') {
        # 第 1 轮：把硬件指纹改成错值，模拟"换了台电脑"
        $lines = [System.IO.File]::ReadAllLines($cfg)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^seta sys_configSum') { Log ("原值： " + $lines[$i]); $lines[$i] = 'seta sys_configSum "12345"' }
        }
        [System.IO.File]::WriteAllLines($cfg, $lines)
        Log "已把 sys_configSum 改成 12345（错值）"
    } else {
        # 第 2 轮：抹掉硬件指纹四件套 → 引擎当"第一次运行"
        $lines = [System.IO.File]::ReadAllLines($cfg) | Where-Object { $_ -notmatch '^seta sys_(configSum|configureGHz|gpu|sysMB)' }
        [System.IO.File]::WriteAllLines($cfg, $lines)
        Log "已抹掉 sys_configSum/sys_configureGHz/sys_gpu/sys_sysMB 四行（其余 config 原样保留）"
        # 同时垫真实 AppData 档案：引擎启动极早期在这里找档案（重定向生效前）
        $src = Join-Path $gameDir 'players\profiles\YunGame\config.cfg'
        $dst = Join-Path $profReal 'YunGame'
        New-Item $dst -ItemType Directory -Force | Out-Null
        Copy-Item $src (Join-Path $dst 'config.cfg') -Force
        [System.IO.File]::WriteAllText($activeReal, "YunGame")
        Log "真实 AppData 已垫：active.txt=YunGame + YunGame\config.cfg"
    }

    # ---- 起游戏（工作目录=游戏目录 + 重定向参数，与启动器一致）----
    Log "启动游戏…"
    $p = Start-Process -FilePath $exe -WorkingDirectory $gameDir -ArgumentList '+set fs_localAppData "D:\YunGame\CoD\CoD5" +set fs_BaseGame "D:\YunGame\CoD\CoD5"' -PassThru
    [W]::target = [uint32]$p.Id
    $seen = @{}
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and -not $p.HasExited) {
        Start-Sleep -Milliseconds 800
        [W]::Scan()
        foreach ($dlg in [W]::tops) {
            $t = New-Object System.Text.StringBuilder 256
            [void][W]::GetWindowText($dlg, $t, 256)
            $title = $t.ToString()
            if (-not $seen.ContainsKey($title)) {
                $seen[$title] = $true
                Log ("弹框： """ + $title + """")
                # 按 ID 找按钮：IDYES=6 IDNO=7 IDOK=1 IDCANCEL=2 —— 点"否"(7)，跨码页稳定
                [W]::ScanBtns($dlg)
                foreach ($b in [W]::btns) {
                    $id = [W]::GetWindowLong($b, -12) -band 0xFFFF
                    Log ("  按钮 ID=" + $id)
                    if ($id -eq 7) { [void][W]::SendMessage($b, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero); Log "  已自动点「否」(IDNO=7)" }
                }
            }
        }
    }
    if ($seen.Count -eq 0) { Log "结果： 30 秒内零弹框 ✔" } else { Log ("结果： 共弹 " + $seen.Count + " 种框（已逐个点掉）") }

    # ---- 优雅关闭：WM_CLOSE 主窗口，15 秒退不掉才 Kill ----
    if (-not $p.HasExited) {
        Log "发 WM_CLOSE 优雅关闭游戏…"
        $main = [IntPtr]::Zero
        [W]::EnumWindows({ param($h, $l)
            $pid2 = 0u; [void][W]::GetWindowThreadProcessId($h, [ref]$pid2)
            if ($pid2 -eq [W]::target -and [W]::IsWindowVisible($h)) { $script:main = $h }
            return $true
        }, [IntPtr]::Zero)
        if ($main -ne [IntPtr]::Zero) { [void][W]::PostMessage($main, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
        if (-not $p.WaitForExit(15000)) { $p.Kill(); Log "警告： 15 秒没退干净，已强杀（下次启动注意安全模式标记会被垫掉——本脚本结束时会顺带删标记）" }
        else { Log ("游戏已退出，退出码 " + $p.ExitCode) }
    } else { Log ("游戏提前退出，退出码 " + $p.ExitCode) }
}
finally {
    Copy-Item $bak $cfg -Force
    Log "config 已从备份恢复原样"
    # 不论哪轮，顺手把真实 AppData 的安全模式标记删掉（游戏没正常退出会留）
    $mark = "$env:LOCALAPPDATA\Activision\CoDWaW\__CoDWaW"
    if (Test-Path $mark) { Remove-Item $mark -Force; Log "已删安全模式标记 __CoDWaW" }
}
