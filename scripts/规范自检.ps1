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

# ---------- 9) 已判死的方案不许重新引进（references\do-not-retry.md） ----------
$doNotRetry = @{
  'Heroes3' = @('dgVoodoo-full', 'renderer\s*=\s*opengl', 'renderer\s*=\s*auto')
}
foreach ($g in $doNotRetry.Keys) {
  $dir = Join-Path (Join-Path (Join-Path $root 'Games') $g) 'src'
  if (-not (Test-Path $dir)) { continue }
  foreach ($f in (Get-ChildItem $dir -File -Recurse)) {
    $text = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
    # 注释掉的「判死方案」是留档说明，不算回潮：.cs 去掉 // 行，bat 去掉 rem / :: 行
    $keep = @()
    foreach ($ln in ($text -split "`r?`n")) {
      $s = $ln.TrimStart()
      if ($s.StartsWith('//')) { continue }
      if ($f.Extension -eq '.bat' -and ($s -match '^(?i)rem\s' -or $s.StartsWith('::'))) { continue }
      $keep += $ln
    }
    $text = $keep -join "`n"
    # cnc-ddraw 的 ini 是「一份模板 + 每个游戏一段」：只认本游戏那一段，别把别的游戏段落算进来
    if ($f.Extension -eq '.ini' -or $f.Extension -eq '.conf') {
      $m = [regex]::Match($text, '(?is)\[heroes3\](.*?)(?=\[|$)')
      if (-not $m.Success) { continue }
      $text = $m.Groups[1].Value
    }
    foreach ($pat in $doNotRetry[$g]) {
      if ($text -match $pat) {
        Add-Issue '判死方案回潮' $f.FullName ("出现了已判死的配置「" + $pat + "」，见 references\do-not-retry.md（规则 33）")
      }
    }
  }
}

# ---------- 10) 界面不许指挥人动手（AGENTS 规则 36：交付=放进目录就能跑，没有第二步） ----------
# 「管理员 / 右键以管理员身份运行 / 请先运行某某」都算违规：客户机上没有这个角色，也不许让人做第二步。
# 已交付出去的游戏按规则 25 不返工，先记在这份名单里；**重做某个游戏的启动器时把它从名单里删掉**。
$grandfatheredTellUserToDoIt = 'CS16', 'DFBHD', 'FiveStar', 'FutureSoldier', 'GodofWarIII',
                               'JustCause', 'OnimushaWotS', 'Prototype1', 'Prototype2',
                               'RoadRash', 'TheSaboteur'
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  if ($grandfatheredTellUserToDoIt -contains $g.Name) { continue }
  $src = Join-Path $g.FullName 'src'
  if (-not (Test-Path $src)) { continue }
  foreach ($f in (Get-ChildItem $src -File -Recurse -Include Launcher.cs, launcher.ini)) {
    $isCs = $f.Name -eq 'Launcher.cs'
    $n = 0
    foreach ($ln in [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)) {
      $n++
      $s = $ln.TrimStart()
      if ($isCs -and ($s.StartsWith('//') -or $s.StartsWith('///') -or $s.StartsWith('*'))) { continue }
      if (-not $isCs -and $s.StartsWith(';')) { continue }
      foreach ($w in '管理员', '以管理员身份运行', '请右键', '请先运行', '双击本') {
        if ($ln -match [regex]::Escape($w)) {
          Add-Issue '要人动手' $f.FullName ("L" + $n + "：出现「" + $w + "」——交付标准是放进目录就能跑，" +
            "做不到就自动处理或只写日志（AGENTS 规则 36）")
        }
      }
    }
  }
}

# ---------- 11) 改系统状态必须无条件执行 + 必还（AGENTS 规则 24 第 11 条、规则 37） ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  if (-not (Test-Path $cs)) { continue }
  $t = [IO.File]::ReadAllText($cs, [Text.Encoding]::UTF8)
  if ($t -notmatch 'ChangeDisplaySettings') { continue }
  foreach ($ln in ($t -split "`r?`n")) {
    if ($ln -notmatch 'Apply60Hz\s*\(' -or $ln.TrimStart().StartsWith('//')) { continue }
    if ($ln -match '^\s*(if|else|switch|while)\b|=\s*.*\?\s*') {
      Add-Issue '分叉的强制设置' $cs ('这行把 60Hz 放在条件里：' + $ln.Trim() +
        '（本机永远走正常分支，异常分支等于没验证过，只允许一条路径）')
    }
  }
  if ($t -notmatch 'finally\s*\{[^}]*Restore') {
    Add-Issue '改了不还' $cs '调了 ChangeDisplaySettings 却没有在 finally 里还原显示模式（退出必还）'
  }
}

