---
name: legacy-launcher-gui
description: 老游戏启动器 game_starter.exe 的 GUI 设计规范与实现配方：C# / .NET Framework WinForms + 纯 GDI+ 自绘，不用图片、不引第三方库、exe 控制在几十 KB。给出三栏布局（游戏介绍 / 选项与启动 / 键帽操作说明）、中国红主题令牌、键帽与卡片画法、键盘焦点环、DPI 缩放与体积红线，以及顶掉 bat 启动器必须满足的平台托管契约（游戏是直接子进程、启动器陪跑到退出、退出码原样回传、关窗不杀游戏）与统一命名 game_starter.exe / game_starter.bat。触发词：启动器、game_starter.exe、GUI、界面美化、键帽、exe 体积、WinForms、csc、设计规范、进程树监控、退出码、Playnite 启动。改 Games\<游戏>\src\Launcher.cs、做新游戏启动器界面、或要把 bat 启动器换成 GUI 时用。
---

# 老游戏启动器 GUI 规范

## 定位

一句话：**像街机 / 机车仪表盘，不像 SaaS 后台**。

- 读者是拿到绿色修复包的玩家，不是开发者：一屏看懂「选什么、按什么键、出问题找谁」。
- 整屏只允许一个视觉记忆点 = **右栏的键帽操作表**（把 W/A/S/D 画成真实键帽形状）。
  其余部分克制、等宽、安静。卡片、圆角、阴影都是结构手段，不是装饰。
- 不做：黑底 + 单一荧光色、每块内容一张同样的圆角卡 + 灰阴影、全大写小标题、
  标题上方再叠一行 eyebrow 标签、装饰性动画。

## 硬约束（违反就是错）

1. **只用 .NET Framework 4.x + WinForms + System.Drawing**，系统自带
   `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 单文件编译。
   不建 `.csproj`、不用 `.resx`、不引第三方 DLL（WinForms 是框架自带的，不算）。
2. **零图片、零内嵌字体、零图标资源文件**。键帽、卡片、按钮、状态点全部 GDI+ 现画。
   需要「图标」时用文字 + 形状（`↑` `Alt` `Shift` 就是键帽里的文字）。
   exe 文件图标在构建时从游戏 exe 抽 `.ico` 再嵌入（见铁律 8），`src\` 和游戏目录不放图标文件。
3. **体积红线：game_starter.exe ≤ 60 KB**（实测见 `Games\RoadRash\适配过程分析.md`）。
   超标先查：内嵌资源 / 图片 / 字体 / Designer.cs / 无用的 `using` 引到的类型。
   用 `scripts/check-launcher.ps1 -Exe <game_starter.exe> -SrcDir <Games\某游戏\src>` 一次查完。
4. **交付形态：GUI 与控制台是两个平级启动器，选项必须一致**。`game_starter.bat` **不是转发 GUI 的薄包装**：
   它用 `echo` 自己生成菜单（中国红 `color 4f`），菜单项、顺序、默认值与 `game_starter.exe` 完全相同，
   玩家选完由 bat **自己启动游戏**并回传退出码；`/go` 跳过菜单按默认项启动。
   改选项/加模式必须两边一起改，不许分叉；两边都要满足下面的「平台托管契约」。
5. **中国红主题**（仓库 AGENTS 规则 10）：底色 `#C0121E` 系，强调金 `#FFD85E`，
   正文米白；**不要黑底**（键帽本身可以是深灰，那是物件色不是背景色）。
6. **键位表从 reg 现算，不写死文案**：右栏读当前所选 `road*.reg` 的 `Controls` 字节，
   用游戏内部键码表反查标签。写死的说明一定会和实际键位对不上（踩过坑）。
