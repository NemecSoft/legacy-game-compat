# 实战案例

所有结论都来自实机验证（AMD RX 580 + Win11）。**该机原生 D3D9 曾在 2026-11-01 前被
“DirectX 修复工具”写的全局 `SoftwareOnly` / `EmulationOnly` 开关关掉，现已修复；D3D11/12/Vulkan
正常，但没有硬件 OpenGL（只有 GDI 1.1）**。dgVoodoo2 的 D3D11/D3D12 后端在本机实测不可用，
所以本机兼容层优先 DXVK。下面早期案例保留“当时原生 D3D9 不可用”的背景，不代表现在的首选方案。
**换机器结论可能不同，请先跑探针。**

---

## 突破点速览

| 游戏 | 卡在哪 | 突破点 |
|---|---|---|
| CS 1.6 | 没有硬件 OpenGL | Mesa Gallium 三件套走 D3D12；FPS 靠读引擎帧时间 `0x1AFB50` |
| 虐杀原形 2 | dgVoodoo 洋红 / 顶点缓冲断言崩溃 | 换 DXVK 2.3.1，两个游戏一起解决 |
| 死亡阴影 | `D3DERR_NOTAVAILABLE`；DXVK 2.3.1 白屏 | DXVK **2.5.3**；白屏无 `ResetSwapChain` 就是卡在首次 Present 前 |
| DFBHD 1.5 | `Resolution test has failed`；单机无入口 | DXVK 的 `d3d8.dll`；单机必须走汉化外挂（`DFBHDLC` 是 LAN 客户端） |
| DFBHD 1.2 | 同 D3D8 失败 | 同方案；这台目录自带中文，直接阻塞启动 `dfbhd.exe` |
| 暴力摩托 Road Rash 2002 | 原生 DirectDraw 时好时坏，弹“未安装 DirectX/版本太低” | **cnc-ddraw 7.1**；auto + DXVK 走硬件，GDI 回退；1080p 是输出放大 |
| 正当防卫 Just Cause 1（汉化版） | 原生 D3D9 顶点着色器失败；汉化 ASI 5–10 秒 `0xC000001D`、汉字全方块 | DXVK 2.5.3；把汉化 ASI 裁成**只留 25 字节 UTF-8 解码补丁**的 `JCTextFix.asi` |
| 魔兽争霸III（War3） | 原生 D3D9 连窗口设备都建不出（`0x8876086A`），双击 2.6 秒静默退出，弹“不能初始化DirectX” | 根因是“DirectX 修复工具”写入的 `Direct3D\Drivers\SoftwareOnly=1`（64 位 + WOW6432Node 两个视图）和 `DirectDraw\EmulationOnly=1`（64 位）；清除后原生 D3D9 恢复，War3 原生 1920×1080 进主菜单 |

---

## 0. 宿主环境先例：三条原生路径全挂的机器

- `Win32_VideoController` 有三个适配器：AMD RX 580、向日葵虚拟显示器（OrayIdd）、GameViewer 虚拟显示器。
- 探针：adapter0 = `0x8876086A`（NOTAVAILABLE），adapter1 = `0x8876086C`（INVALIDCALL），全屏/窗口都一样。
- dgVoodoo2：D3D11 后端 `INVALIDCALL`；D3D12 后端同样失败，游戏直接退出。
- DXVK：`0x00000000` 成功，`Device properties: AMD Radeon RX 580` 正常出现。
- 结论：这台机只能用 Vulkan 路线；**虚拟显示器适配器是头号嫌疑人**。

---

## 1. Counter-Strike 1.6 —— 没有硬件 OpenGL

- 症状：原生 `opengl32.dll` 只提供 GL 1.1 / GDI Generic。
- 方案：**Mesa Gallium**——`opengl32.dll` + `libgallium_wgl.dll` + `dxil.dll` 放游戏目录，走 D3D12。
- 附带工作：
  - 实时 FPS 引擎不暴露（`fps_max` 无效、`net_graph` 不渲染），自写覆盖层读引擎帧时间
    （float 秒值固定低堆 `0x1AFB50`，副本 +0x2C，FPS = 1/值，开局约 4.5s 后有效）。
  - `Process.Modules` 看不到 WOW64 模块，需 64 位进程 + `CreateToolhelp32Snapshot(TH32CS_SNAPMODULE|TH32CS_SNAPMODULE32)`。
  - zbot 自动注入走 `listenserver.cfg`；`bot_quota N` 是"维持 N 个"，换图会重置。
