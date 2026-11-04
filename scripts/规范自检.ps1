# 规范自检：把「靠人记住」的坑变成能跑的检查
# 用法：pwsh -NoProfile -File scripts\规范自检.ps1        （只报问题）
#      pwsh -NoProfile -File scripts\规范自检.ps1 -Quiet  （CI 风格，全过就一行）
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$issues = @()
function Add-Issue([string]$kind, [string]$where, [string]$detail) {
  $script:issues += [pscustomobject]@{ Kind = $kind; Where = $where; Detail = $detail }
}

# ---------- 1) 界面/规范禁词：保证词与自创抽象词 ----------
$banned = '最稳', '最兼容', '不会崩', '稳定全屏', '直连模式', '加速模式'
# 这些词出现在「反例 / 禁止 / 已否掉」这类句子里是合规的（规范本身要举反例）
$allowMark = '反例', '不写', '不要写', '不许', '别写', '已被否', '否掉', '否了', '别自创', '不要自创',
             '禁止', '禁词', '教训', '原来写', '坑', '不是简单', '这类自创', '这种抽象词', '保证词',
             '不要拿它', '旧文案', '定案去掉', '已按', '原来叫', '已去掉', '变成检查', '违规', '去掉'
$textFiles = Get-ChildItem $root -Recurse -File -Include *.cs, *.bat, *.ini, *.md |
  Where-Object { $_.FullName -notmatch '\\(out|release|backup|evidence|node_modules|design-demos)\\' }
foreach ($f in $textFiles) {
  $lines = [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)
  for ($i = 0; $i -lt $lines.Count; $i++) {
    foreach ($w in $banned) {
      if ($lines[$i] -notmatch [regex]::Escape($w)) { continue }
      # 看本行 + 上面 2 行：规范本身要举反例，上下文写明「禁止 / 反例 / 已去掉」就算合规
      $from = [Math]::Max(0, $i - 2)
      $ctx = ($lines[$from..$i] -join ' ')
      $ok = $false
      foreach ($m in $allowMark) { if ($ctx -match [regex]::Escape($m)) { $ok = $true } }
      if (-not $ok) {
        Add-Issue '禁词' $f.FullName ("L" + ($i + 1) + "：「" + $w + "」像是要给玩家看的保证词/自创词（规范说：是什么就是什么）")
      }
    }
  }
}

# ---------- 2) 规范令牌名与代码漂移 ----------
$skill = Join-Path $root '.qoder\skills\legacy-launcher-gui\SKILL.md'
$recipe = Join-Path $root '.qoder\skills\legacy-launcher-gui\references\leather-recipes.md'
foreach ($p in @($skill, $recipe)) {
  if (-not (Test-Path $p)) { Add-Issue '规范缺失' $p '文件不存在'; continue }
  $t = [IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)
  foreach ($stale in 'BaseTop', 'BaseBottom', 'CapInk', 'drawing-recipes.md') {
    if ($t -match [regex]::Escape($stale)) { Add-Issue '规范漂移' $p "还写着旧名「$stale」（代码里是 BgTop/BgBottom/Ink/leather-recipes.md）" }
  }
}

# ---------- 3) 默认档一致性：exe / bat / launcher.ini 三处 ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  $bat = Join-Path $g.FullName 'src\game_starter.bat'
  $ini = Join-Path $g.FullName 'src\launcher.ini'
  if (-not (Test-Path $cs)) { continue }
  $csText = [IO.File]::ReadAllText($cs, [Text.Encoding]::UTF8)
  $m = [regex]::Match($csText, 'int mode\s*=\s*(\d+)')
  if (-not $m.Success) { continue }           # 该启动器没有多档默认，跳过
  $csDefault = [int]$m.Groups[1].Value + 1
  if (Test-Path $bat) {
    $bt = [IO.File]::ReadAllText($bat, [Text.Encoding]::UTF8)
    $mb = [regex]::Match($bt, 'set "sel=(\d+)"')
    if ($mb.Success -and [int]$mb.Groups[1].Value -ne $csDefault) {
      Add-Issue '默认档不一致' $bat ("bat 直接回车 = " + $mb.Groups[1].Value + "，exe 默认 = 第 " + $csDefault + " 档（选项必须两边一致）")
    }
  }
  if (Test-Path $ini) {
    $it = [IO.File]::ReadAllText($ini, [Text.Encoding]::UTF8)
    $mi = [regex]::Match($it, '默认第 (\d+) 档')
    if ($mi.Success -and [int]$mi.Groups[1].Value -ne $csDefault) {
      Add-Issue '默认档不一致' $ini ("文案写「默认第 " + $mi.Groups[1].Value + " 档」，exe 默认 = 第 " + $csDefault + " 档")
    }
  }
}

