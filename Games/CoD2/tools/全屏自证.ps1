param([string]$GameDir = 'D:\YunGame\CoD\CoD2', [switch]$AllowGameLaunch)

# 全屏默认值一次性自证（会真启动游戏一次，走玩家真实路径 game_starter.exe /go）：
#   交付初始档是开发机测试留下的「窗口 1280x720」→ 启动器首次规范必须改成全屏 + 本机桌面分辨率 →
#   游戏窗口必须铺满桌面（左上角 0,0、尺寸 = 桌面）、全程不许出现对话框类 #32770 →
#   WM_CLOSE 正常退出后：config.cfg 保持全屏、display.state 记账、退出码 0、不留标记。
# 没有 -AllowGameLaunch 直接 exit 90（规则 24 第 14 类）。游戏画面截图按坑 91 口径走仓库统一通道
# scripts\capture-window.ps1：前台化 + 临时置顶 + 抓游戏窗口自身矩形（PrintWindow 对 D3D9/Vulkan 交换链是黑的）。

if (-not $AllowGameLaunch) {
  Write-Output '拒绝：本脚本会真启动游戏一次。确认要跑请加 -AllowGameLaunch（用户明令禁止反复启动游戏）'
  exit 90
}
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices; using System.Collections.Generic;
public struct WR3 { public int L, T, R, B; }
public class FS {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out WR3 r);
  [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static List<uint> Want = new List<uint>();
  public static IntPtr Game = IntPtr.Zero; public static bool Dialog;
  public static string Title = "";
  public static bool Cb(IntPtr h, IntPtr l) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    if (!Want.Contains(pid) || !IsWindowVisible(h)) return true;
    StringBuilder c = new StringBuilder(64); GetClassNameW(h, c, 64);
    if (c.ToString() == "#32770") { Dialog = true; return true; }
    if (c.ToString() == "CoD2") {
      WR3 r; GetWindowRect(h, out r);
      if (r.R - r.L > 1000 && Game == IntPtr.Zero) {
        Game = h;
        StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, t.Capacity); Title = t.ToString();
      }
    }
    return true;
  }
  public static void Scan() { Game = IntPtr.Zero; Dialog = false; EnumWindows(new EnumProc(Cb), IntPtr.Zero); }
  public static void TopMost(IntPtr h, bool on) {
    IntPtr after = on ? new IntPtr(-1) : new IntPtr(-2);
    SetWindowPos(h, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
  }
}
'@
$fail = 0
$lines = New-Object System.Collections.Generic.List[string]
function Check($ok, [string]$name, [string]$detail) {
  if ($ok) { $lines.Add('PASS ' + $name) } else { $script:fail++; $lines.Add('FAIL ' + $name + '  ' + $detail) }
}
$ev = Join-Path $PSScriptRoot '..\evidence'
$cfg = Join-Path $GameDir 'main\players\3dm\config.cfg'
$raw = [Text.Encoding]::GetEncoding(28591)
function CfgVal([string]$name) {
  $m = [regex]::Match($raw.GetString([IO.File]::ReadAllBytes($cfg)), "(?m)^seta\s+$name\s+\`"([^`"]+)\`"")
  return $m.Groups[1].Value
}
$fsBefore = CfgVal 'r_fullscreen'; $modeBefore = CfgVal 'r_mode'
$lines.Add('INFO 启动前初始档：r_fullscreen=' + $fsBefore + ' r_mode=' + $modeBefore)

# 走玩家真实路径：启动器 /go（内部先做画面默认值规范，再拉起游戏并陪跑）
$starter = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -ArgumentList '/go' `
  -WorkingDirectory $GameDir -PassThru
$gamePid = 0; $dialogSeen = $false; $title = ''
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Milliseconds 500
  $gp = @(Get-Process -Name 'CoD2SP_s' -ErrorAction SilentlyContinue)
  if ($gp.Count -eq 0) { if ($starter.HasExited) { break }; continue }
  $gamePid = [uint32]$gp[0].Id
  [FS]::Want = [System.Collections.Generic.List[uint32]]::new()
  [FS]::Want.Add($gamePid)
  [FS]::Scan()
  if ([FS]::Dialog) { $dialogSeen = $true }
  if ([FS]::Game -ne [IntPtr]::Zero) { $title = [FS]::Title; break }
}
Check ($gamePid -ne 0) ('启动器把游戏拉起来了（pid ' + $gamePid + '）') '60s 没等到 CoD2SP_s 进程'
Check (-not $dialogSeen) '全程没有对话框（#32770 没出现）' '又弹框了：字节补丁或标记逻辑被谁动了'
Check ([FS]::Game -ne [IntPtr]::Zero) ('游戏主窗口出现（标题：' + $title + '）') '60s 没有宽>1000 的 CoD2 类窗口'

$wa = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ([FS]::Game -ne [IntPtr]::Zero) {
  $r = New-Object WR3
  [FS]::GetWindowRect([FS]::Game, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $h = $r.B - $r.T
  Check ($w -eq $wa.Width -and $h -eq $wa.Height -and $r.L -eq $wa.X -and $r.T -eq $wa.Y) `
    ("窗口铺满桌面 $($w)x$($h) 于 $($r.L),$($r.T)（桌面 $($wa.Width)x$($wa.Height)）") `
    "实际 $w x $h @ $($r.L),$($r.T) —— 不是全屏就是画面默认值没生效"
  Start-Sleep -Seconds 12          # 过 splash，让开场视频/菜单画出来
  # 截图走仓库统一通道 scripts\capture-window.ps1（坑 91 口径：前台化 + 临时置顶 + 游戏窗口自身矩形；
  # PrintWindow 对 D3D9/Vulkan 交换链是黑的）。-KeepOpen 让它别杀进程，关窗仍由本脚本发 WM_CLOSE。
  $shot = Join-Path $ev 'shot-fullscreen-default.png'
  $capRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
  & (Join-Path $capRepo 'scripts\capture-window.ps1') -Process 'CoD2SP_s' -Out $shot -KeepOpen |
    ForEach-Object { $lines.Add('INFO 截图：' + $_) }
  $lines.Add('INFO 全屏实拍：' + $shot + '（capture-window.ps1，游戏窗口自身矩形，坑 91 口径）')
  Start-Sleep -Seconds 8            # 让菜单画完一帧，关窗请求落在正常状态上
  [FS]::Scan()                      # 重新扫：窗口可能重建过，旧句柄发 WM_CLOSE 会石沉大海（2026-11-09 踩过）
  if ([FS]::Game -ne [IntPtr]::Zero) { [FS]::PostMessageW([FS]::Game, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
}
$gameExited = $starter.WaitForExit(150000)   # 干净退出应在 ~30s 内；150s 上限是给看门狗兜底留的路径
Check $gameExited '关窗后游戏和启动器都退了（干净退出或看门狗兜底）' '150s 还没退：看门狗没兜住'
$code = if ($gameExited) { $starter.ExitCode } else { -1 }
Check ($code -eq 0) ('启动器原样回传退出码 0（实际 ' + $code + '）') '退出码不是 0：走了看门狗兜底或引擎真崩，看运行日志'
$log = [IO.File]::ReadAllLines((Join-Path $GameDir '运行日志.txt'), [Text.Encoding]::UTF8) | Select-Object -Last 40
Check (($log -join "`n") -notmatch '看门狗') '本次是干净退出（没借助看门狗杀进程）' '日志出现看门狗：播片关窗挂起又复现，好在兜住了'
Check ((CfgVal 'r_fullscreen') -eq '1') ('退出后 config 仍是全屏（' + (CfgVal 'r_fullscreen') + '）') '引擎退出把设置写回去了却不是全屏'
Check ((CfgVal 'r_mode') -eq ($wa.Width.ToString() + 'x' + $wa.Height.ToString())) `
  ('r_mode = 本机桌面 ' + (CfgVal 'r_mode')) 'r_mode 没规范成桌面分辨率'
$st = Join-Path $GameDir 'display.state'
Check ((Test-Path $st) -and ((Get-Content $st -Raw) -match 'config\.cfg=1\|')) '账本 display.state 记了这次归属' '账本缺失：下次分不清玩家改动'
Check (-not (Test-Path (Join-Path $GameDir '__CoD2SP_s'))) '正常退出没留安全模式标记' '标记还在'
$log = [IO.File]::ReadAllLines((Join-Path $GameDir '运行日志.txt'), [Text.Encoding]::UTF8) | Select-Object -Last 40
Check ((CfgVal 'com_introPlayed') -eq '1') '开场视频标记保持已播（关窗挂起诱因直接消掉）' 'intro 又变回要播了'
Check (($log -join "`n") -match '首次规范|已是全屏') '日志记了画面默认值的处理（首次规范或已是全屏只记账）' '日志里没有画面默认值行'
$outFile = Join-Path $ev 'fullscreen-verify.txt'
[IO.File]::WriteAllLines($outFile, $lines, (New-Object Text.UTF8Encoding $false))
$lines | ForEach-Object { Write-Output $_ }
Write-Output ('结论文件：' + $outFile)
exit $fail