- **2026-11-01 补齐 GUI/启动器交付**：`Games\CS16`（GUI `game_starter.exe` + 菜单 `game_starter.bat` +
  兼容修复/恢复 + `_mesa` 备份）；5 个常用分辨率（1920×1080 全屏/窗口、1600×900、1280×720、1024×768）；
  `/go` 实测进 1920×1080「选择队伍」，启动器陪跑到游戏退出（`Games\CS16\evidence\cs16-native.png`）；
  GUI/bat 含小键盘一键买枪说明（GUI 按键用拟物键帽）。
- **专用服务器（同一天补齐）**：GUI 中栏上下分组——游戏端（分辨率 + 启动游戏）、中间日志/退出、
  服务端（服务器配置 + 启动专用服务器）；两处配置都用下拉框，右栏注明“一般直接用游戏端建房，
  专用服务器是独立开服”。5 个常用配置（混战 16人 de_dust2 8机器人 / 比赛 10人 de_inferno 无机器人 /
  练枪 16人 awp_map 12机器人 / 娱乐 16人 fy_iceworld 8机器人 / 随机图 16人 8机器人），
  GUI 下拉框与 bat 菜单 8 一一对应；`game_starter.exe /server N` 零交互。
  机器人必须在 `+servercfgfile` 指向的生成 cfg 里设置（命令行 `+bot_quota` 这版 hlds 不生效），
  A2S 实测 `players/map/max` 与配置一致。hlds 会过滤控制台 echo 的非 ASCII 并把标题改回 `Console`，
  所以等 A2S 就绪后由启动器 `SetWindowText` + `AttachConsole`/`WriteConsoleW` 写中文标题与提示
  （`evidence\starter-cs16-groups-note.png`、`starter-cs16-server-banner.png`）。

## 2. Prototype 2 —— dgVoodoo 洋红 / 断言崩溃

- dgVoodoo2 2.87（D3D11 输出）：UI 面板约 41%、过场视频 78~96% 洋红；换 D3D12 归零但 D3D11warp 仍洋红。
- P2 的 pure3d 引擎在 dgVoodoo 下 `CreateVertexBuffer` 返回 NULL → 断言 `mVBO != 0` → `0xC0000005`。
- 方案：**DXVK 2.3.1**（单个 `d3d9.dll`）→ 洋红 0%，两个游戏都干净跑完。
- 本机注意：AMD 驱动缺 `VK_KHR_load_store_op_none`，DXVK 3.x 报 `No adapters found`，锁 2.3.1。

## 3. The Saboteur（死亡阴影）—— 显卡初始化失败

- 现场：目录里被人装过 **ReShade 4.9.1**（`d3d9.dll`），其 `d3d9.log` 直接给出：
  `IDirect3D9::CreateDevice failed with error code D3DERR_NOTAVAILABLE`
  （请求 1920x1080 全屏 X8R8G8B8，DepthFormat=75 D24S8，RefreshRate=60）。
- 探针确认原生 D3D9 全挂；dgVoodoo D3D11 失败、D3D12 退出。
- DXVK **2.3.1 → 白屏**（日志里始终没有 `ResetSwapChain`，卡在建设备之后）；
  **2.5.3 → 正常**（左上 HUD `DXVK v2.5.3 / D3D9 / FPS 59`）。