7. 源文件只放仓库 `Games\<游戏>\src\`，游戏目录只收编译好的 `game_starter.exe` + `launcher.ini`。
8. **图标用现成的，不要自己画**：运行时 `Icon.ExtractAssociatedIcon(游戏 exe)` 取游戏自带图标，
   窗口 / 任务栏 / 托盘共用这一个；取不到退回 `SystemIcons.Application`。
   **exe 文件本身也要带同一个图标**：构建时把该图标存成 `out\game_starter.ico`，再用
   `csc -win32icon:` 嵌入；只设运行时窗口图标，资源管理器里仍是白纸（踩过）。
   自绘图标（`Bitmap.GetHicon`）16px 下发糊、还多养一段代码，试过，不值得。
9. **交付启动器名字统一：GUI `game_starter.exe`，控制台菜单版 `game_starter.bat`**
   （备用模式跟着叫 `game_starter-soft.bat` / `game_starter-640.bat`）。
   源码仍叫 `src\Launcher.cs`，改的是**产物名**，不是文件名习惯。
   **旧名 `go.exe` / `go.bat` 作废**：`go.exe` 与 Go 工具链同名，而且平台进程树里一排
   `go` 分不清是哪个游戏；`game_starter` 在任务管理器 / `Get-Process` 里一眼可辨。
   所有游戏同名，靠所在目录区分。
   交付仍然**只一个 exe**：自检补层、按键落地、提权全长在它里面（`/go`、`/keymap N`、
   `/selftest`），不要再有 `keymap.exe` 之类伴生文件；只用框架自带的
   `System.Windows.Forms` / `System.Drawing`，不引第三方 DLL（多一个文件就多一类"缺文件"故障）。
   例外：控制台菜单版 bat 居中用的 `_tools\nircmd\nircmdc.exe` 只服务 bat（`if exist` 保护），
   不参与 exe 的运行。
   改名要连根拔：`csc -out:`、包装 bat 里的 `start "" /wait`、`切键-*.bat`、
   自检工具的进程名（`GetProcessesByName` / `-Proc`）、构建脚本的**旧名清理**——
   游戏目录里同时留 `go.exe`/`go.bat` 和 `game_starter.*` 就是两套入口，玩家必点错。
   删旧名要有签名守卫（内容里确实含 `game_starter` 才删），别顺手删掉用户自己的启动脚本。
10. **窗口永不置顶**：不要 `TopMost=true`，也不要 `SetWindowPos(HWND_TOPMOST)`。
    玩家要 Alt+Tab 出去看别的，置顶会压住一切窗口。（调试脚本置顶过，别学它。）
11. **必须有「打开运行日志」按钮**：界面不解释技术细节，细节全进日志（见「运行日志」一节）。
    不要「打开游戏目录」这类把玩家往文件堆里塞的按钮。
12. **左栏只讲游戏**：来历、玩法，以及底部的「玩不了或报错」（点「打开运行日志」→ 把内容发给管理员）。
    进游戏说明、操作说明、提示放右栏；不写 cnc-ddraw / DXVK / 注册表 / 回滚脚本这类技术说明。
    玩家界面一律说人话：不出现 `DXVK` / `dgVoodoo` / `cnc-ddraw` / `Mesa` / 兼容层 / 翻译层 /
    HUD / 注册表 / 亲和性 / 偏移 / `0x...` / DLL 文件名。启动方式必须区分 DX9 / DX11 / GDI 时，
    用玩家能懂的结果标注：`DX11（画质最好）`、`DX9（老机器）`、`GDI（最兼容，推荐）`；
    不要写渲染器/管线/API。技术细节只进运行日志和仓库文档。
13. **状态指示全窗口只出现一次**：左下角一个 `● 已就绪` 就够。标题带右上角的角标、
    左栏底部的"兼容层就绪 ddraw.dll"这类第二处状态都算重复，玩家会直接要求删掉。
    出错时才把那一句换成人话 + 下一步（`缺兼容层，先双击 兼容修复.bat`）。
14. 脚本一律用 **`pwsh.exe`（PowerShell 7）** 写和跑，不用 `powershell.exe`
    （5.1 会把无 BOM 的 UTF-8 脚本按 ANSI 读，中文注释直接乱码）。

## 平台托管契约（bat 凭什么被信任，GUI 就得凭什么）

玩家的游戏平台（Playnite 这类）用**启动脚本 + 进程树**监控游戏：脚本崩了、游戏异常退出，
平台要能抓到并报警。bat 之所以好用是因为它天然满足四条，GUI 必须一条不落地复刻：

| # | 平台依赖的性质 | GUI 必须怎么做 |
|---|---|---|
| 1 | 游戏在启动器的**进程树**里 | 用 `Process.Start` + `UseShellExecute=false` 直接起游戏，游戏是 game_starter.exe 的子进程；**不要**用 `shell:"<exe>"`、`explorer.exe`、`cmd /c start` 这类会换父进程的方式 |
| 2 | 启动器活着 = 会话活着 | 游戏运行期间 game_starter.exe 不退出（后台线程 `WaitForExit`），玩家可随时 Alt+Tab 回来看操作说明 |
| 3 | 游戏退出码能回传 | 游戏一退出，game_starter.exe 立刻退出并 `return 游戏退出码`；平台靠 `0xC0000xxx` 这类码判异常。**吞掉退出码 = 崩溃查不到** |
| 4 | 关启动器不杀游戏，且**游戏退出码不能丢** | 关窗 = **隐藏到后台陪跑**（托盘图标双击唤回，图标用 GDI+ 现画，不引图片资源）；游戏退出后 game_starter.exe 才结束并回传游戏码。玩家要是把 game_starter.exe 整个杀掉，游戏仍带着它的父 pid，平台按 pid 自己读游戏退出码（这条路径 game_starter.exe 自身回 -1，不能拿来判异常） |

配套要求：

- **零交互入口**：`game_starter.exe /go` 按当前选择（含 `keymap.txt` 记忆）直接开游戏，给平台当启动命令用；
  不带参数时是交互界面，给玩家自己点。
- **参数支持**：`launcher.ini` 的 `GameArgs`（如 `-game cstrike -window`），`GameExe` 支持相对路径
  （相对游戏目录解析）；裸名字 `cmd.exe` 这种依赖 PATH 的写法在 `UseShellExecute=false` 下会失败，
  要么写全路径，要么相对游戏目录。
- **不要控制台**：`-target:winexe`；任何 `Console.*` 输出都不可依赖。
- **自测开关**：`game_starter.exe /selftest <程序> [参数…]` —— 不起界面，直接 `RunAndWait` 并回传该程序的
  退出码。没有它就只能靠真开游戏验退出码，成本高到没人验。
- **验证方法**：`Games\<游戏>\tools\host-sim.ps1`（PowerShell 版）或 `tools\HostSim.cs` → `hostsim.exe`
  （C# WinForms 版，同一套断言，四条 PASS/FAIL 直接显示在窗口上），改完启动器就跑：
  - `-Auto` / `/auto`：临时把 `launcher.ini` 指到 cmd.exe 当「假游戏」（睡 8 秒退码 7），
    不占屏跑完两条场景，结束自动还原 ini；C# 版进程退出码 = 失败项数，日志写 `hostsim-auto.log`；
  - `-Auto -RealGame` / `/auto /real`：真游戏跑，到点自己结束游戏；
  - 不带参数：开一个模拟主 GUI 的窗口，手动点「启动 / 关启动器窗口 / 强杀 / 结束游戏」走全流程。
  断言四条：① 游戏父 pid == 启动器 pid；② 关窗后启动器仍活着、游戏不受影响；
  ③ 游戏退出后启动器跟着退出且**退出码等于游戏码**；④ 强杀启动器后仍读到游戏自己的码。
  主 GUI 想拿游戏码，必须在**发现游戏进程时就 `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`
  抓句柄**再轮询 `GetExitCodeProcess`——只 `WaitForExit` 启动器是拿不到游戏码的。
  认游戏子进程要按 `launcher.ini` 的 `GameExe` 匹配，别把启动器先起的 `keymap.exe` 当成游戏（踩过）。
- **外挂型游戏（汉化外挂是必需启动器的）例外**：直接子进程只能是外挂（如 DFBHD 的
  `汉化外挂.exe` → `dfbhd.dat`）。做法：`Process.Start` 外挂（`UseShellExecute=false`）；
  按 `launcher.ini` 的 `GameProcess` 进程名找到真正的游戏进程（用启动时间过滤旧进程）；
  等游戏退出并把**游戏的退出码**回传；文档写清“游戏不是启动器的直接子进程”，平台若强依赖
  直接子进程，这是外挂限制。参考 `Games\DFBHD\src\Launcher.cs`。
  Git Bash 会把 `/auto` 当路径转换掉，要么 `MSYS_NO_PATHCONV=1 ./hostsim.exe /auto`，要么写 `//auto`；
  退出码 0 只代表「无失败项」，要再看一眼日志确认它真跑过。
