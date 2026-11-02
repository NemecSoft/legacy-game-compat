<#
  legacy-game-compat — 一键适配工作流

  用法:
    powershell -ExecutionPolicy Bypass -File adapt.ps1 -GameDir "D:\Games\SomeGame"
    powershell -ExecutionPolicy Bypass -File adapt.ps1 -GameDir "D:\Games\SomeGame" -Layer dgv
    powershell -ExecutionPolicy Bypass -File adapt.ps1 -GameDir "D:\Games\SomeGame" -DryRun
    powershell -ExecutionPolicy Bypass -File adapt.ps1 -GameDir "D:\Games\SomeGame" -Launcher exe -NoVerify

  参数:
    -GameDir      游戏目录（必填）
    -Exe          指定主程序，默认自动挑
    -Layer        auto | dxvk | dgv | none   （默认 auto）
    -Launcher     bat | exe | none           （默认 bat）
    -DryRun       只探测和报告，不改任何文件
    -NoVerify     跳过启动验证
    -Force        即使探针显示原生正常也照样安装
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [string]$Exe = "",
    [string]$Title = "",
    [ValidateSet("auto","dxvk","dgv","none")][string]$Layer = "auto",
    [ValidateSet("bat","exe","none")][string]$Launcher = "bat",
    [switch]$DryRun,
    [switch]$NoVerify,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$Tools = Join-Path $Root "tools"
$Templates = Join-Path $Root "templates"
$Scripts = Join-Path $Root "scripts"
$Report = New-Object System.Collections.ArrayList

function Log([string]$msg, [string]$level = "INFO") {
    $line = "[{0}] {1}" -f $level, $msg
    Write-Host $line
    [void]$Report.Add($line)
}

function Save-Utf8NoBom([string]$path, [string]$text) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $text, $enc)
}

function Save-Gbk([string]$path, [string]$text) {
    $enc = [System.Text.Encoding]::GetEncoding(936)
    $t = $text -replace "`r`n", "`n" -replace "`n", "`r`n"
    [System.IO.File]::WriteAllText($path, $t, $enc)
}

function Get-PeInfo([string]$file) {
    $bytes = [System.IO.File]::ReadAllBytes($file)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    $machine = [BitConverter]::ToUInt16($bytes, $pe + 4)
    $is64 = ($machine -eq 0x8664)
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes).ToLowerInvariant()
    $api = @()
    foreach ($n in @("ddraw.dll","d3d8.dll","d3d9.dll","d3d11.dll","opengl32.dll")) {
        if ($ascii.Contains($n)) { $api += $n }
    }
    [pscustomobject]@{ Path = $file; Name = (Split-Path $file -Leaf); Is64 = $is64; Api = $api; Size = $bytes.Length }
}

function Select-MainExe([string]$dir, [string]$forced) {
    if ($forced) {
        $p = Join-Path $dir $forced
        if (-not (Test-Path $p)) { throw "指定的 exe 不存在: $forced" }
        return (Get-PeInfo $p)
    }
    $bad = "unins|setup|install|vcredist|dxsetup|repair|update|revupdat|locaupd|pack|init|med|ded|server|editor|tool|config"
    $cands = @()
    foreach ($f in Get-ChildItem $dir -Filter *.exe -File) {
        $base = [System.IO.Path]::GetFileNameWithoutExtension($f.Name).ToLowerInvariant()
        if ($base -match $bad) { continue }
        $info = Get-PeInfo $f.FullName
        $score = $info.Size
        if ($info.Api.Count -gt 0) { $score += 100MB }
        if ($base -match "(lc|lan|net)$") { $score -= 200MB }      # 局域网客户端通常带 LC/LAN
        $info | Add-Member -NotePropertyName Score -NotePropertyValue $score -Force
        $cands += $info
    }
    if ($cands.Count -eq 0) { throw "目录里找不到合适的 exe（可用 -Exe 指定）" }
    $sorted = $cands | Sort-Object Score -Descending
    foreach ($c in $sorted) { Log ("候选: " + $c.Name + "  " + [math]::Round($c.Size/1MB,1) + " MB  接口:" + $(if ($c.Api.Count) { $c.Api -join "/" } else { "-" })) }
    return $sorted[0]
}

