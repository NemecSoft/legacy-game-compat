# 纯静态扫描 TeknoParrot.dll（游戏本体嵌入其中）：节表 / 扣命指令搜索 / 按 RVA 读字节。
param([string]$Dll = 'D:\YunGame\G\Raiden4\TeknoParrot\TeknoParrot.dll',
      [ValidateSet('sections','scan','rva')][string]$Mode = 'sections',
      [string]$Rva = '42F1098')
$ErrorActionPreference = 'Stop'
$src = @'
using System;
using System.Collections.Generic;
using System.IO;
public static class TpScan
{
    public class Sec { public string Name; public uint VSize; public uint VA; public uint RawSize; public uint RawPtr; public uint Char; }
    public static List<Sec> Sections(byte[] b)
    {
        List<Sec> ls = new List<Sec>();
        uint nl = BitConverter.ToUInt32(b, 0x3C);
        ushort n = BitConverter.ToUInt16(b, (int)nl + 6);
        ushort oh = BitConverter.ToUInt16(b, (int)nl + 20);
        int st = (int)nl + 24 + oh;
        for (int i = 0; i < n; i++)
        {
            int o = st + i * 40;
            Sec s = new Sec();
            s.Name = System.Text.Encoding.ASCII.GetString(b, o, 8).TrimEnd('\0');
            s.VSize = BitConverter.ToUInt32(b, o + 8);
            s.VA = BitConverter.ToUInt32(b, o + 12);
            s.RawSize = BitConverter.ToUInt32(b, o + 16);
            s.RawPtr = BitConverter.ToUInt32(b, o + 20);
            s.Char = BitConverter.ToUInt32(b, o + 36);
            ls.Add(s);
        }
        return ls;
    }
    public static uint Rva2Off(List<Sec> ls, uint rva)
    {
        foreach (Sec s in ls)
            if (rva >= s.VA && rva < s.VA + Math.Max(s.VSize, s.RawSize))
                return rva - s.VA + s.RawPtr;
        return 0xFFFFFFFF;
    }
    public static List<string> Scan(byte[] b, List<Sec> ls)
    {
        List<string> hits = new List<string>();
        foreach (Sec s in ls)
        {
            if ((s.Char & 0x20000000) == 0) continue;
            int start = (int)s.RawPtr, end = (int)Math.Min(s.RawPtr + s.RawSize, (uint)b.Length);
            for (int i = start; i + 4 <= end; i++)
            {
                byte b0 = b[i], b1 = b[i + 1], b2 = b[i + 2];
                bool ok = false; string kind = "";
                if (b2 == 0x30 && (b1 & 7) != 4 && (b1 & 0xC0) != 0xC0)
                {
                    byte reg = (byte)(b1 & 0x38);
                    if (b0 == 0xFF && reg == 0x08) { ok = true; kind = "dec dword"; }
                    else if (b0 == 0xFE && reg == 0x08) { ok = true; kind = "dec byte"; }
                    else if (b0 == 0x83 && reg == 0x28 && b[i + 3] == 0x01) { ok = true; kind = "sub dword,1"; }
                }
                if (ok)
                {
                    uint rva = (uint)(i - start) + s.VA;
                    hits.Add(kind + " file=" + i.ToString("X8") + " rva=" + rva.ToString("X8")
                        + " bytes=" + BitConverter.ToString(b, i, 6).Replace("-", " "));
                }
            }
        }
        return hits;
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$b = [System.IO.File]::ReadAllBytes($Dll)
$ls = [TpScan]::Sections($b)
if ($Mode -eq 'sections') {
    foreach ($s in $ls) {
        Write-Output ("{0,-8} VA={1:X8} VSize={2:X8} Raw={3:X8} RawSize={4:X8} Char={5:X8}" -f $s.Name, $s.VA, $s.VSize, $s.RawPtr, $s.RawSize, $s.Char)
    }
    exit 0
}
if ($Mode -eq 'rva') {
    $r = [Convert]::ToUInt32($Rva, 16)
    $off = [TpScan]::Rva2Off($ls, $r)
    if ($off -eq 0xFFFFFFFF) { Write-Output "rva 不在任何节内"; exit 1 }
    Write-Output ("rva=" + $Rva + " file=" + $off.ToString("X8"))
    Write-Output ([BitConverter]::ToString($b, [int]$off, 96).Replace("-", " "))
    exit 0
}
$hits = [TpScan]::Scan($b, $ls)
Write-Output ("hits=" + $hits.Count)
foreach ($h in $hits) { Write-Output $h }