- 交付：`d3d9.dll`（DXVK 2.5.3）+ `_dxvk\` 留底 + `兼容修复.bat` / `恢复原版.bat`，
  原 ReShade 改名 `d3d9.dll.reshade-bak`。
- **2026-11-02 标准化交付**：`Games\TheSaboteur`（GUI `game_starter.exe` + 菜单 `game_starter.bat` +
  `launcher.ini`）。5 档分辨率（1920×1080 默认 / 1600×900 / 1280×720 / 1024×768 / 800×600）由启动器
  写我的文档 `My Games\The Saboteur™\SaveGames\config.ini` 的 `ScreenWidth/ScreenHeight`；`/setres N`
  实测 1280×720 → 1920×1080 可逆，退出码 0。游戏是启动器直接子进程，陪跑并回传退出码。
  exe 已嵌入 `Saboteur.exe` 图标（32.5 KB）；交付 `dxvk.conf` 保留
  `dxvk.hud = version,api,fps` 作为层挂载标志。
  证据：`Games\TheSaboteur\evidence\saboteur-first.png`（`DXVK v2.5.3 / D3D9 / 59.4 FPS`）、
  `saboteur-menu.png`（中文游戏内）、`saboteur-launcher.png`。
- **跨机器交付包**：`Games\TheSaboteur\release\`（`一键应用.bat` / `一键撤回.bat` + payload + manifest）。
  应用到目标目录前先备份同名文件到 `_legacy_compat_backup\TheSaboteur\` 并写 `交付记录.txt`；
  撤回恢复备份、删除未被修改的交付文件，玩家改过的文件跳过并记录。假目标 + 修改文件撤回测试全 PASS，
  记录见 `Games\TheSaboteur\evidence\delivery-*.txt`。

## 4. Delta Force: Black Hawk Down（1.5 + 汉化外挂）

- 症状：`The resolution test has failed. Please make sure that you have the latest drivers...`
- 导入：`DFBHDLC.EXE` 依赖 `d3d8.dll`。
- 方案：**DXVK 2.5.3 `d3d8.dll` + `d3d9.dll`** → 分辨率测试四项全 PASS
  （`DirectX8 / Hardware T&L / Multitexturing / Shaders` 全部 Detected）。
- **单机玩不了的原因不是图形**：`DFBHDLC.EXE` 是**局域网客户端**；单机要走 `汉化外挂.exe`
  （拉起 `dfbhd.dat`，注入 `LG_DF_BHD_TS.dll` 字体扩展 + `localchn.pff/ecn1.pff` 中文资源）。
- 汉化外挂的启动行为随版本不同：本目录 1.5 版会陪跑到游戏退出（是 `dfbhd.dat` 的父进程）；
  无论哪种，都要按进程名等 `dfbhd.dat`，不能只看外挂自己的退出。
- 分辨率：`dfv.cfg` 的 `game_res` 档位表见 `resolution.md`，8 = 1600x1200 全屏铺满 1080p。
- **2026-11-01 标准化交付**：`Games\DFBHD`（GUI `game_starter.exe` + 菜单 `game_starter.bat` +
  `兼容修复.bat` / `恢复原版.bat` + `_dxvk` 留底）；5 档分辨率实测
  （1600×1200 默认 / 1280×1024 / 1024×768 / 800×600 / 640×480，`windowed=1` 仍改桌面分辨率，不做选项）。
  启动器写 `dfv.cfg` 后启动 `汉化外挂.exe`，按进程名找到 `dfbhd.dat` 并回传**游戏退出码**；
  GUI 与 bat `/go` 均实测退出码 0、桌面恢复 1920×1080。
  **注意本目录这版外挂是陪跑到游戏退出的**（`dfbhd.dat` 的父进程），不能只按“外挂拉起就退出”处理；
  游戏不是启动器的直接子进程，是外挂的子进程（平台托管限制，已记录在技能）。
  实施阶段用 DXVK HUD 取证：D3D8 / ~59.9 FPS（早期截图带 `devinfo` 只显示 RX 580；
  `devinfo` 不证明兼容层，后续统一用 `version,api,fps`）；
  交付 `dxvk.conf` 保留 HUD=version,api,fps 作为层挂载标志；旧 no-hud 截图只作历史。
  证据：`Games\DFBHD\evidence\dfbhd-hud-1600x1200.png`、
  `dfbhd-game-1600x1200-no-hud.png`、`dfbhd-launcher.png`。

## 5. Delta Force: Black Hawk Down（1.2 单机完整版）

- 同样是 D3D8 设备失败 → 同样上 DXVK 2.5.3 `d3d8.dll`。
- 该目录资源自带中文，`game_starter.bat` 直接阻塞调用 `dfbhd.exe` 即可（无外挂）。
- 1.2 与 1.5 的 `game_res` 档位表一致；`hw3d_res1600x1200` 默认是 0，必须先打开。

## 6. 暴力摩托 Road Rash 2002 —— DirectDraw 时好时坏

- 游戏：`D:\YunGame\Race\RoadRash`，32 位 `Roadrash.exe`，2002 中文版 / 2004.9 更新。
- 症状：**有时能跑、有时不能**；不能时弹
  `系统检测到你尚未安装 DirectX 驱动或版本太低……`（游戏自己的 DirectDraw 初始化失败提示），
  或者窗口出现但画面不呈现、8~11 秒自行退出。
- 接口：导入表只有 `DDRAW.dll -> DirectDrawCreate`（没有 d3d8/d3d9/opengl32）。
  **不是 D3D9 游戏，DXVK 直替不适用**；也不是缺 DX 运行时，装 DX9 运行时无效。
- 方案：**cnc-ddraw 7.1** 的 `ddraw.dll`，自带 `[RoadRash]` 配置。
  - 640×480 原生全屏：能跑；前 15 秒有 cnc-ddraw 的软件渲染警告。
  - 1080p 输出：`width=1920 height=1080 fullscreen=true windowed=false maintas=true`，
    内部仍 640×480，画面 1440×1080 + 左右黑边。GDI 双线性柔和；DXVK bicubic 更锐利。
  - 默认 `renderer=auto`：本机有 Vulkan → cnc-ddraw 的 D3D9 渲染器加载 DXVK 2.5.3（硬件，60FPS）；
    无 Vulkan 自动回退 GDI。另留 `renderer=gdi` 的纯软件启动、640×480 原版启动。
- 水印验证：软件 = GDI 回退警告；硬件 = `dxvk.hud = version,api,fps`（`DXVK v2.5.3 / D3D9 / 60FPS`）。
  交付 `dxvk.conf` 保留该 HUD 作为层挂载标志；GDI 软件警告只用于测试。测试截图存 `Games\RoadRash\evidence\`。
- 注意：DXVK + 640×480 组合在本机开场 8~16 秒黑屏（20 秒后进比赛）；1080p + DXVK 正常。
- 启动器：`game_starter.bat` + `game_starter.exe`（中国红，同逻辑），另有 `game_starter-soft.bat` / `game_starter-640.bat`。
- 键位：注册表 `HKLM\...\RoadRash 95\Controls`（22 字节），11 个 WORD 低字节恒 0、
  高字节为键码；槽序 = 前进/刹车/左转/左倾/加速/拳打/脚踢/重击/下车/右转/右倾。
  **VK→键码映射表实测自运行中的进程内存 `0x4c5d90`**：`Shift=0x90 Ctrl=0x91 Alt=0x92`、
  `←0x93 ↑0x94 →0x95 ↓0x96`、`PageUp0x81 End0x82 PgDn0x83 Home0x84 Insert0x85`、字母=ASCII 小写。
  三套方案（`Controls` 实测值）：
  - 修改版按键（默认，取自 W 盘未修改目录）`0094009200930084007800610073007A006400950081` = ↑ / **Alt** / ←→ / Home·PageUp / x / a / s / z / d
  - 拳皇式 WASD `00770073006100710090006A0075006B007800640065` = W/S / A·D / Q·E / **Shift** / J / U / K / X
  - 系统默认（游戏内“使用键盘默认设置”后读回）`0094009600930084006E0085000D0020006400950081` = ↑ / ↓ / ←→ / Home·PageUp / n / Insert / Enter / Space / d
  选择做进启动器（默认第 1 套，记住上次选择），落地统一走 `game_starter.exe /keymap N`（早期是 `_tools\keymap.exe`，已合并）：
  值相同不动、不同只改写 `Controls`、缺值才整份导入、需要时自提权。
  坑：不要按“方向键对称”猜特殊码——曾把刹车 `0x92`(Alt) 猜成 ↓/W 塞进前进槽，
  游戏界面直接显示前进=Alt。特殊键码只信原版 reg 或进程内存里的映射表。
- 启动器 GUI：game_starter.exe 按仓库技能 `.qoder\skills\legacy-launcher-gui` 重做为纯 GDI+ 自绘
  三栏界面（左介绍 / 中选项+启动 / 右键帽操作表），零图片、**37 KB**；中栏四个下拉框（画面 / 按键 / 存档 / 自动载入级别）；右栏键帽从所选 reg 的
  `Controls` 反查渲染，切方案立刻重画。`build.ps1` 会跑体积/资源检查，超标即构建失败。
- **存档与自动载入**（`.rrs`，160 字节定长）：`0x00-0x03` 是覆盖 `0x04..0x9F` 的**校验和**，
  `0x04` 魔数 `AD DE FE CA`，`0x10` 金钱(LE)，`0x14` 玩家名(GBK，界面显示成「(代表 X)」)。
  游戏自带命令行载入：`Roadrash.exe <存档文件>`，**参数按空格切**，所以存档名里不能有空白
  （`存档-Milwaukee Jon(荘臣)-5级.rrs` 会被丢掉并退回新游戏；`存档-荘臣-5级.rrs` 正常）。
  校验函数在 `0x42EFC0`：`edi+=b`、`esi+=rem*b`、`eax ± b`（rem 奇偶定号），返回
  `((eax<<16)|(edi&0xFFFF))^esi`；改任何字段都必须回写它，否则游戏**静默拒载**（不报“校验失败”，
  只表现为“没载入”）。公式对 47 个游戏自带存档 47/47 吻合，工具 `Games\RoadRash	oolsix_saves.py`。
- 完整测试矩阵、证据和回滚见 `Games\RoadRash\适配过程分析.md`。

---

- **2026-11-01 GUI 整理**：统一为 900×620 三栏、规范红渐变、四组下拉（画面/按键/存档/载入级别）；
  右栏改成视口整行滚动（不用位图/裁剪），秘技全文保留，V/F10/小键盘 +/- 用键帽。
  证据 `Games\RoadRash\evidence
