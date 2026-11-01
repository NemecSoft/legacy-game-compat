# 安装与使用

> **不需要直接运行 bat。** 主用法是把它装进 AI IDE：AI 读到 `SKILL.md` 后自己按工作流探测、选层、
> 落地、验证。bat/ps1 只是给"没有 AI 的场景"（自己动手、给朋友、CI）留的入口。

## 三种用法

| 用法 | 适合谁 | 怎么开始 |
|---|---|---|
| **AI IDE 技能**（推荐） | 开发者 | 把仓库放进 IDE 的 skills 目录，然后用自然语言提需求 |
| **命令行工作流** | 不想用 AI / 给朋友 | 双击 `workflow\adapt.bat -GameDir "游戏目录"` |
| **脚本调用** | CI / 批量 | `powershell -File workflow\adapt.ps1 -GameDir ... -NoVerify` |

---

## 一、装进 AI IDE

仓库根目录就是技能根（`SKILL.md` 带 `name` + `description` frontmatter），整个仓库放进去即可。

### Qoder

```bat
git clone https://github.com/<you>/legacy-game-compat-skills.git "%USERPROFILE%\.qoder-cn\skills\legacy-game-compat"
```

或者已经 clone 在别处，用目录联接避免复制两份：

```bat
mklink /J "%USERPROFILE%\.qoder-cn\skills\legacy-game-compat" "D:\AI\legacy-game-compat-skills"
```

项目级安装（只对某个项目生效）：`<项目>\.qoder\skills\legacy-game-compat\`

### Claude Code

```bat
git clone https://github.com/<you>/legacy-game-compat-skills.git "%USERPROFILE%\.claude\skills\legacy-game-compat"
```

### Cursor / GitHub Copilot 等没有 skills 机制的 IDE

把 `SKILL.md` 的内容放进规则文件，并在开头加一行指路：

- Cursor：`.cursor/rules/legacy-game-compat.mdc`
- Copilot：`.github/copilot-instructions.md`

```markdown
遇到老游戏黑屏/闪退/显卡初始化失败/分辨率问题，按本规则处理。
完整资料与工具在 legacy-game-compat-skills 仓库（本机路径：D:\AI\legacy-game-compat-skills）。
优先执行：workflow\adapt.ps1 -GameDir "<游戏目录>" -DryRun，确认后再正式执行。
```

### 任意 Agent（手工方式）

把仓库 clone 到任意位置，在需求里带上路径：

> 用 `D:\AI\legacy-game-compat-skills\SKILL.md` 里的工作流，把 `D:\Games\XXX` 修到能玩。

---

## 二、装好后怎么用

直接用自然语言，不需要记命令：

- "《XX》进不去，报显卡初始化失败，用 legacy-game-compat 修一下"
- "这个老游戏黑屏，帮我适配到 Win11"
- "把 D:\Games\XXX 按老游戏兼容流程跑一遍"

AI 的正常动作序列：

1. 读 `SKILL.md` 决定走工作流；
2. 跑 `workflow\adapt.ps1 -GameDir <目录> -DryRun`，把探针结果和主程序选择给你看；
3. 正式执行（自动备份 + 装层 + 生成启动器）；
4. 看 P4 截图判断是否真的进游戏；
5. 把这次经验写进 `references/cases.md` / `pitfalls.md`。

### 验证安装成功

- Qoder / Claude Code 的技能列表里能看到 `legacy-game-compat`；
- 或直接问："用老游戏兼容技能帮我看看某游戏"，AI 能说出探针、DXVK、dgVoodoo 这些词即命中。

---

## 三、不用 AI 的人怎么用

```bat
workflow\adapt.bat -GameDir "D:\Games\SomeGame" -Title "游戏中文名"
```

- 不加参数双击 `adapt.bat` 会提示用法；
- `-DryRun` 只看不改；
- 生成物里有 `兼容修复.bat` / `恢复原版.bat`，随时回滚。

---

## 四、自己 fork / 改仓库时注意

- `SKILL.md` 必须在仓库根，frontmatter 保留 `name`、`description`（触发词写在 description 里）。
- 工具目录结构不能变：
  `tools/dxvk/{x86,x64}`、`tools/dgvoodoo/{x86,x64}`、`tools/nircmd/`。
- `workflow/adapt.ps1` 用 `$PSScriptRoot` 的上一级定位 `tools`、`templates`、`scripts`。
- `.ps1` 存 **UTF-8 带 BOM**（Windows PowerShell 5.1 才不会把中文读成乱码）；
  `.bat` 落地时转 **GBK + CRLF**。
- 仓库约 30 MB，主要是工具二进制；clone 慢可加 `--depth 1`，或走 `https://gh-proxy.com/<url>`。

## 五、更新

```bat
git pull
```

技能与工具一起更新，不用重新安装。
