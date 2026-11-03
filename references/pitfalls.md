# 踩坑清单

格式：**症状 → 原因 → 处理**。全部来自实测。

## 图形层

| # | 症状 | 原因 | 处理 |
|---|---|---|---|
| 1 | 探针结果和游戏表现对不上 | 用 64 位编译的探针，同目录的 32 位包装 DLL 根本没被加载 | `csc -platform:x86` 重新编译 |
| 2 | 同一个游戏换台机器结论不同 | 层是宿主环境相关的：显卡、驱动、是否有虚拟显示器都影响 | 每台机都先跑探针，别照搬结论 |
| 3 | DXVK 装上后白屏 | 版本不匹配 —— 2.3.1 在 The Saboteur 白屏，2.5.3 正常 | 换另一个 DXVK 版本再试 |
| 4 | DXVK 装上后游戏秒退 | DLL 位数不对，或游戏根本不用这个 API | 确认 exe 位数与导入表 |
| 5 | DXVK 报 `No adapters found` | DXVK 3.x 需要 `VK_KHR_load_store_op_none`，老 AMD 驱动没暴露 | 回退 DXVK 2.3.1 / 2.5.3 |
| 6 | dgVoodoo 下 UI / 过场全洋红 | D3D11 后端对某些引擎纹理格式处理错误 | 换 DXVK；或 D3D12 后端（可能崩） |
| 7 | dgVoodoo 下 `CreateVertexBuffer` 返回 NULL 后崩溃 | 引擎不接受 dgVoodoo 改写的 API 语义（pure3d 引擎典型） | 只能换 DXVK |
| 8 | dgVoodoo 设成 D3D12 后游戏直接退出 | 宿主 D3D12 路径对老游戏不可用 | 回 D3D11；还不行换 DXVK |
| 9 | 游戏目录里已经有 `d3d9.dll` | 多半是 ReShade / 旧 dgVoodoo 塞的，与翻译层冲突 | 看文件版本信息确认来源，改名 `.xxx-bak` 留底 |
| 10 | DXVK 日志只有设备信息、没有 `ResetSwapChain` | 游戏卡在"建设备后、首次 Present 前"（常见于白屏） | 先换 DXVK 版本；再考虑关闭 ReShade / 视频初始化相关项 |
| 11 | 游戏能起但一直纯白/纯黑 | 可能是在等 Bink 过场或某个注入层 | 读日志、看已加载模块、试禁用过场 |
| 26 | 老游戏弹“未安装 DirectX 驱动或版本太低”，有时又能跑 | 纯 DirectDraw 游戏（只导入 `DDRAW.dll`）在现代 Windows 上独占全屏/8 位调色板初始化时好时坏 | 上 **cnc-ddraw**（`ddraw.dll` + `ddraw.ini`），不要上 DXVK d3d9 直替 |
| 27 | cnc-ddraw 顶部显示 15 秒 `-WARNING- Using slow software rendering...` | `renderer=auto` 时 D3D9/OpenGL 都不可用，回退 GDI | 测试时把它当“软件渲染水印”；交付要无警告就 `renderer=gdi`（强制软件）或让 auto 走 DXVK（硬件） |
| 28 | cnc-ddraw DXVK 模式在 640×480 开场黑屏 8~16 秒 | 分辨率/渲染组合问题；1080p + DXVK 同游戏正常 | 换 1080p 输出或 GDI；不要只凭“进程没退”判断 |
| 29 | 把 640×480 游戏说成“1080p 画质提升” | 引擎内部固定 640×480，cnc-ddraw 只是输出放大 | 用 `width/height + fullscreen + maintas` 放大，如实记录“输出 1080p / 内部 640×480”；640 与 1080 都截图对比 |
| 49 | **所有** D3D9 游戏同时报“不能初始化DirectX”/`0x8876086A`；dxdiag `D3D Status: Disabled`；`D3D9On12` 也失败；但 D3D11/12/Vulkan 正常；换驱动、DDU、重启全无效 | “DirectX 修复/优化”类工具写入了**全局强制软件渲染开关**：`HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly=1`（**64 位和 WOW6432Node 两个视图**）和/或 `HKLM\SOFTWARE\Microsoft\DirectDraw\EmulationOnly=1`。注意在 **`Direct3D\Drivers` 子键**，不在 `Direct3D` 根键；重装显卡驱动不会清除 | 备份后删除这两个值（**两个视图都要删**；32 位游戏/探针读 WOW6432Node），**不需要重启**；直接跑 `scripts\修复本机D3D9.bat`，再用 `D3D9Probe32.exe` 复测（全 `0x00000000` 即恢复）。**先清开关，再考虑 DXVK/驱动** |
| 50 | 探针输出自相矛盾（显示模式 `S_OK` 但 0×0；`adapters: 2`；adapter1 全 `INVALIDCALL`） | 探针自身有 bug：`printf` 参数求值顺序、C# COM vtable 索引写错（索引 1 是 AddRef，`GetAdapterCount` 是 4，`CreateDevice` 是 16）、用 `GetDesktopWindow()` 当设备窗口 | 用 C++ **真实窗口**探针**先调用、再单独打印**复核；32/64 位都跑。探针不可信时，后面所有推理都不可信 |
| 51 | `D3D9On12` 和原生 D3D9 一起 `0x8876086A` | D3D9 运行时被全局 `SoftwareOnly` 强制软件；不是 AMD UMD 单独坏 | 先清 `Direct3D\Drivers\SoftwareOnly`（两个视图）再复测；9on12 同挂是运行时级开关的强信号 |
| 52 | 删了 `DirectDraw\EmulationOnly` 还是不行 | 漏了 `Direct3D\Drivers\SoftwareOnly`；或只查了 64 位视图；32 位游戏读 WOW6432Node | 两个开关 × 两个视图；开关在 **`Direct3D\Drivers` 子键**，不在 `Direct3D` 根键 |
| 53 | 换驱动、DDU、重启、装 DirectX 运行库、Windows 更新都无效 | 根因不在驱动/运行库；这些操作还可能把能用的驱动换坏（本次 WU 曾 6 次来回降级） | 先查全局开关；暂停 WU 驱动自动更新；别拿重装/更新当排查手段 |