oadrash-launcher.png`、`roadrash-launcher-scrolled.png`。

## 7. 正当防卫 Just Cause 1（简体中文汉化版）—— 汉化 ASI 崩溃 + 方块字

- 游戏：`D:\YunGame\Z\JustCause1`，32 位 `JustCause.exe`（2006 Eidos/Avalanche，Steam 补丁 81984），
  导入 `d3d9.dll` + `dinput8.dll`；汉化走「梅毛补丁」：替换 `Archives\pc3.arc` / `pc4.arc` / `pc.tab`，
  加 `dinput8.dll`（ASI Loader）和 `scripts\*.asi`（含 `JCSubtitleOverlay.asi`）。
- 症状 1（图形）：原生启动弹 DirectX 顶点着色器错误；探针 adapter0 `0x8876086A` —— 本机原生 D3D9 不通，
  与宿主环境先例一致。→ DXVK 2.5.3 `d3d9.dll` 一次过，`JustCause_d3d9.log` 正常，1080p 全屏，实测 230 FPS。
- 症状 2（文字）：所有汉字显示成方块（菜单 / HUD / 字幕 / 加载提示全是 `□□□`）。
- 症状 3（闪退）：带汉化 ASI 启动 5–10 秒必崩，退出码 `0xC000001D`（非法指令）。
- 根因（Ghidra 12 反编译 `JCSubtitleOverlay.asi`）：
  1. 游戏 exe 的 3 字节 UTF-8 解码器在 `0x14A727C`：`and eax,0xF000` 让所有三字节序列固定映射到
     `U+E000+`（私用区），汉化字库里没有这些字形 → **方块**；
  2. ASI 的工作线程先 `Direct3DCreate9(0x20)` 替换 vtable+0x40（CreateDevice）装自己的 Present/字幕层，
     再扫主模块 25 字节特征码把上述解码器改成正确的 `(b1&0x0F)<<12 | (b2&0x3F)<<6 | b3`；
  3. 最后 `MH_CreateHook(match-0x51, …)`：`match-0x51 = 0x14A722B` 落在一条 6 字节 `jns` 的立即数里
     （不是指令边界），MinHook 在半个指令上建跳板 → 游戏跑到文本解码就 `0xC000001D`。
- 方案：`Games\JustCause\src\build-textfix.py` 从原版 ASI 上打两处 5 字节补丁生成 `JCTextFix.asi`：
  - `0x349B`：`85 C0 74 19 B9` → `E9 B4 00 00 00`，跳过整个 D3D9 vtable hook 块；
  - `0x3809`：`call MH_CreateHook` → `mov eax,1`，跳过非法钩子；
  - 25 字节解码补丁原样保留。两处都不在 PE 重定位项上（第一版写在 0x34B8，被 loader 改了 rel32，
    启动即 `0xC0000005`，详见 pitfalls #43）。
- 结果：原生菜单 / HUD / 任务提示 / 字幕全部中文（不再需要 ASI 自绘字幕层），跑 150 秒稳定；
  DXVK HUD `FPS: 230.9` 证明渲染路径。
- 交付：DXVK 2.5.3 + `_dxvk\` 留底、`scripts\JCTextFix.asi`（原版 ASI 存 `_backup\`）、
  `兼容修复.bat` / `恢复原版.bat`、`game_starter.exe`（27.5 KB，中国红渐变三栏，HUD 开关）+
  `game_starter.bat`（控制台菜单版，与 exe 同选项/同默认值，自己启动游戏，`/go` 零交互，
  `_tools\nircmd` 负责控制台窗口居中）+ `launcher.ini`。
  汉化补丁本体（Archives / dinput8.dll / 其它 asi）不动。托管契约：假游戏 5/5 PASS，
  真游戏 `/go` 正常退出回传 `0x00000000`、强杀回传 `0xFFFFFFFF`；bat 菜单 6/6 PASS。
- 证据：`Games\JustCause\evidence\`（`text3-07` 加载界面、`run-06` 字幕、`run-07` 任务提示、
  `menu2-04` 主菜单、`menu-esc` HUD、`hud-06`/`hud-crop` HUD 帧数、`starter-ui` 启动器）。
- 完整过程与测试矩阵：`Games\JustCause\适配过程分析.md`。

---

## 8. 魔兽争霸III（War3）—— “以前能玩，突然不能玩”，其实是整机 D3D9 失效

- 游戏：`D:\Games\War3`，XDGAME 硬盘版（`War3.exe` 1.26.0.6401 / `Game.dll` 1.27 / `Warcraft III.exe` 引导壳），
  目录自 2021 年起没有任何文件改动。
- 症状：双击无窗口，约 **2.6 秒干净退出（exit 0）**；等 10 秒截图什么都看不到。
- 定位（全部实测）：
  - `Games\War3\tools\procdbg.exe`（自带迷你调试器）显示：加载 `SysWOW64\d3d9.dll` + AMD UMD → 2.65s EXIT 0，无异常；
  - 120ms 轮询窗口抓到 `#32770` 对话框：**“魔兽争霸III不能初始化DirectX……”**；
  - 摘掉 AppCompat 的 `DWM8And16BitMitigation`（备份后还原）无效；`-window` / `-swtnl` 无效；
  - `D3D9Probe32.exe` 裸跑：adapter0 全屏 **和窗口** 都 `0x8876086A`，adapter1 `0x8876086C`
    → **不是 War3 单独的问题，是整机原生 D3D9 建不出设备**（与 Just Cause 同根因）。
