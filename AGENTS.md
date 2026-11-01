# AGENTS.md

给接手这个工作空间的 AI / 协作者看。目标只有一句：

> **不改系统、不装驱动、不动全局设置**，用绿色翻译层让老游戏在新系统和新显卡上跑起来，
> 并把每一次经验沉淀回这个仓库。

## 仓库结构

| 路径 | 作用 |
|---|---|
| `SKILL.md` | 技能本体：铁律、探针流程、选层决策表、验证清单 |
| `references/` | 诊断 / DXVK / dgVoodoo2 / 分辨率 / 启动器 / 常见坑 / 实战案例 |
| `.qoder\skills\legacy-launcher-gui\` | 启动器 GUI 设计规范（WinForms 自绘、令牌、键帽画法、体积红线） |
| `scripts/` | `D3D9Probe.cs`（原生 D3D9 探针）、`FixGame.cs`（自动装层/回滚）、`build.bat` |
| `templates/` | `game_starter.bat`、`game_starter-loader.bat`、`兼容修复.bat`、`恢复原版.bat`、`Launcher.cs` |
| `workflow/` | `adapt.ps1` / `adapt.bat`：一键跑完探测→选层→落地→验证→报告 |
| `Games/` | 每个游戏一个隔离工作区：`Games\<游戏>\src`（源文件）、`tools`、`evidence`、`backup`、`build.ps1`、`适配过程分析.md` |
| `INSTALL.md` | 给别人的安装说明（AI IDE 技能 / 命令行） |
| `tools/` | DXVK 2.5.3（x86/x64）、dgVoodoo2 2.87.6（x86/x64）、nircmd（bat 窗口居中） |

## 必须遵守

1. **先探针，再选层**。不跑 `D3D9Probe` 就下结论 = 猜。
2. **一切落在游戏目录**：`d3d8.dll` / `d3d9.dll` / `dxvk.conf` + `_dxvk\` 留底。
3. **改前备份**，并交付 `兼容修复.bat` 与 `恢复原版.bat`；改完保证能一键回滚。
4. **验证要有客观证据**：游戏自带的视频/分辨率测试、进游戏截图、DXVK HUD，不接受"进程没退"。
5. **不碰系统**：不装/换驱动、不关服务、不往 system32 写东西；默认不改全局注册表。
   **唯一例外**：整机 D3D9 被“DX 修复工具”写坏时，用 `scripts\修复本机D3D9.bat` 备份并删除
   `Direct3D\Drivers\SoftwareOnly` / `DirectDraw\EmulationOnly`（64 位 + WOW6432Node），
   然后复测；这是已诊断、可回滚的定向修复，不是“通用优化”。
6. **不动用户手改过的文件**：bat 的标题、窗口尺寸、居中行等只做外科式修改。
7. **反作弊网游一律不接**，翻译层会被判外挂。
8. **交付的启动器名字统一**：GUI 启动器 `game_starter.exe`，控制台启动器 `game_starter.bat`
   （旧名 `go.exe` / `go.bat` 作废：`go.exe` 与 Go 工具链同名，进程树里一排 `go` 分不清是哪个游戏）。
   所有游戏同名，靠所在目录区分。
   **`game_starter.bat` 是和 GUI 平级的正式启动器，不是转发 GUI 的薄包装**：用 `echo` 自己生成菜单，
   菜单项、顺序、默认值与 `game_starter.exe` **完全一致**，玩家选完由 bat 自己启动游戏、游戏退出后
   回传退出码；改选项必须两边一起改，不许分叉。`game_starter.bat /go` 跳过菜单按默认项启动，
   给只认脚本的平台当零交互入口。
   GUI 满足平台托管契约：游戏是 `game_starter.exe` 的直接子进程、
   游戏运行期间启动器陪跑、游戏退出后启动器跟着退出并**原样回传游戏退出码**（平台靠
   `0xC0000xxx` 判崩溃）、玩家关启动器窗口**不杀游戏**（进程树里游戏仍挂 `game_starter.exe` 的 pid，
   平台照监控）、`game_starter.exe /go` 当零交互启动命令用。平台入口优先 `game_starter.exe /go`，
   平台只认脚本时用 `game_starter.bat /go`（bat 阻塞等待游戏，退出码天然回传）。
   若有人把 exe 挂到 bat 后面启动，可用 `game_starter.exe /go /hideparent` 藏掉父控制台（藏窗口不要依赖 nircmd/PowerShell）。
   菜单版 bat 本身就是控制台窗口，居中用 `_tools\nircmd\nircmdc.exe`（`if exist` 保护，缺了照样能启动）。
   界面与契约细节见技能
   `.qoder\skills\legacy-launcher-gui\SKILL.md`，通用约定见 `references/launchers.md`。
   注册表导入要**幂等**：只在关键值不存在时导入，别每次启动覆盖玩家在游戏内的改键/设置；
   玩家**显式选择**的方案（如启动器里的按键方案 1/2/3）按选择写入，且落地统一走一个小工具：
   比较值→相同不动→不同只改写那一个值→缺值才整份导入→需要时自提权。多套方案也可给独立切换脚本。
   **GUI 交付只一个 exe**（自检、按键落地、提权都在 `game_starter.exe` 的命令行开关里，不留 `keymap.exe`
   这类伴生文件，也不引第三方 DLL）；控制台菜单版 bat 需要居中时随包带 `_tools\nircmd\nircmdc.exe`，
   它只服务 bat（`if exist` 保护），不参与 exe。**exe 文件本身必须带游戏图标**：构建时用
   `Icon.ExtractAssociatedIcon(游戏 exe)` 生成 `out\game_starter.ico`，再 `csc -win32icon:` 嵌入
   （窗口/任务栏/资源管理器同图标；`.ico` 只在 `out\` 生成，不进 `src\` 和游戏目录）。
   **窗口永不置顶**；必须有
   「打开运行日志」按钮（技术细节全进 `运行日志.txt`，界面只说人话），
   不要「打开游戏目录」；左栏只讲游戏本身，状态默认「已就绪」。
9. **每个游戏的修改隔离在 `Games\<游戏>\`**：源码、工具、证据、分析都在这里；游戏目录只放成品，
   **不放 `.cs` / `.ps1`**。bat 源码 UTF-8，构建时转 GBK+CRLF；exe 编译后复制过去。
10. **启动器中国红**：bat `color 4f`；exe 背景 `#C0121E` + 金色 `#FFD85E`，不要黑底。
    exe 的具体视觉令牌（配色/字阶/三栏布局/键帽画法）见技能 `.qoder\skills\legacy-launcher-gui`。