## 分辨率

| # | 症状 | 原因 | 处理 |
|---|---|---|---|
| 12 | `d3d9.forceAspectRatio` 改完分辨率没变 | DXVK 只改宽高比报告，不改分辨率 | 别指望 DXVK 强制分辨率 |
| 13 | 游戏里找不到 1920x1080 选项 | 老引擎档位表写死 4:3（最高 1600x1200 / 1920x1440） | 选最高档全屏让显卡缩放；或上 dgVoodoo 的 Force resolution |
| 14 | 改了 `game_res` 却回落 800x600 | 该档位开关没开，或显示器不报告该模式 | 先开 `hw3d_res1600x1200 = 1` 等标志；1920x1440 在 1080p 屏上不可用 |
| 15 | 退出游戏后桌面分辨率被改成 800x600 | 游戏切换显示模式后没恢复 | 启动前记录、退出后校验；测试脚本里要还原 1920x1080 |

## 启动器 / 脚本

| # | 症状 | 原因 | 处理 |
|---|---|---|---|
| 16 | bat 里的 PowerShell 等待循环立刻结束 | `$t`、`|`、`{}` 经 cmd 引号剥离后失效 | 能不用就不用；等待游戏退出直接把 exe 写成阻塞调用 |
| 17 | `if not exist "x" >"x" echo ...` 把文件清空 | 重定向作用于整条 if 语句 | 拆成两行，或接受"每次重写同样内容" |
| 18 | 中文 bat 乱码甚至命令失效 | 存成了 UTF-8 无 BOM | 简体中文系统用 **GBK + CRLF**；仓库里的模板保持 UTF-8，落地时转码 |
| 19 | 启动器窗口和游戏一起消失 / 不消失 | 启动器非阻塞（外挂型加载器拉起游戏就退出） | 用 `start ""` + 进程名等待；或用阻塞调用直接跑游戏 exe |
| 20 | 第二次启动游戏立刻退出 | 上一个实例没完全退出 | 启动前检查/清理同名进程 |
| 30 | bat 报“系统找不到指定的路径”，游戏不启动 | 把 bash 习惯写成 `>/dev/null`，cmd 不认 | 改成 `>nul 2>&1`；cmd 只有 `nul` |
| 31 | 调好的 `ddraw.ini` 重新构建后被还原成上游默认 | 配置只改了游戏目录，构建又从上游复制 | 调好的配置回存 `Games\<游戏>\src`，构建只从 `src` 取 |
| 32 | 用了 `game_starter-640.bat` 后，下次 `game_starter.bat` 仍是 640 | 备用模式改了 `ddraw.ini` 没恢复 | 备用启动器退出后恢复 `_cnc-ddraw\ddraw.ini`；默认启动器每次启动也恢复默认 |
| 33 | 交付的启动器是黑底 / 蓝底，用户不满意 | 沿用了旧模板 `color 1f` / 深色 WinForms | 中国红：bat `color 4f`；exe `#C0121E` + 金色 `#FFD85E` |