- 修复：**只在 War3 目录里**放 DXVK 2.5.3 `d3d9.dll` + `dxvk.conf`，留 `_dxvk\`；
  实测 1920×1080 正常进“游戏设定”界面（`evidence\war3-dxvk.png`）；`兼容修复.bat` / `恢复原版.bat` 往返可用。
- 经验：用户说“突然不能玩”时，先自证没动过它（目录 mtime、工具只写过别的目录），
  再拿游戏自己的错误原文和探针结果判断是单游戏还是整机共性问题；别用“驱动/显卡”当结论，
  用游戏目录内的绿色层直接把问题绕过去。
- 完整过程：`Games\War3\适配过程分析.md`。

### 最终结论（2026-11-01 修好，原生 D3D9，不再需要 DXVK）

- **真正根因**：注册表 `HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly=1`
  （**64 位和 WOW6432Node 两个视图都是 1**），另有 `HKLM\SOFTWARE\Microsoft\DirectDraw\EmulationOnly=1`（64 位）。
  这是“DirectX 修复/优化”类工具写入的**全局强制软件渲染开关**，直接把 D3D9 的 HAL 设备关死；
  **D3D9On12 也受影响**，而 D3D11/12/Vulkan 完全正常——所以换驱动、DDU、重启都不会好。
- **证据**：Procmon 抓到 `d3d9.dll` 在返回 `0x8876086A` 前读取
  `...\Direct3D\Drivers\SoftwareOnly`，结果为 `REG_DWORD 1`；删除后同一探针全部 `0x00000000`。
- **修复**：备份后删除这两个值（两个视图都删），**不需要重启**；`scripts\修复本机D3D9.bat` 一键完成。
- **验证**：`D3D9Probe32/64` 全 `0x00000000`；`D:\Games\War3` 撤掉 DXVK 并恢复原版后，
  原生 D3D9 直接进 1920×1080 主菜单（`evidence\war3-main-fixed.png`）；
  干净副本同样正常（`evidence\war3-native-fixed.png`）。
- 之前的 DXVK 方案保留在 `_dxvk\` 作为其他机器的备选；本机已不需要。
  **驱动换版/DDU/重启都是无效排查——根因不在驱动。**
- **教训**：**D3D9 全挂但 D3D11/12/Vulkan 正常**时，先查
  `Direct3D\Drivers\SoftwareOnly` / `DirectDraw\EmulationOnly`（两个视图），再谈驱动/兼容层。

### 排查复盘（为什么绕了一晚上）

| 阶段 | 当时的判断 | 真相 | 教训 |
|---|---|---|---|
| 探针显示 `S_OK 0×0` | 显示模式映射坏了 | `printf` 求值顺序假象；模式一直是 1920×1080 | 先调用、再单独打印 |
| C# 探针 `adapters: 2` | 有两个适配器 | vtable 索引 1 是 `AddRef`；`GetAdapterCount` 是 4 | 核对 COM vtable |
| 换驱动 / DDU / 重启 | 驱动坏了 | 26.5.2 本来就是能玩的版本；开关在驱动外 | 先查全局开关 |
| 虚拟显示器 / 远程软件 / WSL / AMD 软件 / 刷新率 / KMD 值 | 逐个像嫌疑人 | 全部排除 | 用对照实验，不要靠猜 |
| `D3D9On12` 也失败 | AMD UMD 坏了 | D3D9 运行时被强制软件 | 9on12 同挂 = 运行时级开关 |
| Procmon | — | 抓到 `Direct3D\Drivers\SoftwareOnly=1` | 抓注册表读，一锤定音 |

**最关键的外部约束**：用户明确要求**不重装、不装更新、不拿驱动/显卡当结论**。
如果允许重装，开关会被“碰巧”清掉，但根因永远学不到；正是这三条把方向从“换驱动”逼回注册表和运行时。
完整复盘见 `references/d3d9-global-failure.md`。

---

## 通用交付形态

```
<游戏目录>
  ddraw.dll / d3d9.dll      生效中的翻译层
  ddraw*.ini / dxvk.conf
  _cnc-ddraw\               留底（重装用）
  兼容修复.bat              从 _cnc-ddraw 复制上来
  恢复原版.bat              删除翻译层
  game_starter.bat / game_starter.exe           默认启动器（两个逻辑一致，中国红）
  launcher.ini
  game_starter-soft.bat / game_starter-640.bat  可选多兼容启动方式