function Invoke-Probe([bool]$is64) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    if (-not (Test-Path $csc)) { Log "找不到 csc.exe，跳过探针" "WARN"; return $null }
    $out = Join-Path $env:TEMP ("compat_probe_" + [Guid]::NewGuid().ToString("N").Substring(0,8) + ".exe")
    $plat = if ($is64) { "x64" } else { "x86" }
    & $csc -nologo -platform:$plat -out:$out (Join-Path $Scripts "D3D9Probe.cs") 2>&1 | Out-Null
    if (-not (Test-Path $out)) { Log "探针编译失败" "WARN"; return $null }
    $lines = & $out 2>&1
    Remove-Item $out -Force -ErrorAction SilentlyContinue
    return $lines
}

function Test-GlobalD3D9Switches {
    $found = @()
    $checks = @(
        @('HKLM:\SOFTWARE\Microsoft\Direct3D\Drivers', 'SoftwareOnly'),
        @('HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D\Drivers', 'SoftwareOnly'),
        @('HKLM:\SOFTWARE\Microsoft\DirectDraw', 'EmulationOnly'),
        @('HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw', 'EmulationOnly')
    )
    foreach ($c in $checks) {
        $v = Get-ItemProperty -Path $c[0] -Name $c[1] -ErrorAction SilentlyContinue
        if ($null -ne $v) {
            $val = $v.PSObject.Properties[$c[1]].Value
            $found += ($c[0] + '  ' + $c[1] + '=' + $val)
        }
    }
    return $found
}

function Resolve-Layer($info) {
    if ($Layer -ne "auto") { return $Layer }
    if ($info.Api -contains "ddraw.dll") { return "dgv" }
    if ($info.Api -contains "d3d9.dll" -or $info.Api -contains "d3d8.dll" -or $info.Api -contains "d3d11.dll") { return "dxvk" }
    if ($info.Api -contains "opengl32.dll") { return "none" }
    return "dxvk"
}

function Install-Layer($info, [string]$layer) {
    $sub = if ($info.Is64) { "x64" } else { "x86" }
    $src = Join-Path (Join-Path $Tools $layer) $sub
    if (-not (Test-Path $src)) { throw "tools 里没有 $layer/$sub" }

    $files = @()
    if ($layer -eq "dxvk") {
        if ($info.Api -contains "d3d8.dll") { $files = @("d3d8.dll","d3d9.dll") }
        elseif ($info.Api -contains "d3d11.dll") { $files = @("d3d11.dll","dxgi.dll") }
        elseif ($info.Api.Count -eq 0) { $files = @("d3d8.dll","d3d9.dll") }   # 接口未知：两个都放，游戏只加载自己需要的
        else { $files = @("d3d9.dll") }
    } else {
        $files = @(Get-ChildItem $src -Filter *.dll | ForEach-Object { $_.Name })
    }

    $bak = Join-Path $GameDir "_兼容备份"
    $keep = Join-Path $GameDir "_dxvk"
    if ($layer -eq "dgv") { $keep = Join-Path $GameDir "_dgv" }

    if (-not $DryRun) {
        if (-not (Test-Path $bak)) { New-Item -ItemType Directory $bak | Out-Null }
        if (-not (Test-Path $keep)) { New-Item -ItemType Directory $keep | Out-Null }
    }

    foreach ($f in $files) {
        $from = Join-Path $src $f
        $to = Join-Path $GameDir $f
        if (-not (Test-Path $from)) { Log "缺少源文件 $f" "WARN"; continue }
        if (Test-Path $to) {
            $bakFile = Join-Path $bak $f
            if (-not (Test-Path $bakFile)) {
                Log "备份 $f -> _兼容备份"
                if (-not $DryRun) { Copy-Item $to $bakFile -Force }
            }
        }
        Log "复制 $f"
        if (-not $DryRun) {
            Copy-Item $from $to -Force
            Copy-Item $from (Join-Path $keep $f) -Force
        }
    }

    if ($layer -eq "dxvk") {
        $conf = Join-Path $GameDir "dxvk.conf"
        if (-not (Test-Path $conf)) {
            Log "写入 dxvk.conf"
            if (-not $DryRun) {
                Save-Utf8NoBom $conf "# 显示后端信息: 取消注释下一行`r`n#dxvk.hud = version,api,fps`r`nd3d9.presentInterval = 1`r`n"
            }
        }
    }
    return $files
}

