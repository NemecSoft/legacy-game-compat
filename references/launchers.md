# 启动器规范

交付一个老游戏时，**GUI `game_starter.exe` 和控制台 `game_starter.bat` 是平级的两个正式启动器**：
bat 用 `echo` 自己生成菜单，菜单项、顺序、默认值与 exe 完全一致，玩家选完由 bat 自己启动游戏
（**不是**转发 GUI 的薄包装），两者都必须满足平台托管契约。旧名 `go.exe` / `go.bat` 作废。
只交其中一个是允许的，但同时交付时必须同选项、同顺序、同默认值、同启动行为
（自检补层、恢复默认配置、阻塞等退出、回传退出码）。`/go` 参数在两边都表示「跳过界面/菜单，按默认项启动」。

**固定文案与栏目顺序**见技能 `.qoder\skills\legacy-launcher-gui\SKILL.md` 的「文案规范」：
左栏「关于这款游戏 / 玩不了或报错」，右栏「进游戏说明 → 操作说明 → 提示」，用词统一「管理员」；
秘技必须全文写在界面里，不许写“去某文件看”。

## 平台托管契约（GUI 替代 bat 的前提）

玩家的启动平台（Playnite 这类）用「启动脚本 + 进程树 + 退出码」监控游戏：脚本崩了、
游戏异常退出，平台要能抓到并报警。bat 天然满足四条，GUI 必须一条不落地复刻：

| # | 平台依赖 | GUI 做法 |
|---|---|---|
| 1 | 游戏在启动器的进程树里 | `Process.Start` + `UseShellExecute=false` 直接起游戏；不要用 `shell:`、`explorer.exe`、`cmd /c start` 换掉父进程 |
| 2 | 启动器活着 = 会话活着 | 游戏运行期间启动器不退出（后台线程 `WaitForExit`），玩家可 Alt+Tab 回来看操作说明 |
| 3 | 退出码能回传 | 游戏一退出启动器就退出并 `return 游戏退出码`；`0xC0000xxx` 这类崩溃码要能被平台认出，**吞退出码 = 崩溃查不到** |
| 4 | 关启动器不杀游戏、且游戏码不丢 | 关窗=**隐藏到后台陪跑**（托盘双击唤回），游戏退出后启动器才退出并回传游戏码；玩家强杀启动器时，游戏仍带着它的父 pid，平台按 pid 自己读游戏码 |

配套：`game_starter.exe /go` 作零交互启动命令；`launcher.ini` 支持 `GameArgs`（`-game cstrike` 这类）；
`GameExe` 相对游戏目录解析（`UseShellExecute=false` 不查 PATH，裸 `cmd.exe` 会“找不到”）；
自测开关 `game_starter.exe /selftest <程序> [参数]` 回传子进程退出码，回归用
`Games\<游戏>\tools\host-sim.ps1`（模拟主 GUI：`-Auto` 用假游戏不占屏，`-Auto -RealGame` 用真游戏）。
细节与代码见技能 `.qoder\skills\legacy-launcher-gui\SKILL.md`。

| | BAT 启动器 | EXE 启动器 |
|---|---|---|
| 文件 | `game_starter.bat` | `game_starter.exe` + `launcher.ini` |
| 适合 | 控制台玩家；不想编译；平台只认脚本时用 `/go`；同一套选项做双入口 | 默认交付；键帽操作表；被平台托管 |
| 依赖 | 系统 `cmd` | .NET Framework 4.x（Win10/11 自带） |
| 体积 | 1 个文本文件 | 约 10–30 KB（自绘版实测 27.5 KB，红线 60 KB） |
| 主题 | 红底白字 `color 4f` + 菜单 | 中国红渐变 `#800020→#4A0012` + 金色 `#FFD85E` |

## 共同硬要求