# ---------- 4) nircmd 落地文件名必须和 bat 里写的逐字一致 ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $rel = Join-Path $g.FullName 'src\game_starter.bat'
  if (-not (Test-Path $rel)) { continue }
  $bt = [IO.File]::ReadAllText($rel, [Text.Encoding]::UTF8)
  foreach ($mm in [regex]::Matches($bt, '_tools\\nircmd\\([A-Za-z0-9_\-]+\.exe)')) {
    $name = $mm.Groups[1].Value
    foreach ($dir in (Join-Path $g.FullName 'release\payload\_tools\nircmd'), (Join-Path $g.FullName 'out\_tools\nircmd')) {
      if (-not (Test-Path $dir)) { continue }
      $have = (Get-ChildItem $dir -File | ForEach-Object { $_.Name }) -join ','
      if ((Get-ChildItem $dir -File | Where-Object { $_.Name -eq $name }).Count -eq 0) {
        Add-Issue '落地文件名不一致' $dir ("bat 调用 $name，目录里只有 $have（if exist 会静默跳过）")
      }
    }
  }
}

# ---------- 5) 文本文件必须 CRLF ----------
foreach ($f in (Get-ChildItem $root -Recurse -File -Include *.cs, *.bat, *.ini, *.md, *.ps1, *.txt |
    Where-Object { $_.FullName -notmatch '\\(release|backup|node_modules)\\' })) {
  $b = [IO.File]::ReadAllBytes($f.FullName)
  $lf = 0; $crlf = 0
  for ($i = 0; $i -lt $b.Length; $i++) {
    if ($b[$i] -eq 10) { if ($i -gt 0 -and $b[$i - 1] -eq 13) { $crlf++ } else { $lf++ } }
  }
  if ($lf -gt 0) { Add-Issue '换行符' $f.FullName ("有 $lf 行是 LF-only（规范要求全部 CRLF）") }
}

# ---------- 6) 交付包根目录那份 ddraw.dll 要和「默认档实际装上去的」字节一致 ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $payload = Join-Path $g.FullName 'release\payload'
  if (-not (Test-Path $payload)) { continue }
  $fix = Join-Path $payload '兼容修复.bat'
  $top = Join-Path $payload 'ddraw.dll'
  if (-not ((Test-Path $fix) -and (Test-Path $top))) { continue }
  $fx = [IO.File]::ReadAllText($fix, [Text.Encoding]::GetEncoding(936))
  $mm = [regex]::Match($fx, 'copy /y "([^"]*ddraw\.dll)" "ddraw\.dll"')
  if (-not $mm.Success) { continue }
  $srcRel = $mm.Groups[1].Value
  $srcPath = Join-Path $payload $srcRel
  if (-not (Test-Path $srcPath)) {
    Add-Issue '交付包自相矛盾' $fix ("兼容修复要装 $srcRel，但 payload 里没有这个文件")
    continue
  }
  if ((Get-FileHash $top).Hash -ne (Get-FileHash $srcPath).Hash) {
    Add-Issue '交付包自相矛盾' $top ("兼容修复装上去的是 $srcRel，与根目录 ddraw.dll 字节不同 → 一键撤回会当成玩家改过、不恢复原文件")
  }
}

# ---------- 7) 图标来源：只许取 exe 自己，不许从游戏 exe 抽（AGENTS 规则 21） ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  if (-not (Test-Path $cs)) { continue }
  $lines = [IO.File]::ReadAllLines($cs, [Text.Encoding]::UTF8)
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -notmatch 'ExtractAssociatedIcon') { continue }
    if ($lines[$i] -match 'Application\.ExecutablePath') { continue }
    Add-Issue '图标来源' $cs ("L" + ($i + 1) + "：从别的文件取图标（会把启动器伪装成游戏进程），只允许 Application.ExecutablePath")
  }
}

# ---------- 8) 次按钮不许等宽三等分（「打开运行日志」六个字必被截） ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  if (-not (Test-Path $cs)) { continue }
  $t2 = [IO.File]::ReadAllText($cs, [Text.Encoding]::UTF8)
  if ($t2 -match '\(innerW\s*-\s*S\(1[0-9]\)\)\s*/\s*3') {
    Add-Issue '按钮等宽切三份' $cs '同一行的次按钮按等宽三等分算宽度，长标签（打开运行日志）会被截；宽度要按文字量分配'
  }
}

# ---------- 输出 ----------
if ($issues.Count -eq 0) { Write-Host '规范自检通过：0 项'; exit 0 }
$rel0 = $root.TrimEnd('\') + '\'
foreach ($grp in ($issues | Group-Object Kind)) {
  Write-Host ("== " + $grp.Name + "（" + $grp.Count + " 项）")
  foreach ($it in $grp.Group) {
    Write-Host ("   " + ($it.Where -replace [regex]::Escape($rel0), '') + "  " + $it.Detail)
  }
}
Write-Host ("合计 " + $issues.Count + " 项待修")
exit 1