- **平台入口二选一**：
  - 首选 `game_starter.exe /go`（winexe 天生无控制台），平台直接把它当游戏程序；
  - 平台只认脚本时，用 **`game_starter.bat /go`**：跳过菜单、按默认项由 bat 自己启动游戏（阻塞等待），
    退出码天然回传。菜单版 bat 的写法（同选项、同默认值）：
    ```bat
    @echo off&color 4f
    setlocal
    set "tl=<原游戏名>"
    title %tl%
    mode con: lines=32 cols=92 >nul 2>&1
    if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"
    cd /d "%~dp0"
    set "sel=1"
    if /i not "%~1"=="/go" (
      cls
      echo   1  启动游戏（默认）
      echo   2  启动游戏（备用配置）
      set /p "sel=  请选择 [1-2]，直接回车 = 1: "
    )
    set "sel=%sel: =%"
    if "%sel%"=="2" ( <按选项改配置> ) else ( <恢复默认配置> )
    "%~dp0<游戏.exe>" >nul 2>&1
    set "rc=%ERRORLEVEL%"
    <恢复默认配置>
    exit /b %rc%
    ```
    **坑 1**：`if ... echo a&echo b` 里的 `&` 不受 `if` 管，会无条件执行——多行提示要放进 `( )` 块。
    **坑 2**：`set /p` 必须放在块里/独立行，块里不要再 `%sel%`（解析期展开），判断放到块外。
    game_starter.exe 的 `/hideparent` 仍保留：有人把 exe 挂到 bat 后面时用它藏掉父控制台
    （只对 `cmd`/`powershell`/`pwsh` 动手），藏窗口不要依赖 nircmd/PowerShell。
    菜单版 bat **本身就是控制台窗口**，居中用 `_tools\nircmd\nircmdc.exe`（`if exist` 保护，
    放在 `title`/`mode con` 之后）；nircmd 只负责居中，不用来藏窗，缺了不影响启动。
