# 项目长期笔记

- 项目定位：不改系统、不装驱动，用绿色翻译层让老游戏在 Win11 / 新显卡上跑起来。权威规矩在根目录 AGENTS.md（28 条）与 SKILL.md（铁律 / 决策表），别在别处另写一份。
- 改动范围（用户硬规矩，AGENTS.md 第 25 条）：除非用户明说「都改 / 全部改」，只改点名的那个游戏，不顺手全库统一、不批量重编启动器。
- 游戏目录被删（AGENTS.md 第 26 条）是正常状态，不算异常、不补文件、不报错，Games 工作区照旧留着。
- 交付形态（用户硬规矩，AGENTS.md 第 27 条，2026-11-04）：除非用户明说要 bat / 平台只认脚本，**新交付只做 `game_starter.exe`**，不再新写 bat；已经发出去的 bat 不改、不删、不重写。规则 8 的 exe/bat 一致性只对已有那批生效。
- GUI 启动居中（用户硬规矩，AGENTS.md 第 28 条，2026-11-04）：所有 `game_starter.exe` 窗口必须 `CenterScreen` 或等价手工居中，不许默认位置 / 上次位置 / 固定左上角；控制台 bat 居中走 nircmd。全库 13 个 Launcher.cs 已全部符合。
- 脚本环境：本机 `pwsh.exe` 是 7.x（实测 7.6.6），写脚本默认用它；`powershell.exe` 是系统 5.1，只在确实需要 5.1 特有行为时用并写明原因。
- 收尾必跑 `pwsh -NoProfile -File scripts\规范自检.ps1`，要 0 项；所有文本文件统一 CRLF。
- 开发机：Win11 + AMD RX 580，驱动 31.0.21925.1001 / AMD Software 26.5.2（驱动包 2026-11-01 装入）。原生 D3D9 曾被「DX 修复工具」写的全局开关关掉、11-01 已修好。**2026-11-04 实测：32 位和 64 位进程都能建硬件 OpenGL 4.6 上下文**（ATI Technologies Inc. / RX 580 2048SP / GLSL 4.60 / 287 扩展）。仓库里「无硬件 OpenGL，只有 GL 1.1 GDI Generic」是 10 月底旧驱动状态下的过时结论，文档尚未回改（等用户点名）。「dgVoodoo2 的 D3D11/12 后端不可用」同为旧结论，未复测。换机器先跑探针。
- 启动器写环境变量的通用做法（2026-11-13 定案）：**不许碰 ProcessStartInfo.EnvironmentVariables**——机器环境常有大写/小写重复键（HTTPS_PROXY + https_proxy），StringDictionary 一访问就抛「已添加项」。一律 `Environment.SetEnvironmentVariable` 设当前进程环境，子进程继承。
- 测试启动器/游戏进程的通用做法：PowerShell 工具的 job 会在命令结束时杀掉会话内启动的进程树（退出码 0xFFFFFFFF 假象）。要真验证就用一次性计划任务（Register-ScheduledTask → Start → Unregister）脱离 job。
- PS4 移植整合包（SanctuaryPort/SpartanPort 系）：官方 Rust 启动器 --play = 调旁边 python.exe 跑 run.py；run.py 才是完整编排（CPU 检查/更新合并/着色器），配置 sanctuary.ini 是无节平键文件。游戏专属 profile 的 env（vsync/readbacks 等）代码里定死，外部写了会被盖。**键位可以注入**：`SANCTUARY_KEY_MAP`（SpartanPort 系为 `SPARTAN_KEY_MAP`）格式 `按钮名=键名` 逗号串；按钮名从移植器二进制按钮表取（cross circle square triangle l1 r1 l2 r2 l3 r3 options touchpad touchpad_left touchpad_right up down left right），键名用 Space/Escape/Tab/单字母这类写法（带空格的 Mouse Left 会把暗黑3 probe 解析器搞崩，战神3版本没事）。暗黑3已注入 15 项实机验证通过（pad map: 15 remapped，零 ignored）；战神3专属动作（rage/grab 等）别注入，会被忽略。**坑 1：上一局 run.py 没退干净就再启动，新实例会中途被杀（-1）或 GPU 管线崩——排查先查残留 python.exe run.py。坑 2：测试 taskkill 强杀游戏后，out\game-update-* 临时合并视图会残留，攒多了再启动就 0x17 秒退——强杀后必清 out\game-update-*。**
- **键位设计原则（用户硬规矩，2026-11-13）**：手柄映射的纯键盘键位必须按人体工程学分工——**左手只管移动（WASD）和喝药（Q，WASD 边上不挪手），全部战斗技能放右手**（J K L home row + U O 上排 + H 下探一带）；技能放左手数字键 = 打怪时左手顾不上，明确禁止。PC 原版社区的键位（1234 技能栏、Q 药水）是鼠标操作那套，**不适用**于手柄映射交付，别拿来照搬。暗黑3现役方案即按此排（部署 sha256 889cb45d…；喝药键用户同日纠正：整合包说明本来就是 Q=L1，重排时改空格属自作主张，已改回）。
- **GUI 控制台文案规矩（用户定案，2026-11-13）**：启动器自己的技术日志（启动参数/分辨率/DPI/布局等）**只写运行日志.txt，不刷 GUI 控制台**；GUI 控制台只转发 run.py/移植器实时输出和退出码。技术细节写源码注释给 AI 读，不显示给玩家。**run.py 这类行话对玩家没意义，GUI 上叫「后台服务器」**（用户原话：离线服务器或后台服务器）。


- **dgVoodoo conf 不可信教训（2026-11-15，血战上海滩）**：tools\dgvoodoo\x86 的四个 DLL（D3D8/D3D9/D3DImm/DDraw）实测不读 dgVoodoo.conf——OutputAPI 破坏性测试（改 d3d12 照跑）是 10 分钟定生死的判据；该批 DLL 无 dgVoodoo 字符串、无版本资源，来源存疑。铺满方案必须靠启动器切桌面实现，别指望层配置。
- **切桌面铺满骨架（2026-11-15）**：Games\Shanghai\src\Env.cs（PickMode/Apply/Restore）+ Launcher StartGame try/finally 硬义务 + --mode=N 三前缀兼容，同类 4:3 老游戏直接拷。
- **capture-window.ps1 默认截完杀进程**（要留进程加 -KeepOpen）；对 2003 老引擎发 WM_CLOSE 会假死（画面停声音在），测试收尾一律 Stop-Process 强杀。
- **一键秘籍骨架（2026-11-15）**：Games\Shanghai\src\Cheats.cs（SendInput+KEYEVENTF_SCANCODE，DirectInput 引擎只认扫描码，_ 走 Shift+Minus，每键 60-80ms）+ 中栏按钮 + /cheat=名 三前缀自证通道，同类老游戏直接拷。
- **无线程僵尸壳进程（2026-11-15）**：强杀/假死的 2003 老游戏会留 ThreadCount=0、20K 内存、taskkill 拒绝访问的壳，只能重启清掉；在场不影响新游戏启动。GUI 交付别做托盘（老年玩家找不到），正常任务栏最小化 + 关闭即关闭 + 独立恢复进程兜底（--restore=w,h,rate,pid，pid=0 立即还原可当手动自救口）。
- **启动即崩自愈**：老游戏 0xC0000005 且 <15s → 自动重拉一次（环境指纹变更首启的规律：弹「设置最优设置？」点掉后崩，指纹写回第二发稳）。重拉前必须收走旧恢复进程，否则它抢着还原桌面。
