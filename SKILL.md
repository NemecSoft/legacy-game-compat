---
name: legacy-game-compat
description: 诊断并修复老游戏在新系统/新显卡上的崩溃、黑屏、白屏、显卡初始化失败、分辨率不对（D3D8/D3D9/D3D11/DirectDraw/OpenGL）。用绿色翻译层（DXVK / dgVoodoo2 / cnc-ddraw / Mesa）在游戏目录内落地，带备份与一键回滚。触发词：老游戏、黑屏、白屏、闪退、显卡初始化失败、Resolution test has failed、D3DERR、DXVK、dgVoodoo、兼容层、强制分辨率、4:3、宽屏。
---

# 老游戏兼容适配（绿色方案）

目标：**不改系统、不装驱动、不动注册表**，把兼容文件放进游戏目录，双击就走；出问题能一键回滚。

## 铁律

1. **先测，别猜**。先用探针确认原生的 D3D9/D3D11 到底能不能建设备，再决定用哪层（`scripts/D3D9Probe.cs`）。
2. **一切落地在游戏目录**：`d3d8.dll` / `d3d9.dll` / `dxvk.conf` + `_dxvk\` 留底。
3. **每次改动前备份**，并交付 `兼容修复.bat` 与 `恢复原版.bat`。
4. **验证要有证据**：截图 / 游戏自带的分辨率测试 / DXVK HUD，不能只看"进程没退"。
5. **一个游戏一个目录**，不要做"通用安装器"去改系统全局。
6. **每个游戏一个工作区**：源文件、工具、截图、适配分析都放仓库 `Games\<游戏>\`；游戏目录只放成品，
   **不放 `.cs` / `.ps1` 源文件**。bat 源码 UTF-8 保存，落地转 GBK+CRLF；exe 源码在 `Games` 编译后复制过去。
7. **交付默认只带 GUI 启动器 `game_starter.exe`**（自检补层、恢复默认配置、按键方案、陪跑等退出）。
   它必须满足**平台托管契约**（玩家的启动平台靠进程树+退出码监控游戏，bat 原来天然满足）：
   游戏是直接子进程、启动器陪跑到游戏退出、**原样回传游戏退出码**、关启动器窗口不杀游戏、
   `game_starter.exe /go` 作零交互启动命令。界面与契约见技能 `.qoder\skills\legacy-launcher-gui\SKILL.md`。
   `game_starter.bat` 只在玩家要控制台时保留，若同交付必须与 exe 同选项同默认值。
   注册表导入**幂等**：只在关键设置缺失时导入，别每次启动覆盖玩家在游戏内的改键；
   多套按键/设置用启动器里的选择或单独的 `切键-*.bat`（落地走同一个只改写单值的工具）。
8. **启动器用中国红**：bat 用 `color 4f`；exe 背景 `#C0121E` + 金色 `#FFD85E`，不要黑底。
   game_starter.exe 的界面（令牌、三栏布局、键帽画法、体积红线）按技能 `.qoder\skills\legacy-launcher-gui` 做。