- 启动器界面里没有「退出游戏」这种按钮：关启动器 ≠ 关游戏，脚注要写明白
  （`关窗=隐藏陪跑，游戏退出后自动回传退出码`）。

## 设计令牌

颜色（名字 → 值，代码里做成 `static readonly Color`）：

| 令牌 | 值 | 用途 |
|---|---|---|
| `BaseTop` | `#800020` | 背景渐变上端 |
| `BaseBottom` | `#4A0012` | 背景渐变下端（深红，不是黑） |
| `PanelTop` | `#9B1B30` | 卡片渐变上端 |
| `PanelBottom` | `#6E0F22` | 卡片渐变下端 |
| `BandTop` | `#2A0A0F` | 页眉/页脚渐变带左端（深酒红，压住上下边缘） |
| `BandBottom` | `#4A0E1A` | 页眉/页脚渐变带右端 |
| `PanelEdge` | `#E0574F` | 卡片描边（1px，提亮用） |
| `Gold` | `#FFD85E` | 标题、选中条、主按钮底、焦点环 |
| `GoldDim` | `#C9A227` | 分隔线、次按钮描边 |
| `Ink` | `#FFF3E6` | 正文米白（红底上对比度 ≥ 4.5:1） |
| `InkDim` | `#F0C3A8` | 次要说明、脚注 |
| `OnGold` | `#5C0810` | 金色按钮上的文字（深红，不用黑） |
| `CapFace` | `#2E2E34` | 键帽正面 |
| `CapTop` | `#54545E` | 键帽上沿高光 |
| `CapSide` | `#17171B` | 键帽下沿/右侧阴影 |
| `CapInk` | `#FFFFFF` | 键帽字符 |
| `Ok` | `#7BD88F` | 就绪状态点（必须同时有文字） |
| `Warn` | `#FFB454` | 警告状态点 |

**红色一律走渐变，不用大面积纯色**：窗口底 `BaseTop→BaseBottom`（竖直），
卡片 `PanelTop→PanelBottom`（竖直），页眉/页脚带 `BandTop→BandBottom`（水平）。
三条渐变拉开三层明度，卡片靠描边和金色高光线浮起来。

字阶（系统字体，不嵌字体；**优先微软雅黑**）：

| 角色 | 字体 | 处理 |
|---|---|---|
| 全部界面文字 | `Microsoft YaHei` 优先，缺了依次退 `Microsoft YaHei UI` / `SimHei` / `SimSun` / 系统 UI 字体（运行时用 `InstalledFontCollection` 探测） | — |
| 主标题 | 15pt | Bold + `Gold` |
| 栏标题 | 10.5pt | Bold + `Gold` |
| 选项行 | 10pt | 选中 Bold + `Ink`，未选中 Regular + `InkDim` |
| 正文 | 9.5pt | Regular + `Ink` |
| 键帽字符 | 9pt | Bold + `CapInk` |
| 技术串（版本号、状态） | `Consolas` 优先，缺了退 `Courier New` 9pt | `InkDim` |

尺度：8px 栅格。圆角只两档 —— 卡片 10、行/按钮/键帽 6。间距只用 8 / 12 / 16 / 20。

## 布局

基准 96 DPI 的 `ClientSize = 900 × 620`；右栏内容多时用滚轮滚动，不要把窗口越撑越大；三栏宽度不变。全部尺寸乘 `S()`（DPI 缩放系数）。

