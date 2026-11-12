# 已判死的方案（禁止再试）

**规则**：这张表里的方案，实测已经失败；除非用户明确要求，且前提条件真的变了（要在本文件里写明变了什么），
否则**不许再尝试**，也不许"再确认一次"。每次试都是在浪费客户机的时间和你的时间。

新增一行的格式：`| 游戏 | 方案 | 症状原文 | 判定日期 | 证据 |`。

| 游戏 | 已判死的方案 | 症状（原文） | 判定日期 | 证据 |
|---|---|---|---|---|
| 英雄无敌3 | **dgVoodoo2 全屏**（预设 `dgVoodoo-full.conf`）。**只判死这一个变体**：窗口档 `dgVoodoo-window.conf` 是默认档、实测能用，别因为这条把它一起删掉 | `DirectDraw Error (150): 'DDERR_INVALIDRECT'  File: 'D:\Source_Code\Game\WINGRAPH.CPP'  Line: 456` | 2026-11-04 | 启动器路径稳定复现；同一份 conf 直接双击 `Heroes3.exe` 反而能进 → 不可控。`evidence\mode6-probe.png` |
| 英雄无敌3 | **`renderer=opengl`** | 黑屏 / 崩；本机无硬件 OpenGL（只有 GL 1.1 GDI Generic） | 2026-11-04 | `gl_probe.ps1` |
| 英雄无敌3 | **`renderer=auto`** | 先试 direct3d9，崩了来不及换别的 | 2026-11-04 | 决策表 + 实测 |
| 英雄无敌3 | **DXVK**（往游戏目录放 `d3d9.dll` / `dxgi.dll`） | 与 cnc-ddraw 抢绘制、易崩 | 2026-11-04 | 决策表 |
| 英雄无敌3 | **调试器方式抓崩溃**（`DEBUG_ONLY_THIS_PROCESS` 拉起游戏 + minidump） | 游戏窗口根本不出现，玩家看着像没启动；远程一改就"起不来"，比原来的崩溃更糟 | 2026-11-07 | 用户两次点名「你看你又跑这个！」「又搞什么，现在游戏又不出界面了！」；代码已删，改成读系统自动生成的 WER 报告（`game_starter.exe /wer` + `tools\wer-test.ps1`） |
| 英雄无敌3 | **WER LocalDumps 注册表**（`HKLM\...\WER\LocalDumps`）开自动转储 | 要写 HKLM、要"以管理员身份运行"，客户机上根本没有这一步，交付就不该有这个前提 | 2026-11-07 | 用户定案「哪里有什么管理员？我们只能给用户这个游戏目录」（AGENTS 规则 36）；现方案只读 `%ProgramData%\Microsoft\Windows\WER\Report*`，不动系统 |
| 三国群英传8 | **复刻离线 Steam 模拟**（MiniPipeDll / 私有 Hive） | 受保护机制，属合规红线 | 2026-11-04 | `Games\KingdomHeroes8\适配过程分析.md` §7 || 鬼泣/战神3 等移植器 | **反编译后重新打包** | 不需要，运行时取证已足够 | 2026-11-04 | 各游戏适配分析 |
| 使命召唤4 重制版 | **改 `steam_emu.ini` / `CONFIG.ini` 的 `Language=`、改 `HKCU\Software\Valve\Steam` 的 `Language`、进程环境变量、命令行 `+set language` 来切语言**（只判死这台整合包的这几个开关，不是说游戏本体不支持别的语言） | 11 组对照实验全部照旧进简体中文主菜单（战役／新游戏／任务选择／选项）；把 `simplified_chinese` 目录藏起来，游戏直接起不来 | 2026-11-12 | `Games\CoD4Remastered\evidence\probe-*.png`、`probe2-*.png`、`probe3-*.png`，`适配过程分析.md` §2 |
| 使命召唤4 重制版 | **给这台整合包加「繁体中文」选项** | 主 exe 里虽有 `traditional_chinese` / `tchinese` 字样，但游戏目录只装了 `english` 和 `simplified_chinese` 两个语言包，没有繁体包；用户最终定案「不切语言了」 | 2026-11-12 | 目录清单 + 实测截图，`适配过程分析.md` §2 |

## 为什么写死这条

2026-11-07 用户原话：「那个 456 的错误，一直是错误的，你就记录住，不要再尝试了，为什么还要尝试？」
—— 同一个已知失败的配置被反复重试，既浪费客户机时间，也会让人误以为"这次可能不一样"。
判死的方案就写在这里，`scripts\规范自检.ps1` 会扫源码，重新引进来会直接报错。
