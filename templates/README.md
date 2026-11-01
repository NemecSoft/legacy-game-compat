# 模板说明

这些 `.bat` 是**模板**，仓库里保持 UTF-8 便于阅读；生成到目标机器时请按目标代码页保存：

- 简体中文 Windows：**GBK / CP936 + CRLF**
- 其他语言：对应 ANSI 代码页 + CRLF

否则 `cmd` 会把中文读成乱码，甚至影响命令解析。

## 占位符

| 占位符 | 含义 | 例 |
|---|---|---|
| `{{GAME_NAME}}` | 窗口标题 / 说明里的中文名 | 死亡阴影 |
| `{{GAME_NAME_EN}}` | 英文名（副标题行） | The Saboteur |
| `{{LAYER_DESC}}` | 画面后端说明 | DXVK 2.5.3（D3D9 转 Vulkan） |
| `{{EXTRA_HINT}}` | 额外提示行 | 分辨率在 OPTIONS 里改 |
| `{{GAME_EXE}}` | 直接启动的游戏 exe（阻塞） | Saboteur.exe |
| `{{LOADER_EXE}}` | 启动后自己退出的外挂启动器 | 汉化外挂.exe |
| `{{PROC_NAME}}` | 实际游戏进程名前缀（外挂模式等待用） | dfbhd |
| `{{LAYER_DLL}}` | 翻译层主 DLL | d3d9.dll |
| `{{LAYER_NAME}}` | 层名称（提示文字） | DXVK 2.5.3 |
| `{{BACKUP_SUFFIX}}` | 原文件备份后缀 | reshade-bak |

## 三个文件的分工

- `game_starter.bat` / `game_starter-loader.bat`：启动器。前者直接阻塞调用游戏；后者用于"启动器拉起游戏后自己退出"的
  外挂型游戏（汉化补丁等）。
- `兼容修复.bat`：从 `_dxvk\` 复制 DLL 上来，遇到已有同名 DLL 先改名备份。
- `恢复原版.bat`：删掉翻译层并还原备份。

## 约定

- 不用 `:标签` / `goto` / `pause`（`兼容修复`/`恢复原版` 末尾的 pause 是有意的，供手工双击查看结果）。
- 启动器最后一行直接写 exe，让窗口随游戏退出而关闭。
- 不要在 bat 里写复杂 PowerShell；需要等待时只用一个最简循环。

## 启动器选择

| 文件 | 用途 |
|---|---|
| `game_starter.bat` | 直接启动游戏 exe（阻塞），带标题/居中/提示 |
| `game_starter-loader.bat` | 外挂型游戏（汉化外挂拉起游戏后自己退出） |
| `Launcher.cs` | .NET Framework WinForms 启动器源码，配置读同目录 `launcher.ini` |
| `launcher.ini.sample` | exe 启动器配置示例（改名 `launcher.ini`） |

`Launcher.exe` 已预编译在 `templates/`，重新编译：

```bat
csc -nologo -target:winexe -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:Launcher.exe Launcher.cs
```

**交付到游戏目录时统一改名 `game_starter.exe`**（`workflow\adapt.ps1` 就是这么复制的）——
所有游戏的启动器同名，平台进程树里靠路径区分，别用 `go.exe`（与 Go 工具链同名）。
新游戏的界面版启动器不要用这份模板，照技能 `.qoder\skills\legacy-launcher-gui` 或
直接抄 `Games\RoadRash\src\Launcher.cs`。

bat 居中依赖仓库自带的 tools\nircmd\nircmdc.exe，交付时放到游戏目录 _tools\nircmd\。

规范细节见 [`../references/launchers.md`](../references/launchers.md)。
