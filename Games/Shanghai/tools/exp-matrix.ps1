# conf 格式矩阵实验 v2：修正 -replace 三参 + 量窗口实际宽高
$game = 'D:\YunGame\IndieGame\shanghai'
$conf = Join-Path $game 'dgVoodoo.conf'
$orig = [IO.File]::ReadAllText($conf, [Text.Encoding]::UTF8)

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W2 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
'@

function Run-Case([string]$tag, [string]$res, [bool]$appCtl) {
    $t = $orig
    $t = $t -replace 'Resolution                          = [^\r\n]+', 'Resolution                          = unforced'
    if ($res -ne 'unforced') {
        $idx = $t.IndexOf('[DirectX]')
        $head = $t.Substring(0, $idx)
        $seg = $t.Substring($idx)
        $seg = $seg.Replace('Resolution                          = unforced', ("Resolution                          = " + $res))
        $t = $head + $seg
    }
    $t = $t -replace 'AppControlledScreenMode             = \w+', ("AppControlledScreenMode             = " + ($(if ($appCtl) {'true'} else {'false'})))
    $t = $t -replace 'dgVoodooWatermark                   = \w+', 'dgVoodooWatermark                   = true'
    [IO.File]::WriteAllText($conf, $t, [Text.UTF8Encoding]::new($false))

    $action = New-ScheduledTaskAction -Execute (Join-Path $game 'shanghai.exe') -Argument '-windows' -WorkingDirectory $game
    Register-ScheduledTask -TaskName 'ShanghaiMat' -Action $action -Force | Out-Null
    Start-ScheduledTask -TaskName 'ShanghaiMat'
    $deadline = (Get-Date).AddSeconds(35); $gp = 0
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 700
        $p = Get-Process shanghai -ErrorAction SilentlyContinue
        if ($p) { $gp = $p.Id; break }
    }
    Start-Sleep -Seconds 10
    if ($gp) {
        $p = Get-Process -Id $gp -ErrorAction SilentlyContinue
        if ($p -and $p.MainWindowHandle -ne [IntPtr]::Zero) {
            $r = New-Object W2+RECT
            [W2]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
            Write-Host ("[{0}] 窗口 {1}x{2} @ ({3},{4})" -f $tag, ($r.R-$r.L), ($r.B-$r.T), $r.L, $r.T)
        } else { Write-Host ("[{0}] pid={1} 无主窗口（可能已退）" -f $tag, $gp) }
    } else { Write-Host ("[{0}] 游戏没起来" -f $tag) }
    if ($gp) { Stop-Process -Id $gp -Force -ErrorAction SilentlyContinue }
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline) {
        $s = Get-Process shanghai -ErrorAction SilentlyContinue
        if (-not $s) { break }
        Start-Sleep -Milliseconds 800
    }
    Unregister-ScheduledTask -TaskName 'ShanghaiMat' -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

Run-Case 'E1 1024x768+appctlFalse' '1024x768' $false
Run-Case 'E2 1920x1080+appctlFalse' '1920x1080' $false
Run-Case 'E3 1024x768+appctlTrue' '1024x768' $true
Run-Case 'E4 unforced+appctlFalse' 'unforced' $false

[IO.File]::WriteAllText($conf, $orig, [Text.UTF8Encoding]::new($false))
Write-Host 'conf 已还原'