1. **原游戏标题**：窗口标题写成游戏名（中文名 + 英文名），不要写 "启动器"。
2. **窗口居中**：bat 用 nircmd；exe 用 `StartPosition = CenterScreen`。
3. **适当提示**：画面模式/后端、按键用法、黑屏怎么办、退出后自动关窗。
4. **启动前自检**：翻译层缺失时从 `_cnc-ddraw\` / `_dxvk\` 自动补上；文件缺失给明确提示，不要静默失败。
5. **启动前恢复默认配置**：默认 `game_starter.bat` / `game_starter.exe` 每次把 `_cnc-ddraw\ddraw.ini`（或 `_dxvk\dxvk.conf`）
   复制回生效位置，避免 `game_starter-soft` / `-640` 等备用模式残留。
6. **导入游戏设置**：如果游戏需要自己的 `.reg`（如 RoadRash 的 `road64.reg`），按系统位数导入；
   只写游戏自己的键，能回滚，不碰系统全局。
7. **退出即结束**：游戏退出后启动器窗口自动关（bat）或状态回到"已退出"（exe）。
8. **中国红，不要黑底**：bat `@echo off&color 4f`；exe 背景 `#C0121E`、标题/边框金色、正文白/米色。
9. **源文件隔离**：`Launcher.cs`、bat 源码放仓库 `Games\<游戏>\src`；游戏目录只放编译/转码后的成品，
   **不放 `.cs` / `.ps1`**。bat 源码 UTF-8 + **CRLF**，落地转 **GBK + CRLF**。
10. **反作弊网游不做。**

## BAT 启动器

### 形态一：控制台菜单启动器（正式方案）

用 `echo` 自己画菜单，菜单项与 `game_starter.exe` 一一对应（显示选项 + 打开运行日志 + 退出），
选完由 bat 自己启动游戏；`/go` 跳过菜单按默认项启动（给只认脚本的平台）：

```bat
@echo off&color 4f
setlocal EnableExtensions
set "tl=<原游戏名>"
title %tl%
mode con: lines=32 cols=92 >nul 2>&1
cd /d "%~dp0"
if not exist "<层.dll>" if exist "_dxvk\<层.dll>" copy /y "_dxvk\<层.dll>" "<层.dll>" >nul
if not exist "<层.dll>" (
  echo   缺少兼容层，请先双击 兼容修复.bat
  if /i not "%~1"=="/go" pause
  exit /b 1
)
set "sel=1"
if /i not "%~1"=="/go" (
  cls
  echo   1  启动游戏（默认）
  echo   2  启动游戏（显示帧数 HUD，排查用）
  echo   3  打开运行日志
  echo   4  退出
  set /p "sel=  请选择 [1-4]，直接回车 = 1: "
)
if "%sel%"=="3" start "" notepad "%~dp0运行日志.txt"
if "%sel%"=="3" exit /b 0
if "%sel%"=="4" exit /b 0
if "%sel%"=="2" ( <按选项写配置> ) else ( <恢复默认配置> )
"%~dp0<游戏.exe>" >nul 2>&1
set "rc=%ERRORLEVEL%"
<恢复默认配置>
exit /b %rc%
```

要点：

- **选项/顺序/默认值必须和 GUI 一致**；加模式两边一起改。完整示例见 `Games\JustCause\src\game_starter.bat`。
- `if ... echo a&echo b` 里的 `&` 不受 `if` 管，多行提示放进 `( )` 块；
  `%sel%` 的判断放到块外（块里是解析期展开）。
- 游戏那一行**直接写 exe**（阻塞），退出码用 `set "rc=%ERRORLEVEL%"` 接住再 `exit /b %rc%`；
  `>nul 2>&1` 顺便压掉 DXVK 控制台日志。**cmd 只认 `>nul`**。
- 正常流程不用 `pause`（菜单里也没有“按任意键启动”的闸门）；只有缺文件报错时，
  非 `/go` 才 `pause` 一下让人看清提示。
- **GBK + CRLF**（简体中文系统）。仓库里的模板是 UTF-8 + CRLF，落地时转 GBK + CRLF。
- `color 4f` 是中国红底 + 亮白字；不要 `1f` 蓝底/黑底。
- **控制台窗口居中靠 nircmd**：`if exist "%~dp0_tools\nircmd\nircmdc.exe" "...\nircmdc.exe" win center ititle "%tl%"`
  放在 `title` / `mode con` **之后**；工具缺失时不影响启动。nircmd 只用来居中，不用来藏窗口。
