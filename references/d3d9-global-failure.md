# 整机 D3D9 失效排查复盘（War3 1.27 / 2026-11-01）

> **结论先行**：不是驱动、不是显卡、不是远程软件、不是虚拟显示器、不是 WSL/Hyper-V、不是刷新率、
> 不是 D3D9On12、不是 DirectX 运行库。根因是“DirectX 修复/优化”类工具写入的两个**全局强制软件渲染开关**：
>
> - `HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly = 1`（64 位 + WOW6432Node **两个视图**）
> - `HKLM\SOFTWARE\Microsoft\DirectDraw\EmulationOnly = 1`（64 位）
>
> 删除即可恢复，**不需要重启、重装系统、ISO 或更新**。

## 0. 五分钟版（下次先走这个）

**触发信号**（满足任意两条就按本文处理，不要先换驱动）：

- **所有** D3D9 游戏同时报“不能初始化DirectX”/`D3DERR_NOTAVAILABLE`（`0x8876086A`）；
- `dxdiag` 显示 `D3D Status: Disabled`，但 `DDraw Status: Enabled`；
- 同机 **D3D9On12 也建不出设备**；
- 同机 **D3D11 / D3D12 / Vulkan 正常**（DXVK 能跑）；
- 换驱动、DDU、重启、装 DirectX 运行库、重装游戏都无效。

**30 秒处理**：

1. 备份后删除这四个值（存在才删）：
   - `HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly`
   - `HKLM\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers\SoftwareOnly`
   - `HKLM\SOFTWARE\Microsoft\DirectDraw\EmulationOnly`
   - `HKLM\SOFTWARE\WOW6432Node\Microsoft\DirectDraw\EmulationOnly`
2. **不需要重启**；直接跑 `scripts\修复本机D3D9.bat`（自动备份到 `%TEMP%\D3D9-*.reg`，然后跑探针）。
3. 用 `scripts\D3D9Probe32.exe` 复测：全 `0x00000000` = 恢复；仍是 `0x8876086A` = 继续查（见第 6 节）。
4. 启动游戏；原生 D3D9 正常就**不要**上 DXVK。

**注意键位**：开关在 **`Direct3D\Drivers` 子键**，不在 `Direct3D` 根键；**32 位游戏和 32 位探针读 `WOW6432Node`**，只顾 64 位会漏。

## 1. 最终证据链

### 1.1 Procmon 抓到的原文

对 `d3ddev.exe`（真实窗口的 x64 D3D9 探针）做 9 秒 Procmon 抓取，过滤 `d3ddev.exe` + `Direct3D`：

```
13:24:22.9773267  d3ddev.exe  RegQueryValue
  HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly
  SUCCESS  Type: REG_DWORD, Length: 4, Data: 1
```

紧接着 9 ms 后 `GetDeviceCaps(HAL)` / `CreateDevice` 返回 `0x8876086A`。
同一份抓取里还能看到 `d3d9.dll` → `atiumd64.dll` → `atiumd6a.dll` → `amdihk64.dll` 全部加载成功，
所以**不是 AMD 的 D3D9 UMD 加载失败，而是 D3D9 运行时被这个开关强制成了软件路径**。

### 1.2 两个开关分别影响谁

| 开关 | 视图 | 影响的进程 | 本次实际情况 |
|---|---|---|---|
| `Direct3D\Drivers\SoftwareOnly` | 64 位 | 64 位 D3D9 程序 | =1，导致 64 位探针全挂 |
| `Direct3D\Drivers\SoftwareOnly` | WOW6432Node | **32 位 D3D9 程序（War3）** | =1，**这才是 War3 的直接原因** |
| `DirectDraw\EmulationOnly` | 64 位 | 64 位 DDraw | =1（已先删，但删它不够） |
| `DirectDraw\EmulationOnly` | WOW6432Node | 32 位 DDraw | =0（本来正常） |

**教训**：两个开关是独立的；只删 `EmulationOnly` 不够，只查 64 位视图也不够。
War3 是 32 位，读的是 `WOW6432Node\...\Direct3D\Drivers\SoftwareOnly`。

### 1.3 为什么 D3D9On12 也失败

`Direct3DCreate9On12` 走的是同一个 D3D9 运行时的设备创建路径；运行时被 `SoftwareOnly` 强制成软件后，
9on12 同样拿不到 HAL 设备。**“9on12 和原生一起挂”是运行时级开关的强信号，不是 AMD UMD 的锅。**

## 2. 真实时间线（2026-11-01）