```
┌────────────────────────────────────────────────────────────────────────┐
│ 暴力摩托 Road Rash 2002                                                   │ ← 标题带 76
│ EA · Road Rash · 玩家汉化修改版                                           │
├──────────────┬─────────────────────────┬───────────────────────────────┤
│ 关于这款游戏  │      画面模式            │ 进游戏说明                     │
│ 来历 / 汉化… │ ▾[1 兼容模式 1080p 4:3 ] │ 按任意键开始，Pause 暂停…      │
│              │      按键方案            │ 菜单：1 单机 / 2 多人 / 3 读档  │
│ 玩不了或报错  │ ▾[1 修改版按键（默认）  ] │ 选人、买车在「游戏设置」里改    │
│ 点「打开运行  │      存档（选手 1-5 级） │ 操作说明 · 修改版按键（默认）   │
│ 日志」发管理员│ ▾[1 不准备存档        ] │  前进 [↑]   刹车 [Alt]         │
│              │                         │  转向 [←][→] 倾斜 [Home][PgUp] │
│              │ [      启动游戏      ]   │  ─────────────────────────     │
│              │ [打开运行日志]  [ 退出 ]  │ 提示 · 秘技（大小写都行）       │
│              │                         │ 先盲打 XYZZY，名字变黄后再输：  │
│              │                         │ K'THUNK! 木棒  THWACK! 铁鞭    │
│              │                         │ SPOON! 加速    PLUGH 关闭      │
│              │                         │ BRIBE 贿赂警察 YES,OCCIFER 杀   │
│              │                         │ V 看版本   F10 收放仪表盘       │
├──────────────┴─────────────────────────┴───────────────────────────────┤
│ ● 已就绪                 YUNGAME                 游戏退出后本窗口自动关闭 │ ← 状态带
└────────────────────────────────────────────────────────────────────────┘
```

- 三栏宽 252 / 296 / 280，栏间距 16，外边距 20。
- 中栏是**唯一的操作区**：一张卡里按「标签 + 下拉框」成组，画面 / 按键 / 存档自上而下等间距排，
  下面接主按钮 + 两个次按钮；**整叠在卡里垂直居中**（`y = colCenter.Y + (Height - stack) / 2`），
  这样卡片变高只是上下留白对称，不会中间空出一大块。
- **不要再加「将启动」摘要卡**：下拉框里那行加粗白字就是当前选择，重复回显被玩家判定为多余。
- **同类多选项一律用下拉框**（分辨率、按键方案、服务器配置…），不要做成弹层/对话框让玩家多点一次；
  下拉框里那行加粗白字就是当前选择。
- 一个启动器同时管「游戏端 + 服务端」时，中栏分上下两个 groupbox：游戏端在上、服务端在下，
  「打开运行日志 / 退出」放两组中间；服务端配置同样用下拉框，并注明服务端是给专门开服场景用的、
  模板已配好、一般玩直接用游戏端。分组的关键注意事项用「注意：…」一行写在 groupbox 标题下，
  别只藏在右栏长文里。参考实现：`Games\CS16\src\Launcher.cs`。
- **状态指示全窗口只留左下角一个**（`● 已就绪`）。标题带右上角的角标、左下的"兼容层就绪
  ddraw.dll"这类技术状态都算重复，出错时才把那句换成人话 + 下一步。
- **页脚中间固定显示 `YUNGAME` 品牌字样**：`fMono`、`GoldDim`，与状态/提示同一行且居中，
  不能被左右文字挤掉；这是交付品牌标识，不要换成游戏名或技术名。
- **品牌防改（分层，不宣称绝对安全）**：exe 内不要出现明文 `YUNGAME`；用 XOR/编码字节在
  运行时拼出，并对拼出的字符串做 SHA-256 校验；校验失败弹提示后退出。构建后运行
  `scripts/write-launcher-hashes.ps1` 生成 `game_starter.sha256`，平台可用它复核 exe；
  正式发行建议再加 Authenticode 签名。签名/哈希只能提高门槛，不能阻止有决心的逆向。
- 右栏键帽跟随当前按键方案重绘，下面接「提示」（含完整秘技）——主反馈 + 主记忆点都在这里。
- 窗口保持基准 `900×620`，**不要为了塞文案越撑越大**。右栏内容多时**用滚轮滚动**
  （按整行排版、只画视口内的行 + 细滚动条），左栏 ≤18 行；放不下先滚动/删文案，**不许**缩字号到 9pt 以下。
- **同一窗口所有文字用同一种渲染：TextRenderer（GDI ClearType）**，左/中/右、页眉页脚、按钮一致；
  不要全窗改 GDI+ 灰度（整体发虚），也不要把右栏画进离屏位图（ClearType 二次合成会重影）。
