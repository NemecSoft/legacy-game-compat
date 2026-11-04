# -*- coding: utf-8 -*-
# 显示方式改名（用户选定 B 套：大白话 + 保留技术名），同步界面 / bat / 配置文案 / 文档 / 规范
import io

B = r'D:\AI\legacy-game-compat\Games\Heroes3'
S = r'D:\AI\legacy-game-compat\.qoder\skills\legacy-launcher-gui\SKILL.md'
R = r'D:\AI\legacy-game-compat\references'


def load(p):
    return io.open(p, encoding='utf-8', newline='').read().replace('\r\n', '\n')


def save(p, t):
    io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))


def sub(t, a, b, tag):
    assert a in t, tag
    return t.replace(a, b, 1)


# ---------- 1) Launcher.cs ----------
p = B + r'\src\Launcher.cs'
t = load(p)
old = '''        "1  兼容模式（cnc-ddraw GDI，全屏）",
        "2  兼容模式（cnc-ddraw GDI，保持 4:3）",
        "3  兼容模式（cnc-ddraw GDI，窗口 1280×720）",
        "4  原版画面（cnc-ddraw GDI，640×480 窗口）",
        "5  加速模式（cnc-ddraw DirectX9，全屏）",
        "6  兼容模式（dgVoodoo2，窗口）"'''
new = '''        "1  软件画面 GDI（最稳·全屏）",
        "2  软件画面 GDI（4:3黑边）",
        "3  软件画面 GDI（窗口1280×720）",
        "4  原版640×480（窗口）",
        "5  DX9硬件加速（全屏）",
        "6  dgVoodoo2转换（窗口）"'''
t = sub(t, old, new, 'ModeNames')
save(p, t)
print('Launcher.cs 改名完成')

# ---------- 2) game_starter.bat ----------
p = B + r'\src\game_starter.bat'
t = load(p)
old = '''  echo       1  兼容模式（cnc-ddraw GDI，全屏）
  echo       2  兼容模式（cnc-ddraw GDI，保持 4:3）
  echo       3  兼容模式（cnc-ddraw GDI，窗口 1280×720）
  echo       4  原版画面（cnc-ddraw GDI，640×480 窗口）
  echo       5  加速模式（cnc-ddraw DirectX9，全屏）
  echo       6  兼容模式（dgVoodoo2，窗口）'''
new = '''  echo       1  软件画面 GDI（最稳·全屏）
  echo       2  软件画面 GDI（4:3黑边）
  echo       3  软件画面 GDI（窗口1280×720）
  echo       4  原版640×480（窗口）
  echo       5  DX9硬件加速（全屏）
  echo       6  dgVoodoo2转换（窗口）'''
t = sub(t, old, new, 'bat 菜单')
t = sub(t, '  echo     默认走 cnc-ddraw GDI（最兼容，画面最稳）。',
        '  echo     默认走软件画面 GDI（最兼容，画面最稳）。', 'bat 说明行')
save(p, t)
print('game_starter.bat 改名完成')

# ---------- 3) launcher.ini 文案 ----------
p = B + r'\src\launcher.ini'
t = load(p)
t = sub(t, r'EntryGuide=启动后先等加载（第一次较慢）\n想换窗口用启动器的「窗口模式」\n画面异常换「保持 4:3」或「窗口」',
        r'EntryGuide=启动后先等加载（第一次较慢）\n想切窗口选「软件画面 GDI（窗口1280×720）」\n画面异常先回第 1 档，再试 4:3 或窗口',
        'EntryGuide')
t = sub(t, r'Tips=# 提示\n默认 GDI（最兼容），画面最稳\n黑屏/花屏时优先选 GDI 稳定全屏或窗口\n其他画面方式在本游戏容易出问题，别手动改',
        r'Tips=# 提示\n默认「软件画面 GDI」最兼容（CPU 画图，不碰显卡 3D 驱动）\n黑屏/花屏先回第 1 档，再试窗口档\nDX9硬件加速、dgVoodoo2转换是备选，能用就别常换',
        'Tips')
t = sub(t, r'CenterNote=默认「稳定全屏（GDI）」最兼容；\n人物被拉胖选「保持 4:3」，\n黑屏/切屏问题选「窗口模式」，\n老机器想原汁原味\n选「原版 640×480（窗口）」。',
        r'CenterNote=默认「软件画面 GDI（最稳·全屏）」最兼容；\n人物被拉胖选「软件画面 GDI（4:3黑边）」，\n要切窗口/Alt-Tab 选「软件画面 GDI（窗口1280×720）」，\n想看老味道选「原版640×480（窗口）」，\n想交给显卡画选「DX9硬件加速（全屏）」，\n前面都不行再试「dgVoodoo2转换（窗口）」。',
        'CenterNote')
save(p, t)
print('launcher.ini 文案同步完成')