9. **测试时开水印，交付时关掉**：用 cnc-ddraw 的 GDI 软件警告 / DXVK HUD / dgVoodoo 水印证明
   翻译层确实生效、以及走的是硬件还是软件；证据存 `Games\<游戏>\evidence\`，交付配置不得带 HUD/水印。
10. **分辨率要诚实**：先确认引擎内部渲染分辨率；DirectDraw 老游戏多为固定 640×480，1080p 只是
    cnc-ddraw 输出放大（`width/height + fullscreen + maintas`）。必须实测 640 与 1080 并记录差异，
    不能写成"原生 1080p"。

11. **整机 D3D9 全挂先清全局开关**：所有 D3D9 游戏同时挂、D3D11/12/Vulkan 正常时，先跑
    `scripts\修复本机D3D9.bat`（删 `Direct3D\Drivers\SoftwareOnly` / `DirectDraw\EmulationOnly`，
    64 位 + WOW6432Node 两个视图）再复测；**不要先重装、换驱动、装更新或上 DXVK**。
    完整复盘见 `references/d3d9-global-failure.md`。

## 工作流（推荐入口）

一条命令跑完 **探测 → 选层 → 落地 → 验证 → 报告**：

```bat
workflow\adapt.bat -GameDir "D:\Games\某游戏" -Title "游戏中文名"
workflow\adapt.bat -GameDir "D:\Games\某游戏" -DryRun
```

参数与阶段说明见 `workflow/workflow.md`。手工处理时按下面的步骤走。

## 每个游戏的工作区（Games）

仓库根下 `Games\<游戏名>\` 是该游戏唯一的适配工作区，和游戏目录分离：

```
Games\<游戏名>\
  src\             game_starter.bat / 兼容修复.bat / 恢复原版.bat / Launcher.cs / launcher.ini / ddraw*.ini
  tools\           本次用到的层、探针脚本、测试脚本
  evidence\        水印、最终画面、日志等客观证据
  backup\          用户原文件备份（如 go.bat.原版）
  build.ps1        编译 exe、bat 转 GBK+CRLF、复制成品到游戏目录
  适配过程分析.md   症状、测试矩阵、结论、回滚