function New-FixBats($info, [string]$layer, $files) {
    $main = $files[0]
    $keepDir = if ($layer -eq "dgv") { "_dgv" } else { "_dxvk" }

    $fix = @"
@echo off&color 1f
title 兼容修复
cd /d "%~dp0"
if not exist "$keepDir\$main" echo   缺少运行文件，兼容包不完整&pause&exit /b
if exist "$main" if not exist "$main.bak" ren "$main" "$main.bak"
copy /y "$keepDir\$main" "$main" >nul
if not exist "dxvk.conf" >"dxvk.conf" echo d3d9.presentInterval = 1
echo.
echo   已启用画面适配
echo   双击 game_starter.exe 进游戏
echo.
pause
"@
    $restore = @"
@echo off&color 1f
title 恢复原版
cd /d "%~dp0"
del /q "$main" "dxvk.conf" >nul 2>&1
if exist "$main.bak" ren "$main.bak" "$main"
echo.
echo   已移除适配文件，恢复原生
echo.
pause
"@
    Log "生成 兼容修复.bat / 恢复原版.bat"
    if (-not $DryRun) {
        Save-Gbk (Join-Path $GameDir "兼容修复.bat") $fix
        Save-Gbk (Join-Path $GameDir "恢复原版.bat") $restore
    }
}

function New-GoBat($info, [string]$gameTitleCn, [string]$gameTitleEn) {
    $tpl = Join-Path $Templates "game_starter.bat"
    $text = [System.IO.File]::ReadAllText($tpl, [System.Text.Encoding]::UTF8)
    $text = $text.Replace("{{GAME_NAME}}", $gameTitleCn).Replace("{{GAME_NAME_EN}}", $gameTitleEn)
    $text = $text.Replace("{{GAME_EXE}}", $info.Name).Replace("{{LAYER_DESC}}", "已适配新系统")
    $text = $text.Replace("{{EXTRA_HINT}}", "分辨率/画质在游戏内 OPTIONS 里改")
    Log "生成 game_starter.bat"
    if (-not $DryRun) { Save-Gbk (Join-Path $GameDir "game_starter.bat") $text }
}

function New-ExeLauncher($info, [string]$gameTitleCn) {
    $srcExe = Join-Path $Templates "Launcher.exe"
    $srcIni = Join-Path $Templates "launcher.ini.sample"
    if (-not (Test-Path $srcExe)) { Log "模板里没有 Launcher.exe，跳过" "WARN"; return }
    $proc = [System.IO.Path]::GetFileNameWithoutExtension($info.Name)
    $ini = @"
[Launcher]
Title=$gameTitleCn
GameExe=$($info.Name)
LoaderExe=
GameProcess=$proc
PreLaunch=
Notes=画面已适配新系统，直接进游戏即可\n黑屏或白屏：先跑一次 兼容修复.bat\n退出游戏后本窗口自动关闭
"@
    Log "生成 game_starter.exe + launcher.ini"
    if (-not $DryRun) {
        Copy-Item $srcExe (Join-Path $GameDir "game_starter.exe") -Force
        Remove-Item (Join-Path $GameDir "Launcher.exe") -Force -ErrorAction SilentlyContinue
        Save-Utf8NoBom (Join-Path $GameDir "launcher.ini") $ini
    }
}

function Get-ScreenRes {
    $vc = Get-CimInstance Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution } | Select-Object -First 1
    if ($vc) { return @($vc.CurrentHorizontalResolution, $vc.CurrentVerticalResolution) }
    return @(1920,1080)
}

function Set-ScreenRes([int]$w, [int]$h) {
    $sig = @"
using System;using System.Runtime.InteropServices;
public class DispFix {
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string dmDeviceName;
    public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public int dmFields; public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string dmFormName;
    public short dmLogPixels; public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight; }
  [DllImport("user32.dll")] public static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
}
"@
    Add-Type $sig -ErrorAction SilentlyContinue
    $dm = New-Object DispFix+DEVMODE
    $dm.dmSize = 220
    $dm.dmPelsWidth = $w; $dm.dmPelsHeight = $h; $dm.dmBitsPerPel = 32
    $dm.dmFields = 0x80000 -bor 0x100000
    [void][DispFix]::ChangeDisplaySettings([ref]$dm, 1)
}

