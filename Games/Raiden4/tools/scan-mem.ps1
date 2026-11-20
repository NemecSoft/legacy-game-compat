# 扫内存 dump 明文代码里的扣命指令（dec/sub dword|byte [reg+0x30]），输出运行时 VA。
param([string]$Dir = 'D:\Temp\raiden-mem', [string]$Pat = 'life')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
public static class MemScan2
{
    public static List<string> Scan(byte[] b, long baseA, string pat)
    {
        List<string> hits = new List<string>();
        for (int i = 0; i + 6 <= b.Length; i++)
        {
            byte b0 = b[i], b1 = b[i + 1], b2 = b[i + 2];
            if ((b1 & 0xC0) == 0xC0) continue;
            byte reg = (byte)(b1 & 0x38);
            string kind = null;
            if (pat == "life")
            {
                if ((b1 & 7) == 4) continue;
                if (b2 != 0x30) continue;
                if (b0 == 0xFF && reg == 0x08) kind = "dec dword";
                else if (b0 == 0xFE && reg == 0x08) kind = "dec byte";
                else if (b0 == 0x83 && reg == 0x28 && b[i + 3] == 0x01) kind = "sub dword,1";
            }
            else if (pat == "sib")
            {
                if (b0 == 0xFF && reg == 0x08 && (b1 & 7) == 4 && b[i + 3] == 0x30) kind = "dec dword sib";
                else if (b0 == 0x83 && reg == 0x28 && (b1 & 7) == 4 && b[i + 3] == 0x30 && b[i + 4] == 0x01) kind = "sub dword sib,1";
            }
            if (kind == null) continue;
            hits.Add(kind + " va=" + (baseA + i).ToString("X8")
                + " bytes=" + BitConverter.ToString(b, i, 8).Replace("-", " "));
        }
        return hits;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$total = 0
foreach ($f in Get-ChildItem -Path $Dir -Filter '*.bin' | Sort-Object Name) {
    $baseA = [Convert]::ToInt64($f.BaseName, 16)
    $b = [System.IO.File]::ReadAllBytes($f.FullName)
    $hits = [MemScan2]::Scan($b, $baseA, $Pat)
    $total += $hits.Count
    foreach ($h in $hits) { Write-Output ($f.Name + " " + $h) }
}
Write-Output ("total=" + $total)