11. **测试时显示渲染水印，交付时关掉**：cnc-ddraw 的 GDI 软件警告 / DXVK HUD / dgVoodoo 水印，
    用来证明层生效和硬件/软件路径；DXVK HUD 用 `version,api,fps`（临时 `DXVK_HUD=version,api,fps`
    或 `dxvk.hud`），**`devinfo` 只显示显卡型号，不能当兼容层证据**；证据存 `Games\<游戏>\evidence\`。
12. **分辨率要实测并如实记录**：DirectDraw 老游戏多是固定 640×480，1080p 只是 cnc-ddraw 输出放大；
    640 与 1080 都要截图对比，不能写成原生 1080p。
13. **允许一个游戏多个兼容启动方式**（默认 auto、纯软件、原版 640 等），但默认 `game_starter.exe`
    必须自解释、每次启动回到默认配置；同交付 `game_starter.bat` 时两者选项/顺序/默认值必须一致。
14. **调好的配置文件要回存 `Games\src`**，否则重新构建会被上游默认配置覆盖。
15. **整机 D3D9 全挂先清全局开关**：所有 D3D9 游戏同时挂、D3D11/12/Vulkan 正常时，先跑
    `scripts\修复本机D3D9.bat` 再复测；**禁止先重装/换驱动/装更新/上 DXVK**。
    完整复盘与死胡同清单见 `references/d3d9-global-failure.md`。

## 本机（当前开发机）事实

- Windows 11 x64，工作盘 `D:`。
- 显卡：AMD RX 580（当前官方驱动 `31.0.21925.1001`）。Windows Update 曾反复降级到
  `31.0.12027.9001`；已暂停自动更新并禁止驱动自动更新（`scripts\恢复Windows更新.bat` 可撤销）。
  虚拟显示器 OrayIdd / GameViewer 已卸载，不再作为变量。
- **2026-11-01：原生 D3D9 曾被“DirectX 修复工具”写坏并已修复。** 根因是
  `HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly=1`（64 位 + WOW6432Node 两个视图）
  和 `DirectDraw\EmulationOnly=1`（64 位）——全局强制软件渲染，导致整机 D3D9 全部 `0x8876086A`，
  D3D9On12 同样失败，但 D3D11/12/Vulkan 正常。清除后原生 D3D9 恢复（War3 1.27 原生 1920×1080 实测）。
  **遇到所有 D3D9 游戏同时挂，先跑 `scripts\修复本机D3D9.bat`，不要先换驱动/上 DXVK。**
- dgVoodoo2 的 D3D11 / D3D12 后端在这台机不可用；DXVK（Vulkan）和 Mesa（D3D12）可跑，
  但 D3D9 已原生修好，优先原生；DXVK 只作为个别游戏的备选。
- 无硬件 OpenGL（只有 GL 1.1 / GDI Generic）。
- 推论：探针 `0x8876086A` 先排除全局开关；本机验证“某层能不能用”仍只信实测。

## 工具与环境

- C# 编译：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，
  编 32 位加 `-platform:x86`（老游戏基本 32 位，32 位包装 DLL 才会被加载）。
- 一键编译探针和工具：`scripts\build.bat`。
- **脚本一律用 `pwsh.exe`（PowerShell 7）写和跑**，不要用 `powershell.exe`（5.1 会把无 BOM 的
  UTF-8 脚本按 ANSI 读，中文注释/字符串直接乱码）。
- 下载：GitHub 走 `https://gh-proxy.com/<原始URL>`；npm 用 npmmirror；pip 用清华源；`curl` 加
  `--ssl-no-revoke`。