# ---------- 12) 环境值不许直接当除数（AGENTS 规则 37：客户机 0xC0000094） ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  if (-not (Test-Path $cs)) { continue }
  $n = 0
  foreach ($ln in [IO.File]::ReadAllLines($cs, [Text.Encoding]::UTF8)) {
    $n++
    $s = $ln.TrimStart()
    if ($s.StartsWith('//')) { continue }
    # 分母是环境读数（DpiX/DpiY/refresh/cx/cy/spc/bps/totalC/freeC/elapsed 等）且没被 Math.Max 包住
    if ($ln -match '/\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)?(DpiX|DpiY|refresh|cx|cy|spc|bps|totalC|freeC|elapsed)\b' `
        -and $ln -notmatch 'Math\.Max|Math\.Round\(\s*0|>\s*0') {
      Add-Issue '拿环境值当除数' $cs ('L' + $n + '：' + $s +
        '（取不到就是 0 → 整数除零 0xC0000094；先断言或 Math.Max(1, x) 兜住）')
    }
  }
}

# ---------- 13) 兼容修复.bat 的文案不许和它实际装的层相反（默认档那一层不许被写成「已撤下」） ----------
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $cs = Join-Path $g.FullName 'src\Launcher.cs'
  $fix = Join-Path $g.FullName 'src\兼容修复.bat'
  if (-not (Test-Path $cs) -or -not (Test-Path $fix)) { continue }
  $csText = [IO.File]::ReadAllText($cs, [Text.Encoding]::UTF8)
  $mMode = [regex]::Match($csText, 'int mode\s*=\s*(\d+)')
  $mLayer = [regex]::Match($csText, 'ModeLayer\w*\s*=\s*\{([^}]*)\}')
  if (-not $mMode.Success -or -not $mLayer.Success) { continue }
  $layers = @([regex]::Matches($mLayer.Groups[1].Value, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
  $idx = [int]$mMode.Groups[1].Value
  if ($idx -ge $layers.Count) { continue }
  $defName = if ($layers[$idx] -eq 'voodoo') { 'dgVoodoo' } elseif ($layers[$idx] -eq 'cnc') { 'cnc-ddraw' } else { '' }
  if ($defName.Length -eq 0) { continue }
  $n = 0
  foreach ($ln in ([IO.File]::ReadAllText($fix, [Text.Encoding]::UTF8) -split "`r?`n")) {
    $n++
    if ($ln -match '已撤下' -and $ln -match [regex]::Escape($defName)) {
      Add-Issue '维护工具文案与代码相反' $fix ("L" + $n + "：默认第 " + ($idx + 1) + " 档用的是 $defName，" +
        "这个文件却说它「已撤下」——照这段 echo 说话的人会把默认档讲错（同一事实只留一处权威）")
    }
  }
}

# ---------- 14) 成长四件套有没有真的接上自动化产物（AGENTS 规则 38） ----------
# 只查已经写了自净/成长小节的游戏（已交付的不返工，规则 25）。查的是**那一节自己**：
# 要有「下次省掉哪一步」，并且点名具体产物（规范自检项 / tools\ 脚本 / exe 开关 / 规范条目）；
# 或者写明这次为什么没加检查（如「已交付不返工，加了会全库误报」）——不许默默留一笔流水账。
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $doc = Join-Path $g.FullName '适配过程分析.md'
  if (-not (Test-Path $doc)) { continue }
  $body = ''
  $in = $false
  foreach ($ln in [IO.File]::ReadAllLines($doc, [Text.Encoding]::UTF8)) {
    if ($ln -match '^#{2,4} ') { $in = ($ln -match '自净|成长'); if ($in) { $body += $ln + "`n" }; continue }
    if ($in) { $body += $ln + "`n" }
  }
  if ($body.Length -eq 0) { continue }
  if ($body -notmatch '下次省掉哪一步') {
    Add-Issue '成长没落地' $doc '自净/成长小节里没有「下次省掉哪一步」这句（规则 38 第 4 件）'
    continue
  }
  $artifact = ($body -match '规范自检') -or ($body -match 'tools\\[A-Za-z0-9_\-]+\.ps1') -or
              ($body -match 'game_starter\.exe\s+/[a-z]+') -or ($body -match '硬约束\s*\d+|规则\s*\d+') -or
              ($body -match '脚本\s*[A-Za-z0-9_\-]+\.ps1')
  $reasoned = $body -match '没加自动检查|不加检查|未加检查|没加检查'
  if (-not ($artifact -or $reasoned)) {
    Add-Issue '成长没落地' $doc '自净小节既没点名自动化产物（检查项 / tools\*-test.ps1 / exe 开关 / 规范条目），也没说明为什么这次不加检查（规则 38 第 1 件）'
  }
}

