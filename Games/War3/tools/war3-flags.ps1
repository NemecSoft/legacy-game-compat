param([string]$Exe='D:\Games\War3\War3.exe',[string[]]$ArgSets=@('','-window','-swtnl','-window|-swtnl'),[int]$Wait=9)
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class QW {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@
$d=Split-Path $Exe
foreach($set in $ArgSets){
  $argsList = @()
  if($set){ $argsList = $set -split '\|' }
  $p=Start-Process -FilePath $Exe -ArgumentList $argsList -WorkingDirectory $d -PassThru
  $seen=@()
  $t0=Get-Date
  while(((Get-Date)-$t0).TotalSeconds -lt $Wait -and (Get-Process -Id $p.Id -EA SilentlyContinue)){
    Start-Sleep -Milliseconds 120
    $ids=@($p.Id)
    $cb = [QW+EnumProc]{
      param($h,$l)
      $wp=0; [QW]::GetWindowThreadProcessId($h,[ref]$wp) | Out-Null
      if($ids -contains [int]$wp -and [QW]::IsWindowVisible($h)){
        $sb=New-Object System.Text.StringBuilder 256; [QW]::GetWindowTextW($h,$sb,256) | Out-Null
        $cn=New-Object System.Text.StringBuilder 128; [QW]::GetClassNameW($h,$cn,128) | Out-Null
        $key=$cn.ToString() + '|' + $sb.ToString()
        if($script:seen -notcontains $key){ $script:seen += $key }
      }
      return $true
    }
    $script:seen = $seen
    [QW]::EnumWindows($cb,[IntPtr]::Zero) | Out-Null
    $seen = $script:seen
  }
  $alive=[bool](Get-Process -Id $p.Id -EA SilentlyContinue)
  if($alive){ Stop-Process -Id $p.Id -Force -EA SilentlyContinue; $code='(killed)' } else { $code = '0x' + ((([int64]$p.ExitCode) -band 0xFFFFFFFF).ToString('X8')) }
  Write-Output ("ARGS=[" + $set + "] alive_after=" + $alive + " exit=" + $code + " windows=" + ($(if($seen.Count){$seen -join ' ; '}else{'<none>'})))
  Get-Process War3,'Warcraft III' -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
  Start-Sleep -Milliseconds 500
}