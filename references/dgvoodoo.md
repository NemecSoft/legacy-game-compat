# dgVoodoo2

覆盖范围最广的老图形兼容层：Glide、DirectDraw(1-7)、D3D8、D3D9 → 输出到 D3D11 / D3D12 / WARP。

## 装法

把对应位数的 DLL 放进游戏目录（原名）：

| 游戏 API | 文件 |
|---|---|
| D3D9 | `D3D9.dll` |
| D3D8 | `D3D8.dll`（部分版本还需 `D3DImm.dll`） |
| DirectDraw ≤7 | `DDraw.dll`（+ `D3DImm.dll`） |
| Glide | `Glide.dll`/`Glide2x.dll`/`Glide3x.dll` |

配置面板 `dgVoodooCpl.exe` 需和 `dgVoodoo.conf` 放在同一目录；改完 `Apply` 写回 conf。

## 关键配置

```ini
[General]
OutputAPI = d3d11_fl11_0      ; 兼容性最好；部分老游戏要试 d3d12_fl12_0 / d3d11warp
dgVoodooWatermark = false     ; 左下角水印，验证时可以开

[DirectX]
Resolution = 1920x1080        ; 强制输出分辨率
ForceResolution = true        ; 老游戏只有 4:3 时用它铺满 16:9 屏
```

- **Force resolution 是 dgVoodoo 相对 DXVK 的唯一独占能力**：游戏内部分辨率不变，输出被强制成你要的
  尺寸。老引擎没有 16:9 档位时用这个。
- `OutputAPI` 不要乱选，默认 `bestavailable` 即可；D3D11 洋红就换 D3D12，D3D12 崩就回 D3D11。

## 已知坑

| 症状 | 原因/处理 |
|---|---|
| UI/过场洋红 | D3D11 后端对某些引擎的纹理格式处理错误 → 换 DXVK |
| 进游戏后黑屏有声 | 老版本 D3D9 实现问题 → 升级版本或换 DXVK |
| `CreateVertexBuffer` 返回 NULL 导致崩溃 | 引擎不接受 dgVoodoo 的语义 → 只能换 DXVK |
| 设备创建直接失败（`INVALIDCALL`） | 宿主机的 D3D11/D3D12 本身不可用（驱动残缺/虚拟显示器） |
| 水印 | `dgVoodooWatermark = false` |

## 什么时候首选 dgVoodoo

- DirectDraw / D3D8 老游戏且 DXVK 不可用（没有 Vulkan 驱动）。
- 需要强制输出分辨率、强制抗锯齿/各向异性。
- 需要加载 `.asi` 模组的场景（配合 DxWrapper 更合适）。