- 解 7z：`D:\Temp\nj\node_modules\7zip-bin\win\x64\7za.exe`。
- 截图 / 点键自动化：PowerShell + `System.Drawing.CopyFromScreen`、`SetCursorPos` + `mouse_event`。

## 干活流程（照着走）

0. **先跑工作流**：`workflow\adapt.bat -GameDir "<游戏目录>" -DryRun` 看探针和主程序选择；
   确认无误后去掉 `-DryRun` 正式执行（会自动生成启动器和报告）。脚本覆盖不到的部分再按下面步骤手工处理。


1. **复现**：启动游戏，截全屏图；读 `ReShade` 的 `d3d9.log` 或 DXVK 的 `<exe>_d3d9.log`，
   把错误原文抄下来（例如 `D3DERR_NOTAVAILABLE`、`The resolution test has failed`）。
2. **探针**：`scripts\build.bat` → `D3D9Probe32.exe`；再把候选 `d3d9.dll`/`d3d8.dll` 放到探针
   同目录再跑一次，看这层能不能替代原生。**整机 D3D9 全挂时，先跑 `scripts\修复本机D3D9.bat` 清全局开关再复测。**
3. **选层**：查 `SKILL.md` 决策表。
4. **落地**：源文件写进 `Games\<游戏>\src`，`build.ps1` 编译/转码后复制到游戏目录：
   `ddraw.dll` / `d3d9.dll` + `ddraw*.ini` + `dxvk.conf` + `_cnc-ddraw\` +
   `兼容修复.bat` / `恢复原版.bat` + `game_starter.exe` / `launcher.ini`（可选 `game_starter.bat`、`game_starter-soft.bat` 等）。
5. **验证**：先开水印/HUD 确认渲染路径，再跑默认/备用模式；分辨率测试全 PASS / 进一局截图正常；
   640 与 1080 对比；退出后桌面恢复。
6. **收尾**：杀掉测试进程、确认桌面分辨率复原、把结论写进 `references/cases.md` 和
   `Games\<游戏>\适配过程分析.md`；水印证据只留在 `evidence\`，交付配置关掉水印。

## 游戏侧交付形态

```
<游戏目录>
  ddraw.dll / d3d9.dll     生效中的翻译层（DirectDraw 用 ddraw.dll；硬件加速再加 DXVK d3d9.dll）
  ddraw.ini                默认配置（分辨率/渲染器）
  ddraw-soft.ini           可选：纯软件渲染
  ddraw-640.ini            可选：原版分辨率
  dxvk.conf                可选，交付时关闭 HUD
  _cnc-ddraw\              留底（重装用）
  兼容修复.bat             从 _cnc-ddraw 复制上来，遇同名 dll 先备份
  恢复原版.bat             删除翻译层并还原备份
  _tools\nircmd\           nircmdc.exe（bat 版窗口居中，仅保留 bat 时才需要）
  game_starter.exe       默认启动器：满足平台托管契约（子进程/陪跑/回传退出码/关窗不杀游戏）
  launcher.ini             game_starter.exe 配置（标题、exe、参数、层、文案都可覆盖）
  game_starter.bat     控制台菜单版启动器：菜单项/顺序/默认值与 exe 一致，自己启动游戏；/go 零交互
  road64.reg / road.reg    游戏自己的设置（只在关键值缺失时整份导入；平时只改写被选中的那一个值）
  切键-*.bat               可选：多套按键/设置的切换脚本（内部走同一个落地小工具）
  keymap.txt               记住启动器里上次选的按键方案
  game_starter-soft.bat / game_starter-640.bat 可选：纯软件 / 原版 640×480 启动方式
