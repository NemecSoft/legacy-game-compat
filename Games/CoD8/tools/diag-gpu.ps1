# GPU 归属取证：--go 起游戏，抓 iw5sp 实际渲染在哪个适配器（LUID）上
$ErrorActionPreference = 'Continue'
$g = 'D:\YunGame\CoD\CoD8'
if (Test-Path "$g\运行日志.txt") { Remove-Item "$g\运行日志.txt" -Force }

$proc = Start-Process "$g\game_starter.exe" -ArgumentList '--go' -WorkingDirectory $g -PassThru
Start-Sleep -Seconds 10
$game = Get-Process iw5sp -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $game) { Write-Output '游戏未运行'; exit }

Write-Output ("游戏存活 pid=" + $game.Id)
# GPU Engine 计数器：实例名格式 pid_<pid>_luid_0x..._0x..._phys_N
$samples = (Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples |
  Where-Object { $_.InstanceName -match 'iw5sp' }
$mine = $samples | Where-Object { $_.CookedValue -gt 0 }
if ($mine) {
  Write-Output '===== 游戏 GPU 引擎实例（有占用）====='
  $mine | ForEach-Object { Write-Output ($_.InstanceName + "  " + [Math]::Round($_.CookedValue,1) + "%") }
} else {
  Write-Output '游戏无 GPU 占用（可能还在加载）'
}
# 该 pid 的所有实例（含 0 占用）
$all = $samples | Where-Object { $_.InstanceName -match ('pid_' + $game.Id + '_') } | Select-Object -First 6
if ($all) { Write-Output '===== 该 pid 全部实例 ====='; $all | ForEach-Object { Write-Output ($_.InstanceName) } }

# 三个适配器的 LUID 对照：从注册表 DX 栈取（只读）
Write-Output '===== 适配器 LUID 对照（注册表）====='
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\ScanOptionList' -ErrorAction SilentlyContinue | Out-Null
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\PowerManagedDefaultSettings' -ErrorAction SilentlyContinue | Out-Null
# 用 .NET 枚举 DXGI 适配器拿 LUID 最准
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class DxgiEnum {
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct AdapterDesc {
        public uint Flags; public LUID AdapterLuid;
        public uint SystemVideoMemory, DedicatedSystemMemory, DedicatedVideoMemory, SharedSystemMemory;
    }
    [DllImport("dxgi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern int CreateDXGIFactory(ref Guid iid, out IntPtr factory);
    public delegate int EnumAdaptersCB(IntPtr self, uint index, out IntPtr adapter);
    public static void Run() {
        Guid g = new Guid("790a45f7-4d23-499c-b6d0-632beda97b46"); // IDXGIFactory1
        IntPtr f;
        int hr = CreateDXGIFactory(ref g, out f);
        if (hr != 0) { Console.WriteLine("CreateDXGIFactory hr=0x" + hr.ToString("X")); return; }
        var m = Marshal.GetDelegateForFunctionPointer(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(f, 3 * IntPtr.Size) /* not portable */), typeof(EnumAdaptersCB));
    }
}
'@ -ErrorAction SilentlyContinue
Write-Output '（LUID 对照改用下一轮的 DXGI COM 枚举脚本）'
Write-Output 'done'