```

- **游戏目录只收成品**：`ddraw.dll` / `d3d9.dll` / `ddraw*.ini` / `game_starter.bat` / `game_starter.exe` / `launcher.ini`
  / `兼容修复.bat` / `恢复原版.bat` / `_cnc-ddraw\` / `_tools\`。
- 目标目录出现 `.cs` 就是违规；`build.ps1` 要把源码留在 `Games`，只复制编译/转码后的文件。
- 一个游戏可以有多套启动方式（默认 / 纯软件 / 原版分辨率），但每套的配置放 `Games\src`，
  由构建脚本复制成 `ddraw-soft.ini` / `ddraw-640.ini` 等；启动器切换配置后要在退出时恢复默认。

## 第 0 步：定位（必做）

> **先查全局开关（30 秒，优先于一切）**：如果**所有** D3D9 游戏同时挂，且 D3D11/12/Vulkan 正常，先跑
> `scripts\修复本机D3D9.bat`（清除 `Direct3D\Drivers\SoftwareOnly` 和 `DirectDraw\EmulationOnly`，
> 64 位 + WOW6432Node 两个视图），再跑探针。**不要先换驱动/上 DXVK**——这两个开关重装驱动也不会清。
> 完整复盘（死胡同/探针假象/证据链）：`references/d3d9-global-failure.md`。

```bat
:: 用 32 位编译，游戏基本是 32 位
csc.exe -platform:x86 -out:D3D9Probe32.exe D3D9Probe.cs
D3D9Probe32.exe
```

输出含义：

| 结果 | 含义 | 下一步 |
|---|---|---|
| `0x00000000` | 原生 D3D9 正常 | 别上兼容层，查别的原因（崩溃日志、兼容模式、运行库） |
| `0x8876086A D3DERR_NOTAVAILABLE` | D3D9 HAL 被关/驱动层不通 | **先清全局开关（见上）并复测**；仍失败再上 DXVK |
| `0x8876086C INVALIDCALL` | 适配器不可用 | 同上，多半是虚拟显示器/驱动残 |
| 进程直接崩 | 驱动严重异常 | 同上 + 记录显卡型号/驱动版本 |

再跑一次 `D3D9Probe32.exe`，同时把 `d3d9.dll`（dgVoodoo 或 DXVK）放在同目录，就能看出**该层能不能替代原生**——这是选层最可靠的依据。

## 选层决策

| 游戏 API | 首选 | 备选 | 说明 |
|---|---|---|---|
| D3D9 | **原生**（探针返回 0 且游戏能进）；原生不行再上 **DXVK 2.5.3 / 2.3.1**（`d3d9.dll`） | dgVoodoo2 D3D9 | 先探针后选层；DXVK 版本敏感：2.3.1 白屏的游戏换 2.5.3 就正常，反之亦然，**两个都备** |
| D3D8 | **DXVK 2.5.3**（`d3d8.dll` + `d3d9.dll`） | dgVoodoo2 D3D8 | NovaLogic 系（DFBHD）实测 DXVK 的 d3d8 一次过 |
| DirectDraw 1-7 | **cnc-ddraw 7.1**（`ddraw.dll` + `ddraw.ini`） | dgVoodoo2 DDraw | 2D/软件渲染老游戏优先 cnc-ddraw；自带很多游戏的 `[段名]` 配置 |
| DirectDraw + 想硬件加速 | cnc-ddraw 的 `renderer=direct3d9` + **DXVK d3d9.dll** | cnc-ddraw GDI | 硬件模式更锐利；需要 Vulkan。GDI 是通用回退，无 Vulkan 也能跑 |
| D3D11/D3D12 | 一般不需要 | DXVK d3d11 + dxgi | 只在驱动残缺时用 |
| OpenGL | **Mesa Gallium**（`opengl32.dll`+`libgallium_wgl.dll`+`dxil.dll`） | — | 无硬件 OpenGL 的机器（CS 1.6 案例） |

**本机（RX 580 / Win11）实测：dgVoodoo2 的 D3D11 / D3D12 后端不可用**，所以本机交付一律不走
dgVoodoo2；能上 DXVK 就上 DXVK（走 Vulkan）。换一台机器/dgVoodoo2 版本再按探针复测，不要把结论写死。

判定技巧：
- 用 `strings -a 游戏.exe | grep -i "d3d8\|d3d9\|ddraw"` 看导入。
- 找不到字符串不代表没有：可能延迟加载或加密，此时按版本年代猜（2000-2004 多为 D3D8，2004+ 多 D3D9）。
- 有 ReShade/dgVoodoo 自己塞的 `d3d9.dll` 时，先确认它是不是原游戏的（看版本信息），冲突就改名留底。

### 汉化补丁 / ASI 插件（先让层跑通，再查补丁）

汉化版常见结构：`dinput8.dll`（ASI Loader）+ `scripts\*.asi` + 替换过的资源包。图形层跑通后仍要分两件事看：

- **文字变方块**：先确认文本已在资源里；方块通常是补丁 ASI 没生效（它负责改 exe 的编码/解码器）。
  用 Ghidra 反编译 ASI，找它对主程序的字节补丁和 hook：能单独提取的**字节补丁**可以保留，
  其它 hook（vtable 换 CreateDevice、MinHook 钩函数）往往和 DXVK 冲突或是坏的（Just Cause 案例：
  钩子目标落在指令中间 → `0xC000001D`）。
- **ASI 自己崩**：`0xC000001D` = 执行了非法指令（跳板/补丁写歪），`0xC0000005` = 野指针/跳转飞掉。
  打 `E9 rel32` 补丁前必须查 `.reloc`，踩到重定位项的 rel32 会被 loader 改写（pitfalls #43）。
- 交付时把裁剪过的 ASI 命名成自解释的名字（如 `JCTextFix.asi`），**原版进 `_backup\`**，
  `恢复原版.bat` 负责还原；汉化本体（资源包、ASI Loader、其它 asi）一律不动。

## 安装（目录内）

```
游戏目录\
  ddraw.dll / d3d9.dll     ← 生效中的翻译层（DirectDraw 用 ddraw.dll；要硬件再加 DXVK d3d9.dll）
  ddraw.ini                ← cnc-ddraw 默认配置（分辨率/渲染器）
  ddraw-soft.ini           ← 可选：强制软件渲染
  ddraw-640.ini            ← 可选：原版 640×480
  dxvk.conf                ← 可选，DXVK 配置（交付不写 dxvk.hud）
  _cnc-ddraw\              ← 留底（重装用，含全部 ini/dll）
  兼容修复.bat             ← 从 _cnc-ddraw 复制上来
  恢复原版.bat             ← 删掉翻译层并还原备份
  game_starter.exe         ← 默认启动器（所有游戏同名）
  game_starter.bat                   ← 可选：平台只认脚本时的无控制台薄包装，或玩家要控制台时
  game_starter-soft.bat / game_starter-640.bat ← 可选：纯软件 / 原版分辨率
  launcher.ini             ← game_starter.exe 的配置
  _tools\nircmd\           ← bat 居中
