# 诊断

## 0. 整机 D3D9 全挂：先查全局软件渲染开关（30 秒）

**信号**：所有 D3D9 游戏同时报“不能初始化DirectX”/`0x8876086A`；`dxdiag` 里 `D3D Status: Disabled`；
`D3D9On12` 也失败；但同机 D3D11/12/Vulkan 正常；换驱动、DDU、重启、装 DirectX 运行库都无效。

**处理**：跑 `scripts\修复本机D3D9.bat`；它备份并删除
`HKLM\SOFTWARE\Microsoft\Direct3D\Drivers\SoftwareOnly` 和
`HKLM\SOFTWARE\Microsoft\DirectDraw\EmulationOnly`（**64 位 + WOW6432Node 两个视图**），
然后自动用 32 位探针复测。**不需要重启**。**先清开关，再谈驱动/翻译层。**

**完整复盘**：`references/d3d9-global-failure.md`（死胡同、探针假象、Procmon 证据链）。

## 1. 先抓现场，不要猜

| 现象 | 抓什么 |
|---|---|
| 进程秒退、无提示 | 截图（可能弹过框）、事件查看器 `Application` 日志、游戏自己的 log |
| 弹框（分辨率测试失败等） | 截图 + 游戏版本号 |
| 黑屏/白屏但进程在 | 是否已创建窗口、窗口矩形、CPU 占用、DXVK/ReShade 日志 |
| 花屏/洋红 | 截图 + 当前用的是哪一层 |

常用命令：

```powershell
# 进程与窗口
Get-Process <name> | Select Name,Id,MainWindowTitle,Responding

# 显示模式（游戏切了分辨率会变）
(Get-CimInstance Win32_VideoController | ? Name -match 'Radeon|NVIDIA|Intel').VideoModeDescription

# 崩溃事件
Get-WinEvent -LogName Application -MaxEvents 50 |
  ? { $_.TimeCreated -gt (Get-Date).AddMinutes(-10) -and $_.LevelDisplayName -ne '信息' }
```

若游戏目录里有 ReShade 的 `d3d9.log` / DXVK 的 `<exe>_d3d9.log`，**优先读**——里面有
`CreateDevice` 的完整呈现参数和 HRESULT，比猜快十倍。

## 2. 探针（决定性证据）

`scripts/D3D9Probe.cs`：直接调 `Direct3DCreate9` + 每个适配器的 `CreateDevice`，打印 HRESULT。

```bat
csc.exe -platform:x86 -out:D3D9Probe32.exe D3D9Probe.cs
D3D9Probe32.exe
```

- 必须用 **32 位**编译（绝大多数老游戏是 32 位，x86 包装 DLL 才能被加载；64 位探针会忽略同目录的 32 位 DLL）。
- 探针测三种请求：全屏 `0x54`（硬件顶点+PURE+多线程，老游戏常用）、全屏 `0x44`、窗口模式。
- 输出 `adapters: N` 可与 `Win32_VideoController` 对照（虚拟显示器/远程桌面适配器常导致适配器 0 不可用）。

判定：

| HRESULT | 含义 |
|---|---|
| `0x00000000` | 成功 |
| `0x8876086A` `D3DERR_NOTAVAILABLE` | 该适配器/该模式不可用 |
| `0x8876086C` `D3DERR_INVALIDCALL` | 参数或适配器无效 |
| `0x8876086D` `D3DERR_OUTOFVIDEOMEMORY` | 显存/后端异常 |

**关键用法**：把候选包装 DLL 放在探针同目录再跑一次。探针成功 = 这层能替代原生；仍失败 = 换层。

### DirectDraw 游戏（D3D9 探针不适用）

先看导入表：只有 `DDRAW.dll -> DirectDrawCreate`，没有 d3d8/d3d9/opengl32，就是纯 DirectDraw 游戏。
典型症状是弹“未安装 DirectX 驱动或版本太低”、花屏、或时好时坏。**不要用 D3D9 探针下结论，
也不要装 DX9 运行库**；直接上 **cnc-ddraw**（`ddraw.dll` + `ddraw.ini`），再用它的
GDI 软件警告 / DXVK HUD 验证层是否生效（见 `launchers.md` 水印验证）。

## 3. 判定流程

```
游戏报错/黑屏
   ├─ 只导入 DDRAW.dll（纯 DirectDraw）──> cnc-ddraw → 不行再 dgVoodoo DDraw
   ├─ 探针原生全挂 ──> 上 DXVK（首选）→ 不行换 dgVoodoo → 再不行确认 Vulkan 是否可用
   ├─ 原生正常     ──> 不是 API 问题：查运行库(VC++/DirectX9)、兼容模式、CPU/内存限制、配置
   └─ 只有某分辨率/全屏失败 ──> 分辨率问题，见 resolution.md
```

## 4. 常见"看起来像图形问题其实不是"

- 缺 `d3dx9_*.dll` / VC++ 运行库 → 弹 DLL 缺失，装 DirectX 9.0c 运行时。
- 免 CD / 光盘检测 → 进程静默退出，无窗口。
- 汉化外挂没挂上 → 中文乱码，但游戏本身正常。
- 游戏只支持 4:3 → 画面变形/黑边，不是崩溃。