- 外挂型（汉化外挂这类拉起游戏就退出的加载器）：`start "" "外挂.exe"` + 进程名等待（模板
  `game_starter-loader.bat`，唯一允许的 PowerShell 用法）。
- **注册表导入必须幂等**：只在游戏设置键/关键值不存在（首次运行）时 `regedit/s`。
  无条件每次导入会覆盖玩家在游戏内改的键位、分辨率等，用户会以为设置没保存。

### 形态二：无控制台薄包装（可选）

平台只认脚本、又不想给玩家看控制台时，用薄包装把 exe 挂上去；选项和说明全在 GUI 里。
它**不是**默认交付形态，只在明确需要「零交互 + 无控制台」时用：

```bat
@echo off&color 4f
setlocal
set "tl=<原游戏名>"
title %tl%
cd /d "%~dp0"
start "" /wait "%~dp0game_starter.exe" /go /hideparent
exit /b %ERRORLEVEL%
```

- **`start "" /wait` 不能省**：cmd 对 GUI 程序不等待，直接写 exe 会让 bat 秒退、
  退出码变 0、进程树断开（三条平台依赖一次全丢）。
- **黑框由 `game_starter.exe /hideparent` 处理**：game_starter.exe 找到自己的父进程，只有当它是
  `cmd`/`powershell`/`pwsh` 时，把它名下可见的顶层窗口 `ShowWindow(SW_HIDE)`。
- 玩家直接双击 `game_starter.exe` 时不带 `/hideparent`，行为不变。

### 多兼容启动方式

一套层可以配多份 ini，用不同 bat 选择；默认 `game_starter.bat` / `game_starter.exe` 始终回到默认配置：

| 启动器 | 配置 | 用途 |
|---|---|---|
| `game_starter.bat` / `game_starter.exe` | `ddraw.ini`（如 auto + 1080p） | 默认；硬件优先/自动回退 |
| `game_starter-soft.bat` | `ddraw-soft.ini`（gdi + 1080p） | 强制软件渲染，不依赖 Vulkan |
| `game_starter-640.bat` | `ddraw-640.ini`（gdi + 640×480） | 原版比例/低配 |

备用启动器的流程：复制对应 ini → 启动游戏（阻塞）→ 退出后把 `_cnc-ddraw\ddraw.ini` 复制回来。
默认启动器每次启动也恢复默认 ini，双保险。

### nircmd

- 仓库自带：`tools/nircmd/`（`nircmdc.exe` 命令行版、`nircmd.exe` GUI 版、`NirCmd.chm` 帮助）。
- 交付到游戏目录时放在 `_tools\nircmd\nircmdc.exe`，bat 用相对路径调用。
- 找不到时静默跳过——居中失败不影响游戏启动。

## EXE 启动器（.NET Framework）

单文件 WinForms，零第三方依赖，用系统自带的 `csc.exe` 编译。

**界面规范（配色令牌、三栏布局、键帽画法、键盘焦点、体积红线）见技能
`.qoder\skills\legacy-launcher-gui\SKILL.md`**；参考实现是 `Games\RoadRash\src\Launcher.cs`
（纯 GDI+ 自绘、零图片、game_starter.exe 24 KB）。`templates\Launcher.cs` 是最小可用版（Label + ComboBox），
只适合不需要展示操作说明的极简场景。

```bat
csc -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001 ^
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:game_starter.exe Launcher.cs
```

源码放 `Games\<游戏>\src\Launcher.cs`，配置 `launcher.ini`。中国红主题：

```csharp
BackColor = Color.FromArgb(192, 18, 30);      // 中国红
lblTitle.ForeColor = Color.FromArgb(255, 216, 94);  // 金色
lblNotes.ForeColor = Color.FromArgb(255, 233, 200); // 米色
btn.BackColor = Color.FromArgb(150, 12, 22);
btn.FlatAppearance.BorderColor = Color.FromArgb(255, 216, 94);
```

构建后跑一次体积/资源检查（红线 60 KB，游戏目录与 src 不许出现图片/图标/resx）：

```bat
pwsh -File .qoder\skills\legacy-launcher-gui\scripts\check-launcher.ps1 -Exe out\game_starter.exe -SrcDir Games\<游戏>\src
```

### launcher.ini