| 时间 | 事件 |
|---|---|
| 10/18 09:00 | 安装官方 26.5.2（`31.0.21925.1001`），之后一直正常 |
| 10/21 15:45 | 安装 War3（XDGAME 整合版） |
| 11/1 01:51 | War3 最后一个存档（**此时还能玩**，驱动仍是 26.5.2） |
| 11/1 01:52 | 开始排查；DXVK 临时方案落地（`_dxvk\`） |
| 11/1 02:44–10:54 | Windows Update 反复换驱动：Basic Display → 2022 版 `31.0.12027.9001` → 26.5.2 → 2022 → 26.5.2，共 6 次（**把水搅浑**） |
| 11/1 11:15 | 重新装回 26.5.2，D3D9 仍挂（开关不在驱动里） |
| 11/1 13:20–13:24 | 确认 32/64 位、真实窗口、窗口/全屏、9on12 全挂；D3D11/12/Vulkan 正常 |
| 11/1 13:24 | Procmon 抓到 `Direct3D\Drivers\SoftwareOnly=1` |
| 11/1 13:25 | 删除两个视图的 `SoftwareOnly`；探针全部 `0x00000000` |
| 11/1 13:26–13:27 | War3 原生 1920×1080 进主菜单（干净副本 + 主目录各一张截图） |

## 3. 死胡同与假象（为什么绕了这么久）

| # | 当时看到的现象 | 当时的推断 | 真相 | 教训 |
|---|---|---|---|---|
| 1 | `d3dsteps.exe` 打印 `GetAdapterDisplayMode=S_OK 0x0` | 显示模式映射坏了 | **printf 参数求值顺序**：`dm.Width/Height` 在调用前被读取，实际 `GetAdapterDisplayMode` 返回的是 1920×1080 | 探针必须**先调用、再单独打印**；不要把调用塞进 printf 参数里 |
| 2 | C# 探针 `adapters: 2`、adapter1 `0x8876086C` | 有两个适配器，第二个坏了 | **vtable 索引写错**：索引 1 是 `AddRef`，返回 2；`GetAdapterCount` 是索引 4；`CreateDevice` 是索引 16 | COM 探针逐个核对 vtable；至少用 C++ 头文件探针交叉验证 |
| 3 | 用 `GetDesktopWindow()` 当 `hDeviceWindow` | 桌面窗口应该也能建 | 桌面窗口是额外变量，掩盖了真实原因 | 用**真实窗口**（`CreateWindowEx`/控制台窗口）测 |
| 4 | 59Hz→60Hz、1280×720、各种格式 | 刷新率/模式不匹配 | 全部 `0x8876086A`，模式无关 | 先排除模式，但别把它当根因 |
| 5 | 向日葵/UU 远程、虚拟显示器 OrayIdd/GameViewer | 远程/虚拟显示导致 | 卸载 + 重启后照样挂 | 用“卸载+重启+对照”排除，不要靠猜 |
| 6 | `HypervisorPresent=True`、`Virtualization: Paravirtualization`、`Displayable: Not Supported` | GPU 被半虚拟化/仅渲染 | 都是驱动/系统正常字段或干扰项；D3D12 displayable 改成 1 也没救 D3D9 | 不要用一个 dxdiag 字段当结论 |
| 7 | AMD 软件、Anti-Lag、DVR、着色器缓存 | AMD 软件 hook 坏了 | 杀进程、清缓存后照样挂 | 先证明“是不是 AMD 软件”，用干净进程/新 exe 名对照 |
| 8 | `KMD_EnableDisplayableSupport`、`KMD_EnableDesktopTexture` | KMD 参数被改坏 | 两个都是 INF 默认/正常调优值；改来改去 D3D9 照样挂（已恢复原值） | 改 KMD 值前先查 INF 默认值；别把 D3D12 字段当 D3D9 病因 |
| 9 | 换 26.5.2 ↔ 2022 驱动、DDU、Windows Update 驱动 | 驱动版本坏了 | 能玩时和现在都是 26.5.2；开关在驱动之外，换驱动不会清 | **换驱动前先查全局开关** |
| 10 | 只删了 64 位 `EmulationOnly` | 应该修好了 | 还有 `Direct3D\Drivers\SoftwareOnly`（两个视图） | 两个开关、两个视图、两个子键都要查 |
| 11 | 查 `HKLM\SOFTWARE\Microsoft\Direct3D` 根键 | 没有 SoftwareOnly，排除 | 开关在 **`Direct3D\Drivers` 子键** | 查子键；`reg query ... /s` 或 Procmon |
| 12 | 远程桌面/终端服务检查 | 会不会是 RDP 禁用 D3D9 | `TerminalServerSession=False`，不是 | 检查一次即可，别反复 |

## 4. 决定性方法：Procmon 看注册表读

```powershell
# 1) 启动 Procmon 抓 9 秒
Start-Process 'D:\Temp\procmon\Procmon64.exe' -ArgumentList '/AcceptEula','/Quiet','/Minimized','/BackingFile','D:\Temp\procmon\d3d9.pml','/Runtime','9'
Start-Sleep 3
& 'D:\AI\legacy-game-compat\Games\War3\tools\d3ddev.exe'   # 真实窗口探针
Start-Sleep 9
# 2) 导出 CSV 后只看 d3ddev.exe 的 Direct3D/DirectDraw/Video 读
```

**为什么是 Procmon**：D3D9 的 HRESULT 只有一个 `0x8876086A`，ETW/D3D9 提供者看不到有效原因，
驱动日志也没有；但**运行时在返回前必然读注册表开关**，Procmon 能直接把那一行抓出来。
这是本次唯一“一锤定音”的工具。

## 5. 修复与验证

**修复**：`scripts\修复本机D3D9.bat`（备份 + 删 6 个位置的值 + 跑 32 位探针）。
手工等价操作：删第 0 节列出的四个值；不需要重启。

**验证矩阵**（全部实测）：

| 测试 | 修复前 | 修复后 |
|---|---|---|
| `D3D9Probe32.exe` 窗口 | `0x8876086A` | `0x00000000` |
| `D3D9Probe32.exe` 全屏 1920×1080@60 | `0x8876086A` | `0x00000000` |
| 64 位真实窗口 `CreateDevice` | `0x8876086A` | `0x00000000` |
| `D3D9On12` `CreateDevice` | `0x8876086A` | `0x00000000` |
| War3 主目录（撤 DXVK、恢复原版） | 弹“不能初始化DirectX” | 原生 1920×1080 主菜单（`evidence\war3-main-fixed.png`） |
| War3 干净副本 | 同样报错 | 原生 1920×1080 主菜单（`evidence\war3-native-fixed.png`） |
| D3D11 / D3D12 / Vulkan | 正常 | 正常（本来就不是它们的问题） |

**收尾**：
- `D:\Games\War3` 的 DXVK `d3d9.dll` / `dxvk.conf` 已删除，`_dxvk\` 留作其他机器的备选；
- War3 的 AppCompat `DWM8And16BitMitigation` 已移除（备份 `evidence\layers-before.reg`）；
- KMD 值已恢复原值（`DesktopTexture=1`、`DisplayableSupport=0`）；
- Windows Update 已暂停、禁止自动装驱动（`scripts\暂停Windows更新.ps1` / `恢复Windows更新.bat`）。

## 6. 排查原则（写进肌肉记忆）

1. **先分清“单游戏”还是“整机”**：所有 D3D9 同时挂、D3D11/12/Vulkan 正常 = 整机 D3D9 专用路径，
   不是显卡坏、不是驱动版本、不是游戏文件。
2. **先查全局开关**：`Direct3D\Drivers\SoftwareOnly` + `DirectDraw\EmulationOnly`，**64 位 + WOW6432Node**。
   `scripts\修复本机D3D9.bat` 就是干这个的。
3. **D3D9On12 一起挂** = 运行时级开关，别再往 AMD UMD 里钻。
4. **探针可信度优先于结论**：真实窗口、正确 vtable、顺序调用、32/64 位各跑一遍；
   探针自身有 bug 时，后面所有推理都是错的。
5. **不要用“重装/换驱动/装更新/装运行库”当排查手段**：它们会改变状态、掩盖根因，
   还会把本来能用的驱动换坏（本次 Windows Update 就把 26.5.2 来回降级了 6 次）。
6. **层是最后手段**：原生路径没证明之前，DXVK/dgVoodoo 只会把问题藏起来；
   先拿探针证明原生不可用，再选层。
7. **每个结论都要有证据**：游戏自己的错误原文、探针 HRESULT、Procmon 行、截图；
   不接受“应该是/可能是”。

> **这轮排查最关键的不是工具，而是三条硬约束：不许重装系统、不许装更新、不许拿驱动/显卡当结论。**
> 如果允许重装，开关会被“碰巧”清掉，但根因永远学不到；正是这三条把方向从换驱动逼回了注册表。
> 这条也写进协作规范：**用户的“不许”不是障碍，是诊断边界，先在这个边界内找证据。**

## 7. 相关文件

- 工具：`scripts\修复本机D3D9.bat`、`scripts\D3D9Probe32.exe`、`scripts\暂停Windows更新.ps1`、`scripts\恢复Windows更新.bat`
- 复盘证据：`Games\War3\evidence\`（`dxdiag.txt`、`war3-main-fixed.png`、`war3-native-fixed.png`、
  `direct3d64-before.reg`、`direct3d32-before.reg`、`display-class-before.reg`、`layers-before.reg`）
- 案例：`references/cases.md` 第 8 节
- 踩坑：`references/pitfalls.md` #49–#53
- 工作流：`workflow/adapt.ps1` 的全局开关预检（发现开关会停止并提示先修复）