```

**游戏目录不放 `.cs` / `.ps1` 源文件**；这些和构建脚本都在 `Games\<游戏>\`。

模板见 `templates/`。批量处理可用 `scripts/FixGame.cs`：

```bat
FixGame.exe D:\Games\某游戏            :: 自动判断位数 + 接口 + 装层
FixGame.exe D:\Games\某游戏 /dry        :: 只看不改
FixGame.exe D:\Games\某游戏 /dgv        :: 强制用 dgVoodoo
FixGame.exe D:\Games\某游戏 /undo       :: 回滚
```

## 启动器（每个交付必须有）

**默认只交一个：GUI 启动器 `game_starter.exe`**（名字统一，见技能铁律 9）。
它必须**完整顶掉 bat 的职责**：自检/补翻译层、按键方案落地、画面模式、陪跑到游戏退出并回传退出码。
`game_starter.bat` 只在两种情况下存在：① 平台入口只认脚本 → 当「无控制台薄包装」用；② 玩家明确要控制台说明。
这两种情况下它的选项/默认值必须与 `game_starter.exe` 一致。规范见 `references/launchers.md`。

- **中国红主题**：bat `color 4f`；exe 背景 `#C0121E`、金色标题/边框 `#FFD85E`、白/米色正文，不要黑底。
- 一个游戏可以有多个兼容启动方式（如 `game_starter-soft.bat` 纯软件、`game_starter-640.bat` 原版分辨率），
  但默认 `game_starter.exe`（以及同时交付的 `game_starter.bat`）必须是同一套逻辑和默认配置。
