# 分辨率与宽屏

## 分工

| 层 | 能否改分辨率 |
|---|---|
| 游戏本体 | 决定内部分辨率（受引擎档位表限制） |
| DXVK | **不能**，完全转发游戏给的尺寸 |
| cnc-ddraw（DirectDraw） | 能改**输出**：`width/height + fullscreen + maintas`，内部仍原分辨率 |
| dgVoodoo2 | 能：`ForceResolution` + `Resolution = 1920x1080` |
| 显卡驱动缩放 | 非原生模式会自动缩放铺满（可能拉伸或留黑边） |

## 老引擎只有 4:3

2000-2005 年很多引擎（NovaLogic 系等）档位表写死为 4:3/5:4：

```
512x384  640x480  800x600  1024x768  1280x1024  1600x1200  1920x1440  2048x1536
```

显卡在 1920x1080 面板上会缩放。想"铺满"就在游戏里选最高可用档全屏；想"不变形"就把驱动缩放设为
保持宽高比（会留黑边）。

## 找到游戏的配置文件

| 游戏 | 位置 |
|---|---|
| Delta Force 系列 | 游戏目录 `dfv.cfg` |
| 多数老游戏 | `%USERPROFILE%\Documents\My Games\<游戏名>`、`%APPDATA%`、或注册表 |

`dfv.cfg` 关键项：

```ini
windowed = 0              ; 全屏
hw3d_res1600x1200 = 1     ; 必须为 1 该档位才可用
game_res = 8              ; 档位索引，见下表
```

## DFBHD 档位实测表（1.2 与 1.5 相同）

| game_res | 结果 |
|---|---|
| 0–3、5、8（未开 1600 开关时）、9+ | 800x600（回落） |
| 4 | 640x480 |
| 6 | 1024x768 |
| 7 | 1280x1024 |
| **8** | **1600x1200**（需先把 `hw3d_res1600x1200 = 1`） |

1920x1440 在 1080p 面板上不可用（显示器不报告该模式），设了会回落。

**2026-11-01 实测（DXVK 2.5.3，1.5 汉化版）**：五档
640×480 / 800×600 / 1024×768 / 1280×1024 / 1600×1200 全部可用；启动器默认 1600×1200，
每次启动写 `game_res`、`windowed=0`、`hw3d_res* = 1`。`windowed=1` 实测仍改桌面分辨率，
不作为启动器选项。退出后桌面恢复 1920×1080。实现见 `Games\DFBHD\src\Launcher.cs`。

## 让启动器自动套用

在 `game_starter.bat` 启动游戏前改写配置（PowerShell 单行，注意不要写复杂脚本）：

```bat
if exist "dfv.cfg" powershell -NoProfile -Command "$p='dfv.cfg'; $t=Get-Content $p -Raw; $t=$t -replace 'game_res\s*=\s*\d+','game_res = 8'; $t=$t -replace 'windowed\s*=\s*\d+','windowed = 0'; $t=$t -replace 'hw3d_res1600x1200\s*=\s*\d+','hw3d_res1600x1200 = 1'; Set-Content $p $t -NoNewline"
```

效果：每次启动都以最高档全屏运行；退出后桌面分辨率自动恢复。

## DirectDraw 游戏：cnc-ddraw 输出放大

固定 640×480 的 DirectDraw 游戏（如 RoadRash 2002）可以用 cnc-ddraw 把**输出**放大到 1080p：

```ini
width=1920
height=1080
fullscreen=true
windowed=false
maintas=true          ; 保持 4:3，左右黑边
renderer=gdi          ; 或 auto / direct3d9
```

- 内部渲染仍是 640×480；输出 1440×1080（2.25 倍）+ 左右黑边。**不是原生 1080p。**
- GDI 用双线性，画面柔和；`renderer=direct3d9` + DXVK 的 bicubic 更锐利，但需要 Vulkan。
- 桌面分辨率保持不变（`fullscreen=true` + `windowed=false` 在本机实测不切桌面）。
- 必须实测 640 与 1080 各一张截图，记录清晰度/黑边/性能，别只写“支持 1080p”。

## 想真·原生 1920x1080

- 引擎不支持 → 只能上 dgVoodoo2 的 Force resolution（内部 4:3，输出 1080p，视觉上是拉伸）。
- DXVK 路线下做不到；不要被 `d3d9.forceAspectRatio` 误导，那只影响宽高比报告，不改分辨率。
