# 纯读玩家对象字段：param Objs 逗号分隔地址，Offsets 逗号分隔十六进制偏移。验证 +0x34 是否无敌计时器。
param([string]$Objs = '', [string]$Offsets = '20,2c,30,34,38')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Runtime.InteropServices;
public static class PeekFields
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, uint addr, byte[] buf, int n, out int read);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    public static uint Read(IntPtr hp, uint addr)
    {
        byte[] b = new byte[4]; int got;
        if (!ReadProcessMemory(hp, addr, b, 4, out got) || got != 4) return 0xFFFFFFFF;
        return BitConverter.ToUInt32(b, 0);
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = Get-Process -Name game -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { 'no game'; exit 1 }
$hp = [PeekFields]::OpenProcess(0x0410, $false, $p.Id)
if ($hp -eq [IntPtr]::Zero) { 'open fail'; exit 1 }
foreach ($o in ($Objs -split ',')) {
    $a = [Convert]::ToUInt32($o.Trim(), 16)
    $line = 'obj=' + $a.ToString('X8')
    foreach ($off in ($Offsets -split ',')) {
        $d = [Convert]::ToUInt32($off.Trim(), 16)
        $line += ' +' + $d.ToString('X2') + '=' + [PeekFields]::Read($hp, $a + $d).ToString('X8')
    }
    $line
}
[PeekFields]::CloseHandle($hp) | Out-Null