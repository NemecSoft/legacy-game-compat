# 通用拟物键帽库（PC / PS / Switch / Xbox）

2026-11-04 用户定案：**所有按键，不论是手柄的还是键盘的，都要用拟物键；
PC、PS、Switch 等都要有一套完整的拟物键，直接拿来用，不许再各游戏临时画
「金色扁片 + 深色帽里写文字」这种不伦不类的东西。**

本文件是键帽的唯一规范；唯一实现是仓库 `templates\Keycaps.cs`（C# / .NET Framework 4.x
WinForms + 纯 GDI+，零图片零第三方库）。两者成对维护，改画法要一起改。

## 1. 硬规矩

1. **任何输入设备的任何按键，都必须通过 `Keycaps` 画成拟物键帽**。
   PC 键盘、PS 手柄、Switch 手柄、Xbox 手柄一视同仁；以前的
   「键帽只给真正的键盘键，鼠标/手柄用普通文字」**作废**（旧条目已删除）。
2. **一个键帽只装一个键**。多个键 = 多个独立键帽并排；组合键 = 两个键帽中间一个小 `+`。
   禁止把 `W A S D`、`↑↓←→`、`J K L 空格` 塞进同一个帽里，也禁止把 `L1+✕` 画成
   「帽子里写 L1+ 再补一个小字符」。
3. **不是按键的东西不画键帽**：「提示」「游泳」「PS5」这类是说明文字，不是键，
   用 `—` 占位、不画帽。鼠标键是真键，要画帽（`pc:Mouse1` 等）。
4. **不许在 `Launcher.cs` 里另写 `Keycap` / `PsGlyph` / `DrawFaceCircle` 之类临时画法**。
   发现重复实现直接删，统一调 `Keycaps`。
5. **所有平台共用同一套立体画法**，只是帽形与符号不同；键帽里的字仍用
   `TextRenderer`（GDI ClearType），全窗口文字渲染保持一致，不引灰度/离屏位图。
6. **左栏只写游戏说明**；表的看法写在**右栏自己头上**（一行就够），
   「PageUp / PageDown 翻页」「用滚轮滚动」这类操作提示**不要写**，玩家自己会滚。
   左栏也不再写「纯键盘总览」这种和右栏重复的清单。
7. **说明文字不是键帽位，但必须给出键盘对应**（SKILL 硬约束 19）：卡片说明、底部提醒、
   口诀里写 `R2 才是魔法`、`抓人处决看 ○` 这种句子属于叙述，不必逐字画成键帽，
   但要紧跟对应键盘键（`L2(U)`、`○，键盘按 L`、`按住再按 □△○✕，键盘 J K L 空格`），
   或整段按「手柄键帽 + 金色 PC 键帽 + 说明」一行一组排。纯手柄功能（触摸板 / SHARE /
   PS 键 / L1+○ 钩索）写明「本方案未绑定键盘键」，不许空着让人猜。

## 2. 接入（三步，直接拿来用）

1. 把 `templates\Keycaps.cs` **整份复制**到 `Games\<游戏>\src\Keycaps.cs`，不要改文件名。
2. `build.ps1` 的 `csc` 参数里，在 `Launcher.cs` 前面加上 `Keycaps.cs`：
   ```powershell
   $cscArgs += (Join-Path $Src 'Keycaps.cs')
   $cscArgs += (Join-Path $Src 'Launcher.cs')
   ```
3. 启动器构造函数里注入 DPI 与键帽字体（一次即可，令牌有默认值，宿主也可自行覆盖）：
   ```csharp
   MakeFonts();
   Keycaps.Configure(dpi, fCap, fCapZh);
   ```

API 只有四个，够用：

| 调用 | 作用 |
|---|---|
| `Keycaps.Configure(dpi, font, smallFont)` | 注入缩放与字体 |
| `Keycaps.GroupWidth(g, spec, h)` | 量一串键帽总宽，排版用 |
| `Keycaps.DrawGroup(g, spec, x, y, h, primary)` | 画一串键帽；`primary=true` 是金色玩家键 |
| `Keycaps.Draw(g, id, rect, primary)` | 画单个键帽 |