- **右栏滚动**：按整行排版，只画完整落在视口内的行（不做裁剪、不做位图）；一行放不下就拆成两行，
  绝不能让 cx 重置造成叠字（放不下就停止该行）。参考 `Games\CS16\src\Launcher.cs`、
  `Games\DFBHD\src\Launcher.cs` 的 `DrawRight` / `DrawWrappedVisible` / `DrawLegendRows`。
- 折行函数按字符断，中文会断词（`Esc 退\n出`很难看）：**每条独立信息自带 `\n`**，
  别用空格把好几条拼成一行让折行去切；折行只当兜底。
- 窗口 `FixedDialog` + `MaximizeBox=false`，`StartPosition=CenterScreen`。

### 文案规范（统一，2026-11-01）

所有 `game_starter.exe` 的**栏目名称、顺序、用词**一致；内容按游戏替换，不许分叉：

| 位置 | 固定文案 | 说明 |
|---|---|---|
| 左栏标题 | 关于这款游戏 | |
| 左栏底部 | 玩不了或报错 → 点「打开运行日志」 → 把内容发给管理员 | 不写“玩不动了”“维护人” |
| 右栏顺序 | 进游戏说明 → 操作说明 · <按键方案> → 提示 | 不写“进游戏怎么用” |
| 中心 | 画面模式/显示 → 按键方案 → 存档/级别 → 启动游戏 → 打开运行日志 → 退出 | 按游戏可增减，顺序固定 |
| 状态栏 | 已就绪 / 游戏退出后本窗口自动关闭 | |

- **秘技/关键提示必须全文写进界面**：触发方式、每条代码、效果都要写清楚；
  **禁止写“完整秘技见某文件”这种指路废话**。内容多就用右栏滚轮滚动（窗口保持 900×620），不要把窗口撑大。
- **按键一律用拟物键帽**：操作说明、一键买枪、秘技/提示里出现的键（数字键、Enter、B、. 等）
  都用同一套 `Keycap` 组件绘制，不能把键名写成普通文字；同一按键在不同栏目的样式一致。
  **键帽只给真正的键盘键**；鼠标/手柄等输入（鼠标、左键）用普通文字，说明文字也用正常字号和亮色，
  不要为了统一把非键盘内容套进键帽。
  拟物键帽只是 GDI+ 画的形状（圆角/高光/阴影），键帽里的字仍用普通 TextRenderer（ClearType）；
  **不要因为键帽去改灰度/离屏位图/特殊抗锯齿**——键帽样式和文字渲染是两件事。
- **用词统一**：管理员（不是维护人）、玩不了或报错（不是玩不动了）、
  进游戏说明（不是进游戏怎么用）、提示（“秘技”只作副标题）。
- **界面不出现技术串**（`rc=`、`0xC0000005`、`ddraw.dll 已就绪`），技术细节进 `运行日志.txt`。
- **左栏不出现技术名词**（Mesa / D3D12 / OpenGL / 驱动名）：玩家只看游戏介绍和「玩不了或报错」；
  兼容层说明只写“画面已适配新系统、直接进游戏”。缺层时界面只说“缺少兼容文件，请先双击 兼容修复.bat”。
- 参考实现：`Games\JustCause\src\Launcher.cs`、`Games\RoadRash\src\Launcher.cs`。

## 组件配方

代码片段见 `references/drawing-recipes.md`，逐个抄用即可：

| 组件 | 做法要点 |
|---|---|
| 背景 | `OnPaint` 里 `LinearGradientBrush(BaseTop→BaseBottom, 90deg)` 刷满；页眉 0–76px、页脚 560–底 两条 `BandTop→BandBottom` 水平渐变带，中间夹红色渐变 |
| 卡片 | 圆角矩形 `PanelTop→PanelBottom` 竖直渐变填充 + `PanelEdge` 1px 描边 + 顶部 1px `Gold` 20% 高光线 |
| 选项 | **标准 `ComboBox`（`DropDownList`，保留模式，不自绘）**：不要 `OwnerDrawFixed` / `FlatStyle.Flat` / 子类化双缓冲，那些只会带来闪烁；框里那行用加粗字体，选中值本身就醒目 |
| 选项分组 | 一张卡里「标签 + 下拉框」成组，组间等间距（标签 22 / 框 30 / 间距 20），整叠垂直居中；**不做「将启动」摘要卡** |
| 主按钮 | 圆角 `Gold` 底 + `OnGold` 粗体字；hover 提亮、按下压暗、禁用 `#6E6E6E` 底 + `#B0B0B0` 字 |
| 次按钮 | 透明底 + `GoldDim` 1px 描边 + `Ink` 字 |
| 键帽 | 圆角矩形 `CapFace`，上沿 2px `CapTop`、下沿 2px `CapSide`，字符居中；宽键（Shift/Home/Page Up）按字数加宽 |
| 状态点 | 直径 8 的实心圆 + 右侧文字；颜色只作辅助，**必须同时有文字** |
| 页脚品牌 | 中间固定 `YUNGAME`，`fMono` + `GoldDim`，与左状态/右提示同一行，居中 |
| 分隔线 | 1px `GoldDim`，只在语义分界处用（键位 vs 秘技），不做装饰 |
| 焦点环 | 2px `Gold` 描边，外扩 2px，仅键盘导航时显示（下拉框靠自绘边框变色） |

