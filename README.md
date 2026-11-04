# Legacy Game Compat Skill

A portable, "green" (no-install, nothing written outside the game folder) playbook + toolchain for
making old PC games run on modern Windows and modern GPUs.

[中文说明见下](#中文)

> **Usage model:** this repo is an *agent skill*. Install it into your AI IDE's skills folder
> (Qoder / Claude Code / any Agent-Skills-compatible tool) and just describe the problem —
> the agent runs the probe, picks the layer, installs it and verifies.
> The `.bat` / `.ps1` entry points are for people working without an AI IDE.
> See **[INSTALL.md](INSTALL.md)**.

Most 2000–2010 games fail on current hardware for one of a few reasons: the driver no longer provides a
working D3D8/D3D9 device, DirectDraw is emulated poorly, the game only offers 4:3 resolutions, or the
required backwards-compat runtime is missing. This repo turns that diagnosis into a repeatable workflow:

**probe → pick a translation layer → drop it into the game folder → verify → keep a one-click rollback.**

No driver installs. No system-wide changes. Copy a folder, double-click a `.bat`, play.

## Contents

| Path | What it is |
|---|---|
| `SKILL.md` | The skill itself — workflow, decision table, rules, pitfalls |
| `references/` | Deep dives: diagnosis, DXVK, dgVoodoo2, resolution, real cases |
| `scripts/D3D9Probe.cs` | Tiny D3D9 device probe — tells you whether native D3D9 works at all |
| `scripts/FixGame.cs` | Auto-detect (bitness + graphics API) and install/undo the right layer |
| `templates/` | `game_starter.bat` / `game_starter-loader.bat`, `Launcher.cs` (.NET WinForms), fix/restore `.bat` templates |
| `workflow/` | `adapt.ps1` / `adapt.bat` — one-command pipeline: probe → pick layer → install → verify → report |
| `INSTALL.md` | How to install it as a skill in Qoder / Claude Code / Cursor, and how to use it without an AI |
| `tools/dxvk/` | DXVK 2.5.3 binaries (x86 / x64) |
| `tools/dgvoodoo/` | dgVoodoo2 2.87.6 binaries (x86 / x64) + control panel |
| `tools/nircmd/` | NirCmd (window centering for the .bat launcher) |

## One command

```bat
workflow\adapt.bat -GameDir "D:\Games\SomeOldGame" -Title "Game Title"
```

Probes the machine, picks the layer, installs it with backups, generates the launcher,
takes a screenshot to verify, and writes `_兼容报告.md`. Add `-DryRun` to look before it touches anything.

## Quick start

```bat
:: 1. Is native D3D9 even working?  (build with -platform:x86)
csc.exe -platform:x86 -out:D3D9Probe32.exe scripts\D3D9Probe.cs
D3D9Probe32.exe

:: 2. Install the right layer into a game folder
FixGame.exe "D:\Games\SomeOldGame"          :: auto
FixGame.exe "D:\Games\SomeOldGame" /dgv     :: force dgVoodoo2
FixGame.exe "D:\Games\SomeOldGame" /undo    :: rollback
FixGame.exe "D:\Games\SomeOldGame" /dry     :: dry run
```

Then double-click `game_starter.bat` in the game folder.

## Decision table

| Game API | First choice | Fallback |
|---|---|---|
| D3D9 | Native D3D9 (probe returns 0) | DXVK 2.5.3 / 2.3.1 (`d3d9.dll`); dgVoodoo2 |
| D3D8 | DXVK 2.5.3 (`d3d8.dll` + `d3d9.dll`) | dgVoodoo2 D3D8 |
| DirectDraw 1–7 | cnc-ddraw | dgVoodoo2 DDraw |
| OpenGL (no HW GL) | Mesa Gallium (`opengl32.dll` + `libgallium_wgl.dll` + `dxil.dll`) | — |

DXVK versions are scene-sensitive: a game that white-screens on 2.3.1 may be perfect on 2.5.3 (and vice
versa). Keep both binaries around. On this machine dgVoodoo2's D3D11/D3D12 backends are unusable, so
layered D3D9 games use DXVK (Vulkan); re-probe before applying that conclusion to another machine.
DXVK deliveries show FPS only (`dxvk.hud = fps`) so players can confirm the layer is active without
version/API clutter; version/API HUD is enabled temporarily for diagnostics only.
Native games do not run DXVK and do not show this HUD.

## Verified cases

| Game | Problem | Fix |
|---|---|---|
| Delta Force: Black Hawk Down (2003) | `Resolution test has failed` — no D3D8 device | DXVK 2.5.3 `d3d8.dll` + `d3d9.dll` |
| The Saboteur (2009) | `D3DERR_NOTAVAILABLE` on `CreateDevice` | DXVK 2.5.3 (2.3.1 white-screens) |
| Prototype 2 (2012) | dgVoodoo2: magenta UI / vertex-buffer assert | DXVK 2.3.1 |
| Prototype (2009) | 旧引擎多核启动崩溃 | DXVK + 0x3ff 亲和性 |
| Ghost Recon Future Soldier (2012) | go.bat XP/RUNASADMIN; DX11 native only on dev PC | DX11 native default + DX9/DXVK fallback; launcher + revert package; trainer blocked by VBS |
| Counter-Strike 1.6 | No hardware OpenGL (GDI 1.1 only) | Mesa Gallium → D3D12 |
| Heroes of Might and Magic III (1999) | DirectDraw display/compat issues | cnc-ddraw 7.1 GDI presets (full/4:3/window/640) + launcher/revert package |
| Just Cause (2006) | CJK glyph boxes / crash; registry fix missing | DXVK 2.5.3 + trimmed text ASI; launcher/bat auto-write 32-bit Eidos registry |
| C&C Generals Zero Hour: FiveStar (2003 mod) | Native D3D8 fails; original keygen/graphics tools required | DXVK 2.5.3; launcher reimplements 20-digit registration and Options.ini quality/FPS unlock |

Details, including the exact error logs and probe output, are in `references/cases.md`.

## Tools and licenses

- DXVK — zlib license, https://github.com/doitsujin/dxvk
- dgVoodoo2 — freeware by Dege, https://dege.freeweb.hu/dgVoodoo2/
- cnc-ddraw — MIT, https://github.com/FunkyFr3sh/cnc-ddraw (bundled with the Heroes III package)
- NirCmd — freeware by Nir Sofer, https://www.nirsoft.net/utils/nircmd.html
- Mesa — MIT, https://mesa3d.org/

Bundled third-party binaries are unmodified. See `NOTICE.md`.

## Disclaimer

For single-player / LAN use. Do **not** use translation layers with anti-cheat protected online games —
you will get banned. Redistribute at your own risk; all third-party tools remain under their own licenses.

## 中文

把老游戏（D3D8 / D3D9 / DirectDraw / OpenGL）跑在新系统和高端显卡上的**绿色**适配流程：

1. **先插探针**（`scripts/D3D9Probe.cs`）确认原生 D3D 能不能建设备，别靠猜；
2. 按决策表选翻译层（DXVK / dgVoodoo2 / cnc-ddraw / Mesa）；
3. 全部落在游戏目录：`d3d8.dll`/`d3d9.dll` + `_dxvk\` 留底 + `兼容修复.bat`/`恢复原版.bat`；
4. 用游戏自带的分辨率测试、截图、DXVK HUD 做**客观验证**；
5. 分辨率：DXVK 不能强制，dgVoodoo2 的 Force resolution 才能；老引擎只有 4:3 档时按最高档全屏 + 显卡缩放；
6. 玩家界面说人话：只保留 `DX11（画质更好）`、`DX9（老机器）`、`GDI（软件画面）`
   这类结果标注，不写「最稳」「最兼容」这种保证词；层名、注册表、亲和性、DLL 文件名等技术细节只写进运行日志和仓库文档。

实战踩坑都写在 `SKILL.md` 和 `references/` 里了。

## 推送更新

双击仓库根目录的 `push-to-github.bat`：它会自动暂存、提交并推送到
<https://github.com/NemecSoft/legacy-game-compat>。本地 `Games/*/backup/` 里的游戏原始备份
已被 `.gitignore` 排除，不会上传。

仓库内所有文本文件统一 CRLF（`\r\n`），由根目录 `.gitattributes` 强制；图片、exe、dll 等二进制
不做换行转换。脚本 `scripts/normalize-crlf.ps1` 可在提交前批量校正。