```ini
[Launcher]
Title=暴力摩托 Road Rash 2002
GameExe=Roadrash.exe
GameProcess=Roadrash
LayerDll=ddraw.dll
LayerIni=ddraw.ini
LayerBackupDir=_cnc-ddraw
KeymapHelper=_tools\keymap.exe      ; 自绘版：按键方案落地器
RegFile64=road64.reg                ; 模板版：首次缺失时整份导入
RegFile32=road.reg
Notes=第一行\n第二行                 ; 模板版正文，\n 换行
Intro=...\n...\n# 小标题            ; 自绘版左栏（可选，缺省用内置文案）
MenuTips=...\n...                   ; 自绘版右栏菜单说明
FixTips=...\n...                    ; 自绘版右栏故障说明
```

- `GameExe`：直接启动的游戏 exe。
- `LayerDll` / `LayerIni` / `LayerBackupDir`：自检和恢复用；默认启动时从备份目录恢复 `LayerIni`。
- `RegFile64` / `RegFile32`：按 `Environment.Is64BitOperatingSystem` 选择导入；没有管理员权限时静默跳过。
- 外挂型游戏可另加 `LoaderExe` / `GameProcess`，行为见 `templates/Launcher.cs`。
- 界面文案（介绍 / 菜单 / 故障）能从 ini 覆盖，换游戏不改代码；行首 `# ` 是栏内小标题。

### 行为约定

- 启动优先 `LoaderExe`，否则 `GameExe`；工作目录固定为 exe 所在目录。
- 运行期间"启动游戏"按钮禁用并显示"游戏运行中…"；游戏一退出，启动器跟着退出并回传游戏退出码。
- **交付只一个 exe**：自检补层、按键落地、提权都在 `game_starter.exe` 的命令行开关里
  （`/go`、`/keymap N`、`/selftest`），不留 `keymap.exe` / `nircmdc.exe` 这类伴生文件，不引第三方 DLL。
- **窗口永不置顶**（`TopMost=false`）；玩家关窗 = 隐藏陪跑，不是退出游戏。
- 按钮固定三个：**启动游戏 / 打开运行日志 / 退出**，不要「打开游戏目录」。
  技术细节（层、rc、注册表、退出码）全写进 `运行日志.txt`，界面只说人话，状态默认「已就绪」；
  左栏只讲游戏本身，不写 cnc-ddraw / DXVK / 回滚这类说明。
- 自绘版基准尺寸 900×620（三栏 + 存档选项），模板版约 600×380；都是中国红底 + 金色标题。
- 选项一律用**标准下拉框**（画面模式 / 按键方案 / 存档），别用一排列表行，也别自绘：
  **不要** `OwnerDrawFixed` / `FlatStyle.Flat` / 子类化双缓冲——那套正是闪烁的根因，
  只设 `Font`/`BackColor`/`ForeColor`，绘制交给框架；父窗体 hover/选中时只刷按钮区。
  三个下拉框装进同一张卡（标签 + 框成组、组间等间距、整叠垂直居中），
  **不加「将启动」摘要回显**；状态指示全窗口只留左下角一处。
- 读取 ini 用 UTF-8；`\n` 转成换行；行首 `# ` 是栏内小标题。
- 缺文件、配置矛盾要在状态栏写清「哪坏了 + 下一步做什么」，不要静默失败，也不要弹模态框拦人。
- 按键/设置类多方案：**由启动器选，落地走同一个内部通道**（`game_starter.exe /keymap N`）——
  比较现值→相同不动→不同只改写该值→缺值才整份导入→需要时自提权→记住上次选择。
  不要每次启动整份 `.reg` 覆盖，那会连玩家其它设置一起冲掉。
  `切键-*.bat` 调它必须写成 `start "" /wait "%~dp0game_starter.exe" /keymap N`（cmd 不等待 GUI 程序）。

## 水印验证（测试时开，交付时关）

