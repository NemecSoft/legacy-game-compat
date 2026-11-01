# DXVK

D3D8/9/10/11 → Vulkan。Windows 上直接丢 DLL 就能用（不需要 Wine）。

## 装法

| 游戏 API | 放进游戏目录的文件 |
|---|---|
| D3D9 | `d3d9.dll` |
| D3D8 | `d3d8.dll` + `d3d9.dll` |
| D3D11 | `d3d11.dll` + `dxgi.dll` |

- 位数必须匹配：32 位游戏用 `x86`，64 位用 `x64`。
- 首次运行会在游戏目录生成 `<exe>_d3d9.log`（DXVK 日志），排查先看它。
- 需要可用的 Vulkan 驱动；进游戏后可用 HUD 验证。

## HUD（验证后端 / 测试水印）

`dxvk.conf`：

```ini
dxvk.hud = version,api,fps
```

左上角会显示 `DXVK v2.5.3` / `D3D9` / `FPS`。可选字段：`version api fps frametimes memory gpuload
devinfo drawcalls compiler`。

**取证只用 `version,api,fps`**：`devinfo` 显示的是显卡型号（如 RX 580），不能证明走的是兼容层；
玩家/审核看 HUD 要先看到 `DXVK vX.Y.Z`。

**HUD 只用于测试**：它证明 DXVK 真的在用、走的是哪个 API、帧率多少；截图存 `Games\<游戏>\evidence\`。
交付时删掉这一行（或注释），**不要让 HUD 出现在玩家画面里**。

## 作为 cnc-ddraw 的后端（DirectDraw 游戏）

纯 DirectDraw 游戏不能直接上 `d3d9.dll`，但可以用 cnc-ddraw 的 D3D9 渲染器：

1. 游戏目录放 cnc-ddraw 的 `ddraw.dll` + `ddraw.ini`；
2. 再放 DXVK 的 `d3d9.dll`；
3. `ddraw.ini` 写 `renderer=direct3d9`（或 `auto`，有 DXVK 时会选 D3D9）；
4. `dxvk.conf` 按需配置，测试时开 HUD，交付关闭。

实测 RoadRash：`renderer=auto` 自动加载同目录 DXVK，HUD 显示 `DXVK v2.5.3 / D3D9 / 60FPS`；
无 Vulkan 的机器回退 cnc-ddraw GDI。DXVK + 640×480 组合在该游戏开场黑屏 8~16 秒，
1080p 输出正常；遇到类似情况先换输出分辨率，再怀疑层。

## 常用配置

```ini
# 垂直同步（1 = 跟随显示器刷新，0 = 不限制）
d3d9.presentInterval = 1

# 延迟换链创建，少数老游戏窗口未就绪就建设备时需要
d3d9.deferSurfaceCreation = True

# 强制报告给游戏的宽高比（部分宽屏 hack 用）
d3d9.forceAspectRatio = 16:9

# 帧率上限
d3d9.maxFrameRate = 144
```

注意：**DXVK 没有任何"强制分辨率"的选项**。分辨率永远由游戏决定。

## 版本坑（实战）

- **The Saboteur**：2.3.1 白屏 → **2.5.3 正常**。
- **Prototype 2**：2.3.1 正常；且本机 AMD 驱动缺 `VK_KHR_load_store_op_none`，DXVK 3.x 报
  `No adapters found`。
- **RoadRash（cnc-ddraw 后端）**：640×480 + DXVK 开场黑屏 8~16 秒；1080p + DXVK 正常。
- 结论：仓库里至少留两个版本，出事就换。白屏/黑屏先换版本，再查别的。

## 日志判读

`<exe>_d3d9.log` 里：

- 只有 device feature 列表、没有 `ResetSwapChain` → 游戏在"建设备后、首次 Present 前"卡住。
- `Device properties: <GPU 名>` 正确出现 = Vulkan 真的用上了显卡。
- `D3D9DeviceEx::ResetSwapChain` 里有 `Width/Height/Windowed` → 就是游戏实际用的分辨率和显示模式。