- 默认启动器每次启动时从 `_cnc-ddraw\` 恢复默认 `ddraw.ini`，避免备用模式残留。
- 启动器导入游戏注册表要幂等（关键值已存在就跳过）；多套按键/设置用 `切键-*.bat`
  单独切换，别让启动器每次覆盖玩家改键。
- BAT 模板：`templates/game_starter.bat`；EXE 模板：`templates/Launcher.cs`（**最老的功能版**，
  没有三栏/键帽/日志，且带已废弃的「打开游戏目录」按钮，只当最小可编译样例看）。
  **新游戏的启动器一律照技能 `.qoder\skills\legacy-launcher-gui\` 写**，
  或直接抄 `Games\RoadRash\src\Launcher.cs` 这份已验收的实现。

居中用的 nircmd 已随仓库提供：`tools/nircmd/`。

## 验证清单

1. **水印验证层生效**：测试时开 cnc-ddraw 的 GDI 软件警告（auto 回退）或 DXVK HUD
   （`dxvk.hud = version,api,fps`，或临时设环境变量 `DXVK_HUD=version,api,fps`），截图确认走硬件还是软件；
   `devinfo` 只显示显卡型号，不能当兼容层证据。证据存 `Games\<游戏>\evidence\`。
   半自动取证可用 `scripts\capture-dxvk-hud.ps1 -GameDir <目录> -ProcessName <进程> -OutDir <目录>`：
   它临时设 `DXVK_HUD`，截完自动清掉，不改游戏配置。
2. 游戏不再弹错，能到主菜单并进一局；画面正常，不是纯色/花屏。
3. **默认模式和备用模式都跑一遍**：`game_starter.bat` / `game_starter.exe` 一致；`game_starter-soft.bat` / `game_starter-640.bat`（如有）
   各自配置生效，退出后恢复默认配置。
4. **分辨率对比**：640×480 与 1920×1080 各截一张，确认黑边/比例/清晰度；1080p 要说明是输出放大。
5. 退出后桌面分辨率复原、启动窗口自动关闭；`恢复原版.bat` 能回滚。
6. **交付无水印**：HUD 关闭、默认配置不触发软件警告；测试水印只留在 `evidence\`。

## 分辨率

- DXVK **不能**强制分辨率，分辨率由游戏决定；dgVoodoo2 的 Force resolution 才能强制输出。
- 老引擎常只有 4:3 档位。做法：把游戏设到最高档全屏，让显卡缩放铺满；或在游戏配置里找到 `game_res` 之类的档位索引（见 `references/resolution.md` 实测表）。
- **DirectDraw 游戏可以用 cnc-ddraw 放大输出**（引擎内部仍是原分辨率）：
  ```ini
  width=1920
  height=1080
  fullscreen=true
  windowed=false
  maintas=true      ; 4:3 保持比例，左右黑边
  ```
  640×480 → 1440×1080（2.25 倍）。GDI 双线性偏柔和；`renderer=direct3d9` + DXVK 的 bicubic 更锐利。
  要如实记录“输出 1080p / 内部 640×480”，不要写成原生 1080p。
- 启动器可以在每次启动前自动改写游戏配置文件，实现"自动按最高档全屏跑"。
- **DXVK + 640×480 组合**在 RoadRash 实测开场 8~16 秒黑屏；同一游戏 1080p + DXVK 正常。
  遇到开场黑屏先换分辨率/换 GDI，再怀疑层。

## 常见坑（摘要，完整 41 条见 `references/pitfalls.md`）

- **探针必须 32 位编译**，否则同目录的 32 位包装 DLL 根本不会被加载。
- **D3D9 全挂先清 `Direct3D\Drivers\SoftwareOnly` / `DirectDraw\EmulationOnly`（64 位 + WOW6432Node 两个视图）**；这是 DirectX 修复工具写的，重装驱动无效。
- **DXVK 版本敏感**：The Saboteur 在 2.3.1 白屏、2.5.3 正常 —— 两个版本都备着，出事就换。
- **dgVoodoo2 的 D3D11 后端**在部分游戏把 UI/过场渲成洋红，D3D12 后端在另一些游戏直接崩。
- **DXVK 3.x** 需要 `VK_KHR_load_store_op_none`，老 AMD 驱动会报 `No adapters found`。
- **DXVK 不能强制分辨率**；`d3d9.forceAspectRatio` 只改宽高比报告。
- **cnc-ddraw 的软件警告**：`-WARNING- Using slow software rendering...` 显示 15 秒，是 auto 回退 GDI 的提示；
  测试时当“软件渲染水印”，交付时用 `renderer=gdi`（强制软件）或让 auto 走 DXVK（硬件）避免。
- **bat 里写 `>/dev/null` 会报“系统找不到指定的路径”**：cmd 只认 `>nul`，别把 bash 习惯带进 bat。
- **配置要有唯一来源**：调好的 `ddraw.ini` 要放回 `Games\src`，否则构建时会被上游默认配置覆盖。
- **bat 里别写复杂 PowerShell**，`$`/`|`/`{}` 会被 cmd 吞；等待游戏退出用阻塞调用。
- `if not exist "x" >"x" echo ...` 会无条件截断文件。
- **汉化外挂**类：必须从外挂启动，外挂不阻塞，等游戏要用进程名判断。
- **LAN 客户端 ≠ 完整版**（DFBHDLC 没有单机）。
- 反作弊网游不接。

## 参考

- `references/diagnose.md` 诊断细节与错误码
- `references/d3d9-global-failure.md` 整机 D3D9 失效排查复盘（全局开关 / 死胡同 / Procmon 证据链）
- `references/dxvk.md` DXVK 配置与版本坑
- `references/dgvoodoo.md` dgVoodoo2 配置与坑
- `references/resolution.md` 分辨率与宽屏
- `references/launchers.md` 启动器规范（BAT / EXE 两种）
- `workflow/workflow.md` 可执行工作流（adapt.ps1 参数、阶段、限制）
- `references/pitfalls.md` 踩坑清单（症状 → 原因 → 处理）
- `references/cases.md` 实战案例（CS 1.6 / 虐杀原形 2 / 死亡阴影 / DFBHD ×2）