```

源文件、工具、证据、`build.ps1`、`适配过程分析.md` 都在仓库 `Games\<游戏>\`；
游戏目录不放 `.cs` / `.ps1`。

## 启动器约定（用户偏好）

- bat：`@echo off&color 4f`（中国红）+ `set "tl=..."` + `title %tl%` + `mode con:`
- exe：中国红 `#C0121E` + 金色 `#FFD85E`，不要黑底
- 默认 `game_starter.bat` + `game_starter.exe` 两个都要，逻辑一致；可另加 `game_starter-soft.bat` / `game_starter-640.bat`
- 纯 `echo.` 键位/说明表，不用 `:标签` / `goto`；`pause>nul` 只作为“按键启动”闸门
- 最后一行 `"%~dp0游戏.exe" >nul 2>&1`（阻塞），游戏退出窗口自动关；cmd 只认 `>nul`
- 需要外挂启动器时：`start "" "%~dp0外挂.exe"` + 进程等待（见 templates）

## 10. 虐杀原形 1 / 2 —— 启动器交付（2026-11-02）

- P1：`D:\YunGame\Prototype\Prototype`，`prototypef.exe`；go.bat 链为 `init.exe` → `prototypef.exe`，
  必须带 `0x3ff` 亲和性（不设亲和性直接启动曾崩在 `prototypeenginef.dll`，`0xc0000005` / `0x671dbd`）。
  HUD 实测 `DXVK v2.3.1 / D3D9 / 59.9 FPS`（`Games\Prototype1\evidence\prototype1-hud.png`；
  临时 `DXVK_HUD=version,api,fps`，不显示显卡型号）。