function Invoke-Verify($info, [string]$titleCn) {
    Add-Type -AssemblyName System.Drawing
    $before = Get-ScreenRes
    Log ("验证：桌面分辨率 " + $before[0] + "x" + $before[1])
    $shotDir = Join-Path $GameDir "_compat-shots"
    if (-not $DryRun -and -not (Test-Path $shotDir)) { New-Item -ItemType Directory $shotDir | Out-Null }

    $procName = [System.IO.Path]::GetFileNameWithoutExtension($info.Name)
    $p = Start-Process -FilePath $info.Path -WorkingDirectory $GameDir -PassThru
    Start-Sleep -Seconds 12
    $p.Refresh()
    $alive = -not $p.HasExited
    $shots = @()
    if (-not $DryRun) {
        $bmp = New-Object System.Drawing.Bitmap ([int]$before[0]), ([int]$before[1])
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen(0,0,0,0,(New-Object System.Drawing.Size ([int]$before[0]), ([int]$before[1])))
        $shot = Join-Path $shotDir ("run_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".png")
        $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
        $shots += $shot
        Log "截图：" + $shot
    }
    if ($alive) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 3
    Set-ScreenRes ([int]$before[0]) ([int]$before[1])
    Log ("验证结果：" + $(if ($alive) { "进程存活，已截图，随后关闭" } else { "进程提前退出（需要看截图/日志排查）" }))
    return $alive
}

# ---------------- main ----------------

if (-not (Test-Path $GameDir)) { throw "游戏目录不存在: $GameDir" }
$GameDir = (Resolve-Path $GameDir).Path
Log "游戏目录: $GameDir"
if ($DryRun) { Log "DRY RUN：不会修改任何文件" "WARN" }

$info = Select-MainExe $GameDir $Exe
Log ("主程序: " + $info.Name + "  位数: " + $(if ($info.Is64) { "64" } else { "32" }) + "  接口: " + $(if ($info.Api.Count) { $info.Api -join ", " } else { "未识别（按 d3d9 处理）" }))

if ($info.Api.Count -eq 0) {
    $union = @()
    foreach ($f in Get-ChildItem $GameDir -Filter *.exe -File) {
        $t = Get-PeInfo $f.FullName
        foreach ($a in $t.Api) { if ($union -notcontains $a) { $union += $a } }
    }
    if ($union.Count -gt 0) {
        $info.Api = $union
        Log ("主程序未识别接口，参考同目录 exe: " + ($union -join ", "))
    }
}

$globalSwitches = Test-GlobalD3D9Switches
if ($globalSwitches.Count -gt 0) {
    foreach ($g in $globalSwitches) { Log ("发现全局 D3D9 软件渲染开关: " + $g) "WARN" }
    Log "这是整机 D3D9 失效的常见根因（DirectX 修复工具写入），不是单个游戏的问题。请先运行 scripts\修复本机D3D9.bat，再重跑本工作流。" "ERROR"
    throw "检测到全局 D3D9 软件渲染开关，已停止；先运行 scripts\修复本机D3D9.bat"
}

$probe = Invoke-Probe $info.Is64
$nativeOk = $false
if ($probe) {
    foreach ($l in $probe) { Log ("探针 " + $l) }
    $nativeOk = ($probe -join "`n").Contains("fullscreen(0x44)=0x00000000")
    Log ("原生 D3D9: " + $(if ($nativeOk) { "可用" } else { "不可用" }))
}

$chosen = Resolve-Layer $info
if ($nativeOk -and -not $Force) {
    Log "原生 D3D9 可用，默认不安装翻译层（需要强制安装加 -Force）" "WARN"
} elseif ($chosen -eq "none") {
    Log "该接口需要手工处理（OpenGL 用 Mesa，见 references/diagnose.md），工作流结束" "WARN"
} else {
    Log "选层: $chosen"
    $files = Install-Layer $info $chosen
    New-FixBats $info $chosen $files
    $titleCn = if ($Title) { $Title } else { Split-Path $GameDir -Leaf }
    $titleEn = $info.Name
    if ($Launcher -eq "bat") { New-GoBat $info $titleCn $titleEn }
    elseif ($Launcher -eq "exe") { New-ExeLauncher $info $titleCn }
    if ($DryRun) { Log "DRY RUN：跳过启动验证" }
    elseif (-not $NoVerify) { [void](Invoke-Verify $info $titleCn) }
    else { Log "已跳过启动验证" }
}

$reportPath = Join-Path $GameDir "_兼容报告.md"
$head = "# 兼容适配报告`r`n`r`n- 生成时间: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "`r`n- 游戏目录: " + $GameDir + "`r`n- 主程序: " + $info.Name + " (" + $(if ($info.Is64) { "64 位" } else { "32 位" }) + ")`r`n- 接口: " + $(if ($info.Api.Count) { $info.Api -join ", " } else { "未识别" }) + "`r`n- 选择: " + $chosen + "`r`n- 启动器: " + $Launcher + "`r`n`r`n## 过程日志`r`n`r`n" + (($Report | ForEach-Object { "- " + $_ }) -join "`r`n") + "`r`n"
if (-not $DryRun) { Save-Utf8NoBom $reportPath $head; Log "报告已写入: $reportPath" }
Log "完成"
