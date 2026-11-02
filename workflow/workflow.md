# 工作流

把 `SKILL.md` 的知识变成一条可执行流水线：**探测 → 选层 → 落地 → 验证 → 出报告**，
中间留人工确认点。

```bat
:: 一键（推荐）
workflow\adapt.bat -GameDir "D:\Games\SomeGame"

:: 带参数
workflow\adapt.bat -GameDir "D:\Games\SomeGame" -Title "三角洲特种部队6 黑鹰坠落" -Launcher exe
workflow\adapt.bat -GameDir "D:\Games\SomeGame" -Layer dgv
workflow\adapt.bat -GameDir "D:\Games\SomeGame" -DryRun
```

直接调 PowerShell：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File workflow\adapt.ps1 -GameDir "D:\Games\SomeGame" -DryRun
```

## 五个阶段

| 阶段 | 脚本自动做什么 | 产物 / 判据 | 人工要确认什么 |
|---|---|---|---|
| **P0 采集** | 挑主程序（按大小+导入表+排除安装器/LAN 客户端）、读 PE 位数、扫 `d3d8/d3d9/ddraw/d3d11/opengl32` 导入 | 主程序名、位数、接口 | 自动挑错时用 `-Exe` 指定 |
| **P1 探针** | 现场编译 `D3D9Probe` 并运行，解析每个适配器的 HRESULT | `原生 D3D9: 可用/不可用` | 可用且不 `-Force` 时不装层 |
| **P2 选层** | D3D8/D3D9 → DXVK；DirectDraw → dgVoodoo；OpenGL → 提示手工用 Mesa；接口未知时 d3d8+d3d9 都放 | 选层结果 | 探针 OK 但游戏仍挂 → 查运行库/免 CD |
| **P3 落地** | 先把源文件/配置写进 `Games\<游戏>\src`；构建脚本编译 `game_starter.exe`、bat 转 GBK+CRLF，再复制 DLL/INI/启动器到游戏目录；原文件进 `_cnc-ddraw\`（或 `_dxvk\`），写 `ddraw*.ini` / `dxvk.conf`；生成 `game_starter.bat` + `game_starter.exe`（+ 可选 `game_starter-soft.bat` / `game_starter-640.bat`）、`兼容修复.bat` / `恢复原版.bat` | 目录里的成品 + `Games\<游戏>\` 源文件 | 已有 `game_starter.bat` 会被覆盖（原版备份到 `Games\<游戏>\backup`）；游戏目录不允许出现 `.cs` |
| **P4 验证** | 先开水印/HUD 证明层生效与渲染路径；再跑默认/备用启动方式，启动游戏并截图到 `Games\<游戏>\evidence\`；判进程存活、640 与 1080 对比、退出后桌面分辨率恢复；关掉水印/HUD | 截图 + 日志 + 存活/分辨率 | 截图里要看**主菜单/进局画面**；软件警告/HUD 只能出现在测试证据里，交付配置要关掉 |
| **P5 报告** | 写 `Games\<游戏>\适配过程分析.md`：症状、PE/探针、测试矩阵（原生/各层/各分辨率/硬件软件）、最终方案、证据、回滚；`references/cases.md` 加摘要 | 分析 md + 案例 | 记录“哪个更好、为什么选它”；未验证项明确标注 |

## 参数

| 参数 | 作用 |
|---|---|
| `-GameDir` | 游戏目录（必填） |
| `-Exe` | 指定主程序文件名，默认自动挑 |
| `-Title` | 启动器标题（默认用目录名） |
| `-Layer auto\|dxvk\|dgv\|none` | 选层策略，默认 auto |
| `-Launcher bat\|exe\|none` | 生成哪种启动器，默认 bat |
| `-DryRun` | 只探测和打印，不改文件 |
| `-NoVerify` | 跳过启动验证 |
| `-Force` | 探针显示原生正常也照样装层 |

## 产物

```
<游戏目录>
  ddraw.dll / d3d9.dll      翻译层（DirectDraw 用 ddraw.dll；硬件加速再加 DXVK d3d9.dll）
  ddraw.ini                 默认配置（1080p / renderer=auto）
  ddraw-soft.ini            可选：纯软件渲染
  ddraw-640.ini             可选：原版 640×480
  dxvk.conf                 交付关闭 HUD
  _cnc-ddraw\               留底
  _tools\nircmd\
  兼容修复.bat 恢复原版.bat
  game_starter.bat / game_starter.exe           默认启动器（两个逻辑一致）
  launcher.ini
  road64.reg / road.reg     游戏设置（启动器仅在关键值缺失时导入）
  切键-*.bat                可选：多套按键/设置切换（导入 + 同步默认 reg）
  game_starter-soft.bat / game_starter-640.bat  可选：多兼容启动方式