- P2：`D:\YunGame\Prototype\Prototype.2`（实际目录带点），`prototype2.exe`；go.bat 的有效链是直接启动游戏
  + `0x3ff` 亲和性（其中 `./init.exe` 在 cmd 报错，但游戏不依赖它）。用户确认 go.bat 正常，启动器不改它。
- 启动器：`Games\Prototype1` / `Games\Prototype2`，900×620 三栏、规范红渐变、正常 TextRenderer；
  P1 先跑 init，再以 `CREATE_SUSPENDED + SetProcessAffinityMask(0x3ff) + ResumeThread` 启动游戏；
  P2 直接启动；都不传分辨率/窗口参数，不覆盖分辨率/画质/存档。`game_starter.bat` 同菜单（启动/日志/退出）。
- 兼容层/回滚：`_dxvk` 留底，`兼容修复.bat` / `恢复原版.bat`；游戏、汉化、go.bat 均保留。
- 两个启动器 exe 均已嵌入游戏图标（构建时从游戏 exe 抽 `.ico` + `-win32icon`，与游戏图标一致），
  体积 31.5 KB；`devinfo` 不再用于 HUD 取证。
- 注意：黑边/宽高比/人物比例不是启动器参数问题，启动器严格不加参数；请在游戏内「选项」里设置。
  之前测试中出现过不同结果，是测试启动方式差异，不作为交付链。