# ---------- 15) 取证截图只许 PrintWindow，不许 CopyFromScreen 截全屏（pitfalls 77 / AGENTS 工具与环境） ----------
# CopyFromScreen 会把用户桌面上的别的窗口一起带进证据（踩过两次）。已交付的其它游戏按规则 25 不返工，
# 先记在这份名单里；重做某个游戏时把它从名单里删掉。
$grandfatheredScreenGrab = 'CS16', 'GodofWarIII', 'JustCause', 'KingdomHeroes8', 'OnimushaWotS', 'RoadRash'
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $tdir = Join-Path $g.FullName 'tools'
  if (-not (Test-Path $tdir)) { continue }
  if ($grandfatheredScreenGrab -contains $g.Name) { continue }
  foreach ($f in (Get-ChildItem $tdir -File -Filter *.ps1)) {
    $n = 0
    foreach ($ln in [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)) {
      $n++
      $s = $ln.Trim()
      if ($s.StartsWith('#') -or $s.StartsWith('//')) { continue }
      if ($s -match 'CopyFromScreen') {
        Add-Issue '取证方法错' $f.FullName ("L" + $n + "：CopyFromScreen 截全屏会把用户桌面带进证据，" +
          "改用 PrintWindow(PW_RENDERFULLCONTENT) 抓窗口自己（pitfalls 77、AGENTS 规则 38 第 1 件）")
      }
    }
  }
}

# ---------- 16) 会把游戏真拉起来的测试脚本必须显式确认（AGENTS 规则 24 第 14 类） ----------
# 判据：脚本用 `/go`、`/mode=` 零交互启动，或者按游戏进程名强停 —— 这两种都会真开一次游戏。
# 已交付的其它游戏按规则 25 不返工，先进豁免名单；重做某个游戏时把它从名单里删掉。
$grandfatheredGameLaunch = 'CS16', 'DFBHD', 'FiveStar', 'FutureSoldier', 'GodofWarIII', 'JustCause',
                            'KingdomHeroes8', 'OnimushaWotS', 'Prototype1', 'Prototype2',
                            'RoadRash', 'TheSaboteur', 'War3'
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $tdir = Join-Path $g.FullName 'tools'
  if (-not (Test-Path $tdir)) { continue }
  if ($grandfatheredGameLaunch -contains $g.Name) { continue }
  foreach ($f in (Get-ChildItem $tdir -File -Filter *.ps1)) {
    $t = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
    $launches = ($t -match "ArgumentList\s+'/-?[a-z]*go'") -or ($t -match '/mode=') -or
                ($t -match "Stop-Process\s+-Name\s+\w*Heroes3") -or ($t -match 'ArgumentList.*"/go"')
    if (-not $launches) { continue }
    if ($t -notmatch 'AllowGameLaunch') {
      Add-Issue '测试反复起游戏' $f.FullName '这个脚本会真把游戏拉起来，但没有 -AllowGameLaunch 门禁' +
        '（用户明令别再反复运行游戏；收尾只跑不启动游戏的测试，规则 24 第 14 类、规则 38 第 1 件）'
    }
  }
}