# ---------- 4) 适配过程分析.md ----------
p = B + r'\适配过程分析.md'
t = load(p)
old = '''| 1 | 兼容模式（cnc-ddraw GDI，全屏） | `_cnc-ddraw\\ddraw.dll` + `_presets\\ddraw.ini` | **默认最稳**：Windows 自带 2D 画图，不碰显卡 3D 驱动 |
| 2 | 兼容模式（cnc-ddraw GDI，保持 4:3） | `_presets\\ddraw-43.ini` | `maintas=true`，人物不被拉胖 |
| 3 | 兼容模式（cnc-ddraw GDI，窗口 1280×720） | `_presets\\ddraw-window.ini` | 方便 Alt-Tab，躲开 HDR/多显示器/覆盖层 |
| 4 | 原版画面（cnc-ddraw GDI，640×480 窗口） | `_presets\\ddraw-640.ini` | 原汁原味 |
| 5 | 加速模式（cnc-ddraw DirectX9，全屏） | `_presets\\ddraw-d3d9.ini` | 同样经 cnc-ddraw，但把游戏的 Blt 交给显卡 D3D9 画（GDI 太慢/太糊时试）；**不叠 DXVK** |
| 6 | 兼容模式（dgVoodoo2，窗口） | `_dgvoodoo2\\ddraw.dll` + `_presets\\dgVoodoo-window.conf` | 换一层转译（输出走 D3D11），GDI 那几条都不灵时用 |'''
new = '''| 1 | 软件画面 GDI（最稳·全屏） | `_cnc-ddraw\\ddraw.dll` + `_presets\\ddraw.ini` | **默认最稳**：Windows 自带 2D 画图（CPU），不碰显卡 3D 驱动 |
| 2 | 软件画面 GDI（4:3黑边） | `_presets\\ddraw-43.ini` | `maintas=true`，人物不被拉胖 |
| 3 | 软件画面 GDI（窗口1280×720） | `_presets\\ddraw-window.ini` | 方便 Alt-Tab，躲开 HDR/多显示器/覆盖层 |
| 4 | 原版640×480（窗口） | `_presets\\ddraw-640.ini` | 原汁原味 |
| 5 | DX9硬件加速（全屏） | `_presets\\ddraw-d3d9.ini` | 同样经 cnc-ddraw，但把游戏的 Blt 交给显卡 D3D9 画（GDI 太慢/太糊时试）；**不叠 DXVK** |
| 6 | dgVoodoo2转换（窗口） | `_dgvoodoo2\\ddraw.dll` + `_presets\\dgVoodoo-window.conf` | 换一层转译（输出走 D3D11），GDI 那几条都不灵时用 |

命名是用户从四套方案里选的 B 套：**大白话说清「这档干什么」，括号/后缀保留技术名（GDI / DX9 / dgVoodoo2）方便远程核对**。
反例：我先前自创的「稳定全屏」「直连模式」「加速模式」——看不出走什么路，已被否掉。'''
t = sub(t, old, new, '模式表')
t = sub(t, '- **显示方式（启动器六选一，2026-11-04 改成「模式（层+渲染）」命名）**：',
        '- **显示方式（启动器六选一，2026-11-04 定名：大白话 + 技术名）**：', '旧标题') if '- **显示方式（启动器六选一' in t else t
save(p, t)
print('适配过程分析.md 改名完成')

# ---------- 5) cases.md ----------
p = R + r'\cases.md'
t = load(p)
old = '''  1 兼容模式（cnc-ddraw GDI，全屏）/ 2 兼容模式（cnc-ddraw GDI，保持 4:3）/
  3 兼容模式（cnc-ddraw GDI，窗口 1280×720）/ 4 原版画面（cnc-ddraw GDI，640×480 窗口）/
  5 加速模式（cnc-ddraw DirectX9，全屏）/ 6 兼容模式（dgVoodoo2，窗口）。'''
new = '''  1 软件画面 GDI（最稳·全屏）/ 2 软件画面 GDI（4:3黑边）/ 3 软件画面 GDI（窗口1280×720）/
  4 原版640×480（窗口）/ 5 DX9硬件加速（全屏）/ 6 dgVoodoo2转换（窗口）。
  名字用「大白话 + 技术名」两套信息并存：前半句说这档干什么，后半句留 GDI/DX9/dgVoodoo2 便于远程核对；
  这套是给用户从四个候选里挑的（用户否掉了「稳定全屏」「直连模式」这类自创词）。'''
t = sub(t, old, new, 'cases 模式行')
save(p, t)
print('cases.md 改名完成')

# ---------- 6) 技能规范：命名规则 ----------
p = S
t = load(p)
old = '''- 选项文字本身要短到能看懂：显示方式一律写成「**模式（层 + 渲染）**」，
  如 `兼容模式（cnc-ddraw GDI，全屏）` / `加速模式（cnc-ddraw DirectX9，全屏）` /
  `兼容模式（dgVoodoo2，窗口）`；不要用「稳定 / 直连」这种看不出走什么的词，
  也不要给玩家看不懂的 `auto` / `config` / `重置`。'''
new = '''### 选项名字怎么起（用户定案，别再自创词）

**大白话说清「这档干什么」+ 保留技术名（GDI / DX9 / dgVoodoo2）便于远程核对**，例如：

```
1  软件画面 GDI（最稳·全屏）        ← CPU 用 Windows 自带 2D 画，不碰显卡 3D 驱动
2  软件画面 GDI（4:3黑边）
3  软件画面 GDI（窗口1280×720）
4  原版640×480（窗口）
5  DX9硬件加速（全屏）              ← 交给显卡的 Direct3D9 画
6  dgVoodoo2转换（窗口）
```

- **不要自创抽象词**：反例「稳定全屏」「直连模式」「加速模式」——听完不知道走哪条路，用户明确否过。
- **不要给玩家看不懂的词**：`auto` / `config` / `重置` / `备份` / `兼容层` 一律不当选项名。
- **新游戏要定名字时，先给 3-4 套候选让用户挑**（可以混搭），别自己拍一套就写进界面和文档；
  定下来之后界面、`game_starter.bat` 菜单、`launcher.ini` 的 `CenterNote/Tips/EntryGuide`、
  适配文档必须一起对齐。
- 名字变长时记得回头核对弹层宽度（见上一节），别让新名字被截断。'''
t = sub(t, old, new, '命名规则')
save(p, t)
print('SKILL.md 命名规则已更新')