Games\<游戏>\
  src\                      源文件（Launcher.cs、go*.bat、兼容修复/恢复原版、ddraw*.ini、dxvk.conf）
  tools\                    层文件、探针/测试脚本
  evidence\                 水印、最终画面、DXVK 日志
  backup\                   用户原文件（go.bat.原版）
  build.ps1                 编译/转码/复制成品
  适配过程分析.md            测试矩阵与结论
```

## Games 工作区与多模式

- **源文件不进游戏目录**：`.cs` / `.ps1` / 未转码的 UTF-8 bat 都放 `Games\<游戏>\src`。
  `build.ps1` 用 csc 编译 exe，把 bat 转 GBK+CRLF，再复制成品。
- **默认两个启动器**：`game_starter.bat` 和 `game_starter.exe` 必须同逻辑（自检/补层、导入游戏注册表、等待退出、提示一致），
  都用中国红主题（bat `color 4f`；exe `#C0121E` + 金色）。
- **允许额外启动方式**：按渲染/画质增加 `game_starter-soft.bat`（强制软件）、`game_starter-640.bat`（原版分辨率）等；
  每个模式对应一份 `ddraw-*.ini`，启动前复制、退出后恢复 `_cnc-ddraw\ddraw.ini`。
- **默认配置有唯一来源**：调好的 `ddraw.ini` 回存 `Games\src`，不要只改游戏目录。

## 水印验证（测试时开，交付时关）

- cnc-ddraw 没有可开关的 logo 水印；软件渲染回退时会在顶部显示 15 秒
  `-WARNING- Using slow software rendering...`，可作为“软件渲染水印”。
- 硬件渲染：cnc-ddraw `renderer=direct3d9` + DXVK，`dxvk.conf` 写 `dxvk.hud = fps`，
  左上角只显示 FPS；排障时才临时加 `version,api`。
- dgVoodoo2 用 `dgVoodooWatermark = true`。
- 流程：开水印 → 截图存 `evidence\` → 确认层生效和硬件/软件 → 交付配置关掉 HUD/水印。
- 1080p：先确认引擎内部分辨率；DirectDraw 固定 640×480 的游戏，1080p 是 cnc-ddraw 输出放大，
  640 与 1080 各截一张对比，记录清晰度/黑边/性能。

## AI 使用方法

1. 先 `-DryRun` 跑一遍，把探针结果和候选主程序看一遍，确认没挑错。
2. 正式跑（默认带验证），**亲眼看 P4 的截图**：
   - 到达主菜单 / 进入关卡 → 通过；
   - 纯色、花屏、报错框、进程提前退出 → 回到 `references/` 排查（先换 DXVK 版本，再换层）。
3. 用 `Games\<游戏>\tools` 下的观察脚本多跑几次（老游戏启动可能 8~30 秒才进画面），
   按 `水印验证` 开一次软件/硬件标识并截图，再关掉跑交付模式。
4. 通过后，把这一局写进 `references/cases.md`，并在 `Games\<游戏>\适配过程分析.md` 记录完整测试矩阵
   （原生、各层、640/1080、硬件/软件、哪个更好、最终选择）。
5. 遇到脚本覆盖不到的情况（外挂启动器、汉化、需要改游戏配置），手工按 `SKILL.md` 处理，
   并把新规则补回文档。

## 已知限制

- 只处理 D3D8 / D3D9 / D3D11 / DirectDraw 的翻译层；OpenGL 只给提示（Mesa 需手工放三件套）。
- 验证只截一张图，不代表进局可玩；复杂游戏仍要人工过一遍。
- 游戏若需要外挂启动器（汉化外挂等），生成的 `game_starter.bat` 会直接跑主 exe —— 要手工改成
  `game_starter-loader.bat` 模板并填 `-Exe` 外挂名。
- 不写注册表、不改系统；需要写注册表的游戏（如 CS 1.6 分辨率、RoadRash 自身设置）要单独处理，
  使用游戏目录自带的 `.reg` 并保留回滚。
- 1080p 输出放大不等于引擎原生 1080p；DXVK/硬件模式需要 Vulkan。