- cnc-ddraw 软件渲染：`renderer=auto` 且 D3D9/OpenGL 不可用时会显示 15 秒
  `-WARNING- Using slow software rendering...`；测试时截图存 `Games\<游戏>\evidence\`。
- DXVK 硬件渲染：`dxvk.conf` 写 `dxvk.hud = fps`，左上角只显示 FPS；排障时才临时加
  `version,api` 看版本/API。
- DXVK 交付配置只写 `dxvk.hud = fps` 作为层挂载标志；cnc-ddraw 软件警告 /
  dgVoodoo 水印只作为诊断信息，交付关闭。原生游戏没有 DXVK 层，不能用这个 HUD 判断。
- **玩家界面说人话**：启动方式只给玩家能懂的结果——`DX11（画质最好）`、`DX9（老机器）`、
  `GDI（最兼容，推荐）`；界面不写 DXVK/dgVoodoo/cnc-ddraw/Mesa/兼容层/HUD/注册表/亲和性/DLL
  文件名，技术细节只进日志和仓库文档。左栏只讲游戏本身。

## 目录布局（交付时）

```
<游戏目录>
  <游戏.exe>
  ddraw.dll / d3d9.dll      翻译层（按需）
  ddraw*.ini / dxvk.conf
  _cnc-ddraw\               留底
  _tools\nircmd\            nircmdc.exe（bat 版居中用）
  兼容修复.bat
  恢复原版.bat
  game_starter.bat / game_starter.exe          默认两个启动器（同逻辑）
  launcher.ini
  game_starter-soft.bat / game_starter-640.bat 可选多模式
  切键-*.bat + road*.reg    可选多套按键/设置方案（配 _tools\keymap.exe 落地 + keymap.txt 记忆）
  _legacy_compat_backup\    可选：一键应用生成的备份与交付记录（撤回用，别手改）

Games\<游戏>\
  src\  tools\  evidence\  backup\
  build.ps1  build-release.ps1
  release\                  交付包：一键应用.bat / 一键撤回.bat / payload\ / manifest.txt
  适配过程分析.md
```

## 一键应用 / 一键撤回（跨机器交付）

- **应用**：拖入或输入目标游戏目录，校验主程序；目标同名文件先复制到
  `_legacy_compat_backup\<游戏>\`，记录到 `交付记录.txt`，再覆盖/复制 payload。可重复执行；
  已有历史备份不重复覆盖。
- **撤回**：按备份恢复被替换的原文件；没有原备份的交付文件，只有和 payload 完全一致时才删除。
  玩家改过的文件保留并写“跳过恢复/删除”，不能悄悄覆盖。
- **不碰**：存档、游戏资源、`ReShade.ini` 等非交付文件。外部配置（如我的文档 `config.ini`）
  和运行日志/cache 无法完全恢复的，写进记录；运行产物可删除并记录。
- 备份目录和 `交付记录.txt` 撤回后保留，作为审计与手工恢复依据；不要在应用前删除它。
- 参考实现：`Games\TheSaboteur\release\`；源脚本在 `src\一键应用.bat` / `src\一键撤回.bat`，
  `build-release.ps1` 负责转 GBK + CRLF 并组装 payload。

## 选择建议

- 玩家要按键表、要一眼看完操作 → 先看 **bat**。
- 游戏本身有菜单、玩家只想双击进游戏 → **exe**。
- 两个都要：**默认两个都交付**，同一目录并存互不影响。
- 想要 1080p/硬件加速/原版分辨率各一种 → 用 `game_starter-soft.bat` / `game_starter-640.bat` 等备用启动器，
  不要让默认两个启动器行为分叉。

## 踩过的坑

- bat 里写复杂 PowerShell（`$t`、`|`、`{}`）会被 cmd 的引号规则吞掉——只用最简循环或干脆阻塞调用。
- **把 `>nul` 写成 `>/dev/null`**：cmd 报“系统找不到指定的路径”，游戏根本不启动。
- `if not exist "x" >"x" echo ...` 的重定向作用于整条 if，会无条件截断文件。
- 中文 bat 存成 UTF-8 无 BOM 会乱码 → GBK + CRLF。
- exe 启动器里不要用 `Process.Start(gameExe)` 后立即关窗口，要等进程退出；外挂型必须按进程名等。
- 窗口居中在 `mode con` 之前调用会按旧尺寸居中，偏。
- 备用模式改完 `ddraw.ini` 不恢复，下次默认启动会沿用错误模式；默认启动器要每次恢复默认 ini。