# ---------- 17) 含中文的 bat 必须自己钉死代码页（GBK 脚本被 UTF-8 控制台调用会写出乱码文件名） ----------
# 2026-11-07 实测：从 pwsh 7（代码页 65001）调用 一键应用.bat，cmd 按 UTF-8 解码 GBK 字节，
# 「交付记录.txt」被写成乱码文件名，下一次撤回就找不到记录 —— 同一份包换个调用方式就坏（规则 24 第 10 类）。
# 只管维护/交付/修复这类**会写文件**的 bat；已交付的游戏按规则 25 不返工，进豁免名单，重做时删掉自己的名字。
$grandfatheredBatCp = 'CS16', 'DFBHD', 'FiveStar', 'FutureSoldier', 'GodofWarIII', 'JustCause',
                      'KingdomHeroes8', 'OnimushaWotS', 'Prototype1', 'Prototype2', 'RoadRash', 'TheSaboteur'
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $src = Join-Path $g.FullName 'src'
  if (-not (Test-Path $src)) { continue }
  if ($grandfatheredBatCp -contains $g.Name) { continue }
  foreach ($f in (Get-ChildItem $src -File -Filter *.bat)) {
    if ($f.Name -eq 'game_starter.bat') { continue }   # 玩家侧控制台：按 bat 规范保持原样，规则 27 不改已交付 bat
    $t = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
    $writesChineseFile = ($t -match '>>"%' -or $t -match 'echo\s+[^\r\n]*[\u4e00-\u9fff]')
    if ($t -notmatch '[\u4e00-\u9fff]' -or -not $writesChineseFile) { continue }
    if ($t -notmatch 'chcp\s+936') {
      Add-Issue 'bat 没钉代码页' $f.FullName ('这份 bat 会输出中文' + $(if ($t -match '>>"%') { '并写记录文件' } else { '' }) +
        '，但没在开头 `chcp 936 >nul`：从 UTF-8 控制台调用时中文全乱码，写出的记录文件名也会变乱码，' +
        '下一次按名字就找不到它（规则 24 第 10 类：同一功能只允许一条路径）')
    }
  }
}

# ---------- 18) 测试断言不许依赖控制台代码页（规则 38：同一条坑不许踩第二次） ----------
# 断中文结论时，必须让程序把结论另存成 UTF-8 文件再读，别解析 stdout。
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  $tdir = Join-Path $g.FullName 'tools'
  if (-not (Test-Path $tdir)) { continue }
  if ($grandfatheredScreenGrab -contains $g.Name) { continue }
  foreach ($f in (Get-ChildItem $tdir -File -Filter *.ps1)) {
    $t = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
    if ($t -match 'Out-String' -and $t -match "-match\s*'[^']*[\u4e00-\u9fff]") {
      Add-Issue '断言依赖代码页' $f.FullName ('从 stdout 取中文再断言（Out-String + 中文正则）：' +
        '换一台机器、另一种控制台代码页就会假失败。改成让程序把结论写成 UTF-8 文件再读，' +
        '样板：tools\wer-test.ps1 调 game_starter.exe /wer <wer> <out.txt>')
    }
  }
}

# ---------- 19) 盘符不等于设备类型（客户机 A:/B: 是虚拟盘，被日志写成「软驱」误导了排查） ----------
# 类型只认 GetDriveType 的回答；按盘符猜设备 = 日志撒谎（规则 24 第 12 类）。
foreach ($g in (Get-ChildItem (Join-Path $root 'Games') -Directory)) {
  foreach ($sub in @('src', 'tools')) {
    $dir = Join-Path $g.FullName $sub
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in (Get-ChildItem $dir -File -Include *.cs, *.ps1, *.py -Recurse)) {
      $t = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
      $lines = $t -split "`r`n|`n"
      for ($n = 0; $n -lt $lines.Count; $n++) {
        if ($lines[$n] -match '软驱') {
          Add-Issue '按盘符猜设备' $f.FullName ("L" + ($n + 1) + "：把 A:/B: 写成「软驱」——现在的机器上那是虚拟盘或误报，" +
            "类型必须问 GetDriveType 再如实报（英雄无敌3 客户机日志就是这么撒谎的）")
        }
      }
      if ($t -match '"[AB]:\\' -and $t -notmatch 'GetDriveType') {
        Add-Issue '按盘符猜设备' $f.FullName '枚举了 A:/B: 却没问 GetDriveType：盘符推不出设备类型，读数会误导排查'
      }
    }
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