`spec` 语法：id 之间空格分隔，`+` 当独立 token，`—` 或空串 = 这一行没有这个键。

```
pc:W pc:A pc:S pc:D
ps:L1 + ps:cross
ps:square ps:triangle ps:circle ps:cross
—
```

版式建议：键帽太多时**键帽一行、说明整卡宽度另起一行**，别把说明挤到键帽右边
（实测六个键帽 + 同行说明会把字压出卡片）。

## 3. 完整键位 id 清单

id 格式 `<平台>:<键名>`。库按规则解析，没列全的普通键名也能直接当 id 用
（如 `pc:F7`、`ps:START`、`xb:RB`），只是显示原文标签。

### PC（`pc:`）

| 类别 | id | 帽上显示 |
|---|---|---|
| 字母 / 数字 | `pc:A`…`pc:Z`、`pc:0`…`pc:9` | 原字母数字，方形帽 |
| 功能键 | `pc:F1`…`pc:F12` | `F1`…`F12` |
| 修饰键 | `pc:Shift` `pc:LeftShift` `pc:RightShift` `pc:Ctrl` `pc:Alt` `pc:CapsLock` `pc:Tab` `pc:Enter` `pc:Esc` `pc:Backspace` | Shift / 左Shift / … / 退格 |
| 编辑/导航 | `pc:Insert` `pc:Delete` `pc:Home` `pc:End` `pc:PageUp` `pc:PageDown` `pc:PrintScreen` `pc:Pause` | Insert / Delete / Home / End / PgUp / PgDn / PrtSc / Pause |
| 方向 | `pc:Up` `pc:Down` `pc:Left` `pc:Right` | 拟物帽上画箭头 |
| 空格 / 符号 | `pc:Space` `pc:Minus` `pc:Plus` `pc:Comma` `pc:Period` `pc:Slash` `pc:Semicolon` `pc:BracketL` `pc:BracketR` | 空格 / `-` / `+` / … |
| 鼠标 | `pc:Mouse1` `pc:Mouse2` `pc:Mouse3` `pc:WheelUp` `pc:WheelDown` | 左键 / 右键 / 中键 / 滚轮↑ / 滚轮↓ |

### PlayStation（`ps:`）

| 类别 | id | 帽上内容 |
|---|---|---|
| 面部键 | `ps:square` `ps:triangle` `ps:cross` `ps:circle` | 官方配色 □ 粉 / △ 绿 / ✕ 蓝 / ○ 红，圆形帽 |
| 肩键 / 扳机 | `ps:L1` `ps:L2` `ps:R1` `ps:R2` | 宽矩形帽 + 文字 |
| 摇杆按下 | `ps:L3` `ps:R3` | 宽矩形帽 + 文字 |
| 摇杆 / 十字键 | `ps:stick_l` `ps:stick_r` `ps:dpad` `ps:dpad_up` `ps:dpad_down` `ps:dpad_left` `ps:dpad_right` | 双圆摇杆 / 十字形 / 单箭头 |
| 系统键 | `ps:options` `ps:share` `ps:touchpad` `ps:ps` `ps:START` `ps:SELECT` | OPTIONS / SHARE / 触摸板形状 / PS 等 |

### Nintendo Switch（`sw:`）

| 类别 | id | 帽上内容 |
|---|---|---|
| 面部键 | `sw:A` `sw:B` `sw:X` `sw:Y` | 圆形帽 + 白色字母 |
| 肩键 | `sw:L` `sw:R` `sw:ZL` `sw:ZR` `sw:SL` `sw:SR` | 宽矩形帽，Joy-Con 蓝 / 红 |
| 系统键 | `sw:plus` `sw:minus` `sw:home` `sw:capture` | `+` / `−` / HOME / 截图方框 |
| 摇杆 / 十字键 | `sw:stick_l` `sw:stick_r` `sw:dpad` | 双圆摇杆 / 十字形 |