- 证据：`Games\Prototype1\evidence\prototype1-launcher.png`、`prototype1-hud.png`；
  `Games\Prototype2\evidence\prototype2-launcher.png`。
- 启动器实测：P1 `/go` → init.exe → prototypef 的父进程是启动器、亲和性 0x3FF；
  关闭游戏后启动器退出码 0、桌面恢复 1920×1080。P2 使用同一套 CreateProcess/亲和性/退出码逻辑。

## 11. 幽灵行动：未来战士（v1.8 全 DLC）—— XP 兼容性与修改器

- 路径：`D:\YunGame\V\Future Soldier`；DX11 主程序 `Future Soldier DX11.exe`，
  包装器 `Future Soldier.exe`；`init.exe` 退出码 10 是正常初始化。
- `go.bat` 会写 `HKCU\...\AppCompatFlags\Layers` 的两个值：
  `~ DWM8And16BitMitigation WINXPSP3 RUNASADMIN`（DX9/DX11 都有）。这会强制管理员权限，
  普通修改器无法注入；已备份并只删除这两项（`Games\FutureSoldier\backup\`）。
  启动器不调用 go.bat，也不写兼容性；`兼容修复.bat` 可重复清理。
- 移除 XP 兼容性后，**DX11 原生可玩**（RX 580 / Win11，1920×1080），不需要 DXVK/DX9。
- 启动链：`init.exe` → `Future Soldier.exe "Future Soldier DX11.exe"`；
  实际游戏进程是 `Future Soldier DX11.exe`。启动器等待实际进程并取它的退出码，
  不是只拿包装器的 0；游戏是孙进程（游戏自身分层）。
- 原生 DX11 没有 DXVK 层，不显示 DXVK HUD；`/DisplayFPS 1` 参数实测转发到实际游戏但画面未出现 FPS，
  不作为层证据。其他 DXVK 交付统一保留 `version,api,fps`。
- 修改器：LinGon v1.8 DX9/DX11 都是 Themida/WinLicense 加壳，带反虚拟机检测；本机
  `HypervisorPresent=True` / VBS 运行中，启动即报
  `Sorry, this application cannot run under a Virtual Machine.`，点确定退出。
  两个修改器表现相同，不是版本/DX 选错。已记入 `Games\FutureSoldier\evidence\trainer-vm-error.txt`；
  未关闭 VBS/Hyper-V、未脱壳第三方修改器。替代：无壳修改器 / Cheat Engine 表，或用户自行
  关闭内存完整性/VBS 后重启再试。
- 交付：`Games\FutureSoldier\release\`（`一键应用.bat` / `一键撤回.bat` + payload + manifest），
  应用会调用 `兼容修复.bat /quiet` 清 XP 兼容性并写记录；撤回保留被玩家改过的文件并记录跳过。
- 证据：`Games\FutureSoldier\evidence\future-soldier-dx11.png`、
  `future-soldier-launcher-game.png`、`future-soldier-bat-game.png`、
  `future-soldier-launcher.png`、`launcher-log.txt`。