## 交互与可访问性

- 鼠标：行/按钮 hover 变亮，`Cursor=Hand`；点击即选中，主按钮点击才启动游戏。
- 键盘（必须做，自绘不等于放弃键盘）：`Tab` / `Shift+Tab` 走「画面 → 按键 → 存档 → 启动 → 目录 → 退出」；
  下拉框的 Tab 要靠窗体 `ProcessCmdKey` 接管（真控件不会自己跟自绘按钮串成一条链），
  框内 `↑↓` 换项、`Enter` 确认；焦点在自绘按钮上时 `Enter` / `Space` 激活、`Esc` 关窗；
  焦点环（按钮）或 2px 金色边框（下拉框）必须看得见。
- 反馈只在状态变化时给：选中态、启动中（按钮文字变「游戏运行中…」并禁用）、
  退出后状态行回写。**不做装饰动画、不做逐卡淡入。**
- 对比度：`Ink` 在 `Panel` 上 ≥ 4.5:1；金色按钮上的字用深红不用黑。
- 状态不能只靠颜色表达（点 + 文字成对出现）。
- 错误文案说清「哪坏了 + 下一步做什么」：
  `兼容层缺失，请先双击 兼容修复.bat`，不要 `启动失败`。

## 下拉框：只用标准控件，别自绘

`ComboBox` 按标准用法写（保留模式，绘制交给框架）：

```csharp
ComboBox cb = new ComboBox();
cb.DropDownStyle = ComboBoxStyle.DropDownList;
cb.BackColor = Color.FromArgb(0x6B, 0x08, 0x12);
cb.ForeColor = Ink;
cb.Font = fRowSel;                 // 加粗：框里那行就是当前选择
cb.Items.AddRange(items);
cb.SelectedIndex = selected;
cb.SelectedIndexChanged += changed;
Controls.Add(cb);
```

**不要** `DrawMode = OwnerDrawFixed`、**不要** `FlatStyle.Flat`、**不要**子类化去
`SetStyle(OptimizedDoubleBuffer)` 或吃 `WM_ERASEBKGND`。踩过的完整一圈：
自绘条目 → 展开/切换时闪 → 加双缓冲子类 → 还闪 → 回退标准控件，**不闪了**。
自绘把一个控件级小问题升级成窗体级问题；标准控件白底黑字在红底卡片上反而更醒目，
玩家一眼看清当前选择，这个视觉一致性不值得拿闪烁去换。

配套只留一条真有用的：**父窗体别在 hover / 选中变化时整窗 `Invalidate()`**，
只刷按钮区（`Invalidate(ButtonsRect)`）——重绘范围越小越稳；下拉框的
`SelectedIndexChanged` 里除了右栏键帽，别的地方不需要重绘。

**别信"录屏量亮度"这种自证**：试过用 ffmpeg 逐帧量下拉框区域平均亮度做 A/B，
两种写法都只出 ±3 波动（鼠标指针、抓帧重复帧就够吃掉这个量），分辨不了闪烁——
测不出来的测量不要拿来当证据。

## 运行日志（玩家报障的唯一入口）

界面上不解释技术，全写进游戏目录的 `运行日志.txt`，玩家点「打开运行日志」用记事本打开，
截图或整个文件发回来即可定位。

- 一行一条：`yyyy-MM-dd HH:mm:ss  事件`，UTF-8；文件不存在就先写一行说明头。
- 必记的事件：启动（带参数与当前三项选择）、点「启动游戏」、画面模式切换、
  存档摆渡结果、按键方案写入结果与 rc、**游戏退出码（写成 0x%X8）**、
  关窗转后台、缺文件/异常（异常记 `ex.ToString()` 带堆栈）。