## 游戏本身

| # | 症状 | 原因 | 处理 |
|---|---|---|---|
| 21 | 单机模式点不动 / 没有单机 | 启动的是 **LAN 客户端**（如 `DFBHDLC.EXE`） | 换完整版 exe，或走汉化外挂（它拉起的是 `dfbhd.dat`） |
| 22 | 中文乱码 | 汉化是外挂 DLL 注入 + 独立字体/资源包，绕过外挂直接跑 exe 就没了 | 必须从汉化外挂启动器启动 |
| 23 | 进程静默退出、无窗口 | 免 CD / 光盘检测失败，不是图形问题 | 找对应免 CD/补丁版 exe，别在图形层上耗 |
| 24 | 弹 DLL 缺失 | 缺 DirectX 9.0c 运行时 / VC++ 运行库 | 装官方运行时，不需要兼容层 |
| 25 | 带反作弊的网游被封号 | 翻译层被判定为外挂 | 不接这类游戏 |
| 34 | DirectDraw 游戏装了 DirectX 9.0c 运行时仍报“未安装 DirectX” | 报错来自 DirectDraw 初始化失败，不是缺运行时 | 上 cnc-ddraw；不要反复装 DX9 |
| 35 | 老游戏的“默认按键”写进 reg 后，游戏界面显示成 Alt 等怪键；想加自定义键位又不知道该填什么码 | 特殊键码不是直觉值：Road Rash 用 `Shift=0x90 Ctrl=0x91 Alt=0x92 ←0x93 ↑0x94 →0x95 ↓0x96 Home0x84 PageUp0x81 Insert0x85`，字母=ASCII 小写；按“对称”猜值必翻车 | 两个可信来源：① 未修改原版目录的 reg/配置；② **游戏运行时用 `ReadProcessMemory` 读它的 VK→键码映射表**（Road Rash 在 `0x4c5d90`，256 字节，下标=VK），比在游戏里逐键绑定快且准；都没有才用游戏内绑定后导出 |
| 36 | 启动器上写的按键说明和实际生效的键位不一致 | 键位文案写死在 exe 里，reg 改了说明没跟着改 | 界面键位表从 reg 的 `Controls` 现算，再用键码反查标签渲染（见技能 `legacy-launcher-gui`） |
| 37 | 玩家看启动器说「怎么两个已就绪」「下面那块『将启动』没用」 | 同一信息画了两遍（标题带角标 + 状态带；下拉框已显示选择又加摘要卡）；折行函数按字符断，中文被切成 `Esc 退/出` | 状态指示全窗口只留左下角一处，不做摘要回显；每条独立信息在文案里自带 `\n`，别指望折行。规则已进技能铁律 12/13 |
| 38 | 托管契约自检偶发 FAIL「强杀 game_starter.exe 后读不到游戏退出码」 | 同时跑了两个 `hostsim.exe /auto`，两者开头的 `KillAll()` 会按进程名互砍对方的 game_starter.exe / 假游戏 | 一次只跑一个，等上一条返回再跑；跑前 `Get-Process game_starter,hostsim` 确认没有残留 |
| 39 | 启动器改名后玩家仍点到旧入口、自检按进程名找不到启动器 | 只改了 `csc -out:`：游戏目录里旧名 `go.exe`/`go.bat` 还在，包装 bat、`切键-*.bat`、自检工具的进程名没跟着改 | 交付名**统一 `game_starter.exe` + `game_starter.bat`**（技能铁律 9）；改名连根拔：构建脚本删旧名（删前用内容签名确认是我们的包装，别误删玩家脚本）+ bat 调用 + 工具 `-Proc`/`GetProcessesByName` 一起改，改完重跑托管契约与 bat 包装两条回归，并提醒玩家**平台入口要改成新名字** |
| 40 | 改了存档文件里的字段（金钱/玩家名），游戏照旧从 1 级开跑，也不报错 | 定长存档记录开头几个字节往往是**覆盖整条记录的校验和**，不匹配时游戏静默放弃载入 | 先 A/B 验证（原始文件能载入 / 改动文件不能载入），再从 exe 里逆出校验函数（找魔数 `AD DE FE CA` 的引用点最快），改完**回写校验和**再测；改前整目录备份（见 `Games\RoadRash	oolsix_saves.py`） |
| 41 | 命令行给老游戏传存档/文件参数没生效 | 1995 前后的程序多按**空白**切参数，带空格的文件名会被拆成两个参数丢掉 | 文件名去掉空白（中文可以），或用游戏能接受的引号形式；传错参数时游戏常自报用法（本作弹 `Usage: Roadrash [savefile]`），照它给 |
| 42 | 汉化版老游戏所有汉字变方块（菜单/HUD/字幕全是 `□□□`） | 汉化靠补丁 ASI 改 exe 的 UTF-8 解码器，ASI 没跑起来就还是原版映射（如 Just Cause 把三字节 UTF-8 固定映射到 `U+E000+` 私用区，字库没这些字形） | 先让图形层跑通，再看汉化 ASI 是否加载/崩溃；反编译 ASI 找它对 exe 的字节补丁，能单独提出来就裁掉其它 hook（Just Cause 案例：`Games\JustCause\src\build-textfix.py`） |
| 43 | 给 DLL 打 `E9 rel32` 跳转补丁，编译测试都没事，一启动就 `0xC0000005` | 补丁位置踩在 **PE 重定位项**上（`IMAGE_REL_BASED_HIGHLOW`）：DLL 被加载到非首选基址时 loader 把增量加进你的 rel32，跳转飞掉 | 补丁前查 `.reloc`（脚本解析/`Ghidra` 都行），只选无重定位覆盖的字节；或改用相对本函数内的短跳/改条件码 |
| 44 | MinHook `MH_CreateHook` 返回 0（成功），游戏随即 `0xC000001D` | 钩子目标不是指令边界（Just Cause 的 ASI 算 `match-0x51`，正好落在一条 6 字节 `jns` 的立即数第 4 字节），MinHook 从半条指令建跳板 | 下钩前先用反汇编器确认目标地址是合法指令开头；跨版本 exe 更要用特征码位置反推，别写死偏移 |
| 45 | `dxvk.conf` 写 `d3d9.hud = 1`，DXVK 日志“读取成功”但屏幕没有 HUD；或者 HUD 只显示 RX 580，用户看不出走的是兼容层 | HUD 是全局选项，键名是 **`dxvk.hud`**；`d3d9.hud` 不是合法键。`devinfo` 只显示显卡型号，不显示 DXVK 版本/API，不能当层证据 | 交付写 `dxvk.hud = fps`；排障取证时临时用 `version,api,fps`（或 `DXVK_HUD=version,api,fps`），截图有 `DXVK vX.Y.Z / D3D9 / FPS`，交付前改回 `fps`
| 46 | 老游戏双击后 2~3 秒“什么也没发生”就退出，等 10 秒截图抓不到任何东西 | 游戏先弹了错误对话框（窗口类 `#32770`）然后自动退出，截图间隔太长 | 120ms 轮询 `EnumWindows` 找该进程的 `#32770`，再用 `EnumChildWindows`+`GetWindowText` 读对话框文字（`Games\War3\tools\war3-dialog.ps1`）；拿不到再用迷你调试器看模块加载和退出码（`procdbg.exe`） |
| 47 | 用户说“以前能玩的东西现在不能玩”，怀疑适配工具“操作过头” | 只看单个游戏会误判：本机原生 D3D9 可能整机失效（窗口模式也建不出设备），所有 D3D9 游戏一起坏 | 先自证（游戏目录 mtime、自己的工具写过哪些目录），再用 `D3D9Probe32.exe` 裸跑（**全屏+窗口都测**）判断是单游戏还是整机共性问题；修复仍走游戏目录内绿色层，不拿驱动/显卡当结论 |
| 48 | 想把显卡驱动包从 DriverStore 备份出来离线重装，`pnputil /add-driver` 报“指定的驱动程序包缺失或无效” | 厂商 INF 里写了 `PnpLockDown = 1`，锁定安装来源，只允许从 DriverStore/官方安装器安装，改 INF 会破坏 .cat 签名 | 不要离线重装显卡驱动包：用官方完整安装包（Adrenalin）或 Windows 更新的驱动；DriverStore 备份只适合留档，不能当安装源 |
| 54 | 专用服务器黑窗口标题被改回 `Console`，cfg 里 `echo` 的中文全被吞掉 | `start "标题"` 的标题会被 hlds 覆盖；GoldSrc 控制台输出会过滤非 ASCII 字符，且 hlds 启动时会清屏（cmd 先 echo 也保不住） | 等 A2S 查询有回应（地图已加载）后：`SetWindowText` 改控制台标题，`AttachConsole(hlds pid)` + `WriteConsoleW` 直接写 Unicode 中文；不要指望 hlds 自己 echo 中文 |
| 55 | 启动参数 `+bot_quota 8` 在 hlds 上不生效，服务器 0 人 | 这版 HLDS（build 3213）启动早期不认 bot cvar；`listenserver.cfg` 只在监听服务器执行，专用服务器不跑 | 用 `+servercfgfile` 指到生成的 cfg：先 `exec server.cfg`，再 `bot_join_after_player 0` + `bot_quota N`；无机器人写 `bot_quota 0` + `bot_kick all`。用 A2S 的 players 字段验收 |
| 56 | 右栏小字发虚/彩色描边/重影，或整窗灰度后左中右都不清晰；右栏还可能叠字 | 右栏离屏 ClearType 二次合成；或混用灰度与 ClearType；或行宽超出后把 cx 重置 | 全窗口统一 TextRenderer（GDI ClearType）；右栏滚动只画视口内整行（不做裁剪/位图）；长行拆行，放不下停止该行，禁止叠画。参考 `Games\CS16`、`Games\DFBHD` |
| 57 | 以为拟物键帽需要特殊文字渲染，把全窗或右栏改成灰度/离屏，结果字发虚、重影，左右仍不一致 | 把“键帽图形样式”和“文字抗锯齿”混为一谈；右栏滚动又用了离屏位图 | 键帽只是 GDI+ 画的形状，键帽里的字仍用普通 TextRenderer（ClearType）；右栏滚动只画视口内整行，不做位图/裁剪/灰度。规则见技能铁律与绘制配方 |
| 58 | 启动器窗口/任务栏有图标，但生成的 `game_starter.exe` 在资源管理器里是白纸 | 只在运行时调 `Icon.ExtractAssociatedIcon` 设窗口图标，exe 文件本身没有图标资源 | 构建时把游戏 exe 的图标存成 `out\game_starter.ico`，再 `csc -win32icon:` 嵌入；`.ico` 不进 `src\`/游戏目录，体积 +5~10 KB 仍在 60 KB 内 |
| 59 | 单机修改器（LinGon/Themida）弹 “Sorry, this application cannot run under a Virtual Machine.”，点确定就退出 | Themida/WinLicense 反虚拟机检测把 Win11 的 VBS/内存完整性/Hyper-V 的 `HypervisorPresent` 误判为虚拟机；原 go.bat 写的 `RUNASADMIN` 还会让普通修改器无法注入 | 先移除该游戏的 XP 兼容性/RUNASADMIN（`兼容修复.bat`，带备份记录）；仍报错就换无壳修改器/Cheat Engine 表，或在用户明确同意下关闭 VBS/Hyper-V 后重启。不要改游戏 exe 或把修改器脱壳当通用方案；记录为“第三方修改器限制” |
| 60 | Future Soldier 启动 `DX9.exe` 后仍加载 `d3d11.dll`、没有 DXVK HUD | 游戏的 `Future Soldier.exe` 包装器在 Win11 上默认总选 DX11；`DX9.exe` 只是启动桩，`-no_dx11` 也不改变选择 | 只在 DX9 兼容模式的进程树设置 `__COMPAT_LAYER=WINXPSP3`，包装器才会选 DX9；不要写注册表 Layers。配 DXVK `d3d9.dll` + `dxvk.conf`，实测 HUD `DXVK v2.5.3 / D3D9 / FPS` |
| 61 | Heroes3 用 cnc-ddraw 的 auto/direct3d9/DXVK 黑屏、花屏或 `dxvk::DxvkError`；给玩家加“硬件加速”反而崩 | 1999 年 DirectDraw 游戏走 ddraw 通道，D3D9/DXVK 是多一层转换，现代驱动/高刷屏更容易踩坑 | 统一用 **GDI 软件渲染**，启动器提供 稳定全屏 / 4:3 / 窗口 1280×720 / 原版 640×480（窗口）四种预设；`maxfps=60`；不要放 d3d9.dll/dxgi.dll。参考 `Games\Heroes3` |
| 62 | 给 32 位老游戏写注册表后游戏仍不认，64 位 `reg query` 也找不到 | 32 位程序写 `HKLM\SOFTWARE` 实际落到 `WOW6432Node`；64 位 cmd/reg 默认访问 64 位视图 | 用 `reg add ... /reg:32` 或 `RegistryView.Registry32`；验证时也查 32 位视图。案例：Just Cause 的 `reg.fix.exe`（UPX + AutoIt3）写 `HKLM\SOFTWARE\Eidos\Just Cause` |
| 63 | Playnite 启动游戏时火绒弹“系统加固 → 隐藏执行 PowerShell”，平台把游戏判成异常 | Playnite 的启动动作用了 PowerShell 脚本/隐藏 `Start-Process`；发起程序是 `Playnite.exe`、目标是 `powershell.exe`，不是游戏的 `game_starter.exe` | 改用 Playnite「文件」启动动作：程序 `game_starter.exe`，参数留空（保留存档/分辨率/按键菜单）或 `/go`（零交互），工作目录为游戏目录；不要关闭火绒，也不要让启动器自动跳过菜单 |
| 64 | DXVK 下 C&C 将军/五星之光选 1280×800 全屏，游戏弹 DirectX 错误或黑屏 | 该分辨率不是当前显示器支持的显示模式；DXVK 日志 `D3D9: EnterFullscreenMode: Failed to change display mode` | 用显示器实际支持的模式（如 1920×1080）；启动器不要照搬原画质管理器的 1280×800 等非标准档位。案例：`Games\FiveStar` |
| 65 | 分发生化危机/鬼武者等 RE Engine 游戏后，对方电脑只有 240p 或分辨率被锁死，游戏内改不了 | 包里带了开发机的 `config.ini`：`FullScreenDisplayModeIndex` / `NormalWindowResolution` 是机器相关值，目标机没有对应显示模式 | 不随包发 `config.ini`；启动器首启写最小显示配置，之后每次启动只把 `[Graphics]` / `[RenderConfig]` 的 `WindowMode` 固定为 `Borderless`、`FullScreenMode=false`、`NormalWindowResolution` 写当前物理桌面；Borderless 实测忽略分辨率字段和无效索引。留 `/raw` 直接启动跳过所有预设。案例：`Games\OnimushaWotS` |
| 66 | 网吧/PnP 装机的电脑上，游戏 GUI 只有小方块/240p，开发机完全复现不了 | 网吧按 PnP 批量安装驱动，游戏可能识别不到显卡或驱动版本，回退到最低画质和最小分辨率；这时“让游戏自动检测”不可信 | 启动器默认按当前物理桌面强制 `Borderless` + 桌面分辨率，不把画面完全交给游戏自动检测；“让游戏自动检测（重置）”只作可选排障项。Borderless 下分辨率字段被忽略，输出仍按桌面铺满。案例：`Games\OnimushaWotS` |