```

`Games\<游戏>\` 里保留源文件、测试脚本、水印/画面证据、构建脚本和 `适配过程分析.md`；
游戏目录里出现 `.cs` / `.ps1` 就是违规。

## bat 规范（用户偏好，务必遵守）

- **GBK + CRLF**（简体中文系统），否则中文乱码甚至解析出错。
- **中国红主题**：`@echo off&color 4f`（红底白字），不要 `1f` 蓝底/黑底。
- `set "tl=..."` + `title %tl%` + `mode con: lines=.. cols=..`。
- 纯 `echo.` 提示表；**不要 `:标签` / `goto` / `pause`**（修复/恢复脚本末尾的 pause 除外）。
- **启动器 bat 要有和 `game_starter.exe` 一样的菜单**：菜单项、顺序、默认值一致，`set /p` 选完自己启动游戏；
  `/go` 跳过菜单。多行提示放进 `( )` 块（`if ... echo a&echo b` 的 `&` 不受 `if` 管）；
  `set /p` 从管道读到的值可能带尾随空格，判断前先 `set "sel=%sel: =%"`；`%sel%` 的判断写在块外。
- **控制台窗口居中用 nircmd**：`if exist "%~dp0_tools\nircmd\nircmdc.exe" "%~dp0_tools\nircmd\nircmdc.exe" win center ititle "%tl%"`，
  放在 `title` / `mode con` 之后；工具缺失时不影响启动。nircmd 只用来居中，别拿它藏窗口。
- 最后一行直接 `"%~dp0游戏.exe" >nul 2>&1`（阻塞调用，顺便压掉 DXVK 日志），
  紧跟 `set "rc=%ERRORLEVEL%"` 接住退出码，再 `exit /b %rc%`；游戏退出窗口自动关。
  **cmd 只认 `>nul`，不要写 `>/dev/null`**（会报“系统找不到指定的路径”）。
- 外挂型启动（汉化外挂这类"拉起就退出"的加载器）：`start ""` + 进程名等待循环。
- **不要在 bat 里写复杂 PowerShell**：`$`、`|`、`{}` 经 cmd 引号剥离后容易失效。
- `if not exist "x" >"x" echo ...` 的重定向作用于整条 if，会无条件截断文件——要么拆两行，要么接受。
- 多模式启动器（game_starter-soft / -640）启动前复制对应 ini，游戏退出后把 `_cnc-ddraw\ddraw.ini` 复制回来；
  默认 game_starter.bat/game_starter.exe 每次启动也恢复默认 ini，防止残留。

## 记录规范（这个仓库的核心价值）

- **新游戏** → `references/cases.md` 加一节：症状原文、探针输出、试过什么、最终方案、验证证据；
  同时在 `Games\<游戏>\适配过程分析.md` 写完整测试矩阵（原生/各层/各分辨率/水印/硬件软件对比）。
- **新坑** → `references/pitfalls.md` 加一行：症状 → 原因 → 处理。
- **规则变了** → 改 `SKILL.md` 决策表，别只写在聊天里。
- **测试证据** → 截图/日志/HUD 存 `Games\<游戏>\evidence\`；交付配置不保留水印/HUD。
- 只写实测结论；没验证的标注"未验证"，不要写"应该/可能"。
