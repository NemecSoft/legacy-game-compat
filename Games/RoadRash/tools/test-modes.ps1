param(
  [string]$GameDir = 'D:\YunGame\Race\RoadRash',
  [string]$Only = 'all'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class MWin2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public static string T(IntPtr h){var sb=new StringBuilder(2048);GetWindowText(h,sb,sb.Capacity);return sb.ToString();}
  public static string C(IntPtr h){var sb=new StringBuilder(256);GetClassName(h,sb,sb.Capacity);return sb.ToString();}
  public static List<IntPtr> Tops(uint pid) {
    var l = new List<IntPtr>();
    EnumWindows((h,x) => { uint p; GetWindowThreadProcessId(h,out p); if(p==pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
    return l;
  }
  public static IntPtr FindChild(IntPtr parent, string clsPart, string text) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(parent, (h,x) => {
      if (found != IntPtr.Zero) return true;
      bool clsOk = (clsPart == null || C(h).Contains(clsPart));
      bool txtOk = (text == null || T(h) == text);
      if (clsOk && txtOk) { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@

$outDir = Join-Path $PSScriptRoot 'mode-test2'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Get-Res {
  $v = Get-CimInstance Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution } | Select-Object -First 1
  if ($v) { "$($v.CurrentHorizontalResolution)x$($v.CurrentVerticalResolution)" } else { 'unknown' }
}
function Get-Mode {
  $s = Get-Content (Join-Path $GameDir 'ddraw.ini') -Encoding UTF8
  $r = ($s | Select-String '^renderer=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value
  $m = ($s | Select-String '^maintas=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value
  $w = ($s | Select-String '^width=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value
  $h = ($s | Select-String '^height=(.*)$' | Select-Object -First 1).Matches[0].Groups[1].Value
  "$r maintas=$m ${w}x${h}"
}
function Save-Shot([string]$name) {
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
  $g.Dispose()
  $path = Join-Path $outDir $name
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return $path
}
function Wait-Game([int]$ms) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.ElapsedMilliseconds -lt $ms) {
    $p = Get-Process -Name Roadrash -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { return $p }
    Start-Sleep -Milliseconds 300
  }
  return $null
}
function Kill-Game {
  Get-Process -Name Roadrash -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 1500
}
function Reset-State {
  Get-Process -Name Roadrash, go -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Copy-Item (Join-Path $GameDir '_cnc-ddraw\ddraw.ini') (Join-Path $GameDir 'ddraw.ini') -Force
  Remove-Item (Join-Path $GameDir 'Roadrash_d3d9.log') -Force -ErrorAction SilentlyContinue
  Start-Sleep -Milliseconds 600
}

function Test-BatMode([string]$inputLine, [string]$tag, [int]$waitSec) {
  Write-Output ("== bat $tag ==")
  Reset-State
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'cmd.exe'
  $psi.Arguments = '/c ""' + (Join-Path $GameDir 'game_starter.bat') + '""'
  $psi.WorkingDirectory = $GameDir
  $psi.UseShellExecute = $false
  $psi.RedirectStandardInput = $true
  $psi.CreateNoWindow = $false
  $proc = [System.Diagnostics.Process]::Start($psi)
  Start-Sleep -Milliseconds 800
  $proc.StandardInput.WriteLine($inputLine)
  $proc.StandardInput.Flush()
  $game = Wait-Game 20000
  Write-Output ("  started=" + [bool]$game + " mode=" + (Get-Mode))
  if ($game) {
    Start-Sleep -Seconds $waitSec
    Write-Output ("  alive=" + [bool](Get-Process -Id $game.Id -ErrorAction SilentlyContinue) + " res=" + (Get-Res) + " dxvkLog=" + (Test-Path (Join-Path $GameDir 'Roadrash_d3d9.log')))
    Write-Output ("  shot=" + (Save-Shot ($tag + '.png')))
    Kill-Game
    Write-Output ("  after kill res=" + (Get-Res))
  } else { Kill-Game }
  if (-not $proc.HasExited) { $proc.WaitForExit(8000) | Out-Null }
  if (-not $proc.HasExited) { $proc.Kill() }
  Write-Output ("  restored mode=" + (Get-Mode))
}

function Test-ExeMode([int]$index, [string]$tag, [int]$waitSec) {
  Write-Output ("== exe $tag ==")
  Reset-State
  $exe = Start-Process -FilePath (Join-Path $GameDir 'game_starter.exe') -WorkingDirectory $GameDir -PassThru
  Start-Sleep -Seconds 2
  $top = [MWin2]::Tops([uint32]$exe.Id) | Select-Object -First 1
  if (-not $top) { throw 'game_starter.exe 没有窗口' }
  Save-Shot ($tag + '-launcher.png') | Out-Null
  $combo = [MWin2]::FindChild($top, 'COMBOBOX', $null)
  $btn = [MWin2]::FindChild($top, $null, '启动游戏')
  Write-Output ("  window=[" + [MWin2]::T($top) + "] combo=" + ($combo -ne [IntPtr]::Zero) + " startBtn=" + ($btn -ne [IntPtr]::Zero))
  if ($combo -ne [IntPtr]::Zero) { [void][MWin2]::SendMessage($combo, 0x014E, [IntPtr]$index, [IntPtr]::Zero) }
  if ($btn -ne [IntPtr]::Zero) { [void][MWin2]::PostMessage($btn, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) }
  $game = Wait-Game 20000
  Write-Output ("  started=" + [bool]$game + " mode=" + (Get-Mode))
  if ($game) {
    Start-Sleep -Seconds $waitSec
    Write-Output ("  alive=" + [bool](Get-Process -Id $game.Id -ErrorAction SilentlyContinue) + " res=" + (Get-Res) + " dxvkLog=" + (Test-Path (Join-Path $GameDir 'Roadrash_d3d9.log')))
    Write-Output ("  shot=" + (Save-Shot ($tag + '.png')))
    Kill-Game
    Start-Sleep -Seconds 2
    Write-Output ("  restored mode=" + (Get-Mode))
    [void][MWin2]::PostMessage($top, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
  } else { Kill-Game }
  if (-not $exe.HasExited) { $exe.Kill() }
}

if ($Only -eq 'all' -or $Only -eq 'm1') { Test-BatMode '' 'bat-mode1-compat' 30 }
if ($Only -eq 'all' -or $Only -eq 'm2') { Test-BatMode '2' 'bat-mode2-stretch' 30 }
if ($Only -eq 'all' -or $Only -eq 'm3') { Test-BatMode '3' 'bat-mode3-soft' 30 }
if ($Only -eq 'all' -or $Only -eq 'm4') { Test-BatMode '4' 'bat-mode4-640' 22 }
if ($Only -eq 'all' -or $Only -eq 'exe2') { Test-ExeMode 1 'exe-mode2-stretch' 30 }
Reset-State
Write-Output 'all modes done; state reset'