- 体积自保：超过 ~200 KB 就只保留最后几百行，别把玩家磁盘写满。
- 日志里可以出现 `rc=2`、`0xC0000005` 这类技术串；**界面文案不行**。
- 提权失败、注册表写不进、`reg import` 返回码这些都要落日志，否则远程无从判断。

## 体积与构建

```bat
csc -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001 ^
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:game_starter.exe Launcher.cs
```

- `-codepage:65001`：源码 UTF-8，否则中文字面量在 cmd 下会乱。
- 不加 `-debug`（默认 release 无 pdb）；`-optimize+` 省几 KB。
- 想要高分屏不糊：加 `-win32manifest:app.manifest`（`dpiAware=PM`），代价约 1 KB，
  同时所有尺寸必须走 `S()`；不加也能用，只是 150% 缩放下由系统拉伸略糊。
- **加游戏图标**：构建时 `Icon.ExtractAssociatedIcon(游戏 exe)` → `out\game_starter.ico` →
  `csc -win32icon:`；实测 +5~10 KB，仍在 60 KB 红线内。不要手工画图标，也不要把 `.ico`
  提交进 `src\` 或游戏目录。
- 游戏目录里出现 `.png` / `.jpg` / `.ico` / `.resx` / `.cs` 就算违规，用检查脚本兜底。

## 交付前检查清单

- [ ] `check-launcher.ps1` 通过：exe ≤ 60 KB，src 无图片/资源文件
- [ ] exe 文件图标已嵌入（资源管理器/任务栏显示游戏图标，不是白纸）
- [ ] 截图两张进 `Games\<游戏>\evidence\`：默认态、切到备用按键方案后的键帽变化
- [ ] 100% 与 150% 缩放各看一次，无截断文字、无重叠
- [ ] 键盘 Tab/↑↓/Enter 全程可走通，焦点环可见
- [ ] **不闪**：下拉框是标准控件（无 `OwnerDraw`、无 `FlatStyle.Flat`、无子类化双缓冲），
      hover/选中只局部重绘；最后请人眼确认（录屏量亮度分辨不出来，别拿它当证据）
- [ ] **交付目录只有一个启动器 exe**：没有 `keymap.exe`、`nircmdc.exe` 等伴生文件，没有第三方 DLL
- [ ] 不置顶（`TopMost` 为 false，没有 `HWND_TOPMOST` 调用）
- [ ] 「打开运行日志」可用，且本次启动在日志里留了行；出错信息只进日志不进界面
- [ ] 左栏无技术名词（cnc-ddraw / DXVK / 注册表 / 回滚），状态默认「已就绪」
- [ ] **状态只有一处**：左下角 `● 已就绪`，标题带/卡片里没有第二个角标或摘要卡
- [ ] **页脚中间有 `YUNGAME` 品牌字样**，与左右状态/提示同一行且不重叠
- [ ] **品牌防改**：exe 里没有明文 `YUNGAME`，解码后 SHA-256 校验通过；失败会提示并退出；
      已用 `scripts/write-launcher-hashes.ps1` 生成 `game_starter.sha256`
- [ ] 右栏键帽 = 当前所选 reg 的 `Controls` 反查结果（改 reg 后界面跟着变）
- [ ] **键帽只给真正的键盘键**：鼠标/左键/手柄等输入是普通文字，说明文字用正常亮色（不是暗色），
      没有为了统一把非键盘内容套进键帽
- [ ] **拟物键帽没有牵动文字渲染**：全窗口仍是 TextRenderer ClearType，键帽里的字也是普通字；
      没有因为键帽引入灰度/离屏位图/特殊抗锯齿
- [ ] **托管契约回归通过**：`tools\host-sim.ps1 -Auto`（假游戏）四条断言全 OK
- [ ] `game_starter.exe /go` 零交互能直接进游戏（平台启动命令用它）
- [ ] 若仍交付 `game_starter.bat`：与 `game_starter.exe` 选项、顺序、默认值、启动行为一致
- [ ] 启动/退出行为未回归：自检补层、恢复默认 ini、阻塞等进程退出、按键方案写入
- [ ] `launcher.ini` 仍可覆盖标题/exe/参数/层文件名/文案，换游戏不改代码

## Resources

- `references/drawing-recipes.md`：可直接抄的 GDI+ 片段（渐变底、卡片、键帽、分段选项、
  焦点环、DPI 缩放、`Controls` 字节 → 键帽标签反查表）。
- `scripts/check-launcher.ps1`：体积 + 违规文件检查，构建后跑一次。
- 参考实现：仓库 `Games\RoadRash\src\Launcher.cs`（本规范的首个落地版本）。