### Xbox（`xb:`）

| 类别 | id | 帽上内容 |
|---|---|---|
| 面部键 | `xb:A` `xb:B` `xb:X` `xb:Y` | 圆形帽，官方 A 绿 / B 红 / X 蓝 / Y 黄 |
| 肩键 / 扳机 | `xb:LB` `xb:RB` `xb:LT` `xb:RT` | 宽矩形帽 + 文字 |
| 系统键 | `xb:view` `xb:menu` `xb:guide` | VIEW / MENU / XBOX |
| 摇杆 / 十字键 | `xb:stick_l` `xb:stick_r` `xb:dpad` | 双圆摇杆 / 十字形 |

## 4. 统一画法（暴力摩托基线）

`Games\RoadRash\src\Launcher.cs` 的 `Keycap` 是键盘拟物键的参考样板，所有平台共用：

| 项 | 规定 |
|---|---|
| 帽体 | 圆角矩形（键盘 / 肩键 / 系统键，半径 `S(4)`）或正圆（面部键 / 摇杆）；纯色填充 + 1px 黄铜描边 |
| 顶沿高光 | 内缩 3px、`S(3)` 高的圆角亮条，用 `CapTopHi`；这是"键帽有厚度"的关键 |
| 底部厚度 | 内缩 3px、`S(2)` 高的 `CapSide` 暗条；不能只画渐变扁片 |
| 帽上文字 | `TextRenderer` 居中，9pt Bold；长标签退回 8.5pt |
| 帽上符号 | 2px 圆头笔画，按平台官方配色；箭头 / 十字键 / 摇杆 / 触摸板各有拟物形状 |
| 尺寸 | 帽高 `S(21)`；单键最窄 `S(22)`，宽键按字宽 + `S(12)`；帽间距 `S(3)` |
| 令牌 | `CapFace #4A2A1B` / `CapTopHi #9C6A3B` / `CapSide #1C0F0A` / `PanelEdge #B98A55` / `Ink #F4E3C6` / `Gold #F0D596` / `GoldDim #9A6A2A` / `OnGold #2A160D` |

两个变体，只换色不改画法：

- `primary=true`：**金色键帽**，玩家真正要按的键盘键；深棕字（`OnGold`）。
- `primary=false`：深棕键帽，游戏画面上的手柄键，只用来对照；符号按平台官方配色。

## 5. 体积口径

2026-11-04 用户放宽：**`game_starter.exe` ≤ 1 MB 即可**，早期 60 KB 的经验值作废
（通用键帽库等标准件进来后太紧，硬压会牺牲可读性和完整平台支持）。
零图片、零内嵌字体、零第三方 DLL 仍是硬约束，体积检查只是粗筛，
真正挡资源靠 `check-launcher.ps1` 的禁止文件列表（`*.png` / `*.ico` / `*.resx` / `*.Designer.cs` 等）。
`check-launcher.ps1` 默认 `MaxKB = 1024`；已有 `build.ps1` 显式传 `-MaxKB 60` 的保持原样，
等点名那个游戏再改，别批量重编。

## 6. 交付前自检

- [ ] 界面里每一个键都是 `Keycaps` 画出来的拟物键帽，没有纯文字键名、没有扁片
- [ ] 一个帽只装一个键；多键 / 组合键是多个独立帽
- [ ] 非按键（提示文字、状态、平台名）没有被套进键帽
- [ ] 手柄键与键盘键的帽体高光/厚度一致，只有形状和颜色不同
- [ ] `src\Keycaps.cs` 与 `templates\Keycaps.cs` 字节一致，没有游戏私有改动
- [ ] `build.ps1` 的 csc 参数包含 `Keycaps.cs`，构造时调了 `Keycaps.Configure`
- [ ] 左栏没有翻页提示 / 表的用法 / 重复键位总览；右栏自己头上有一行说明
- [ ] 截图里键帽不重叠、说明文字不越出卡片、100% 与 150% 缩放各看一次
