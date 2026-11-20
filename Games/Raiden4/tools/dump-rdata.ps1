param([string]$OutDir = 'D:\Temp\tp-rdata')
$src = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public class RdataDump {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool b, int c);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr baseAddr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static void Run(int pid, long imageBase, string outDir) {
    IntPtr h = OpenProcess(0x00100410, false, pid);
    if (h == IntPtr.Zero) { Console.WriteLine("OpenProcess fail " + Marshal.GetLastWin32Error()); return; }
    long[][] blocks = new long[][] {
      new long[] { 0x668000, 0x20e28a },   // .rdata
      new long[] { 0x877000, 0x1690c8 },   // .data
      new long[] { 0x46e5000, 0xe38 },     // teknoGod small data
      new long[] { 0x1000, 0x666766 }      // .text refresh
    };
    Directory.CreateDirectory(outDir);
    foreach (var bl in blocks) {
      byte[] buf = new byte[bl[1]];
      int got;
      bool ok = ReadProcessMemory(h, new IntPtr(imageBase + bl[0]), buf, buf.Length, out got);
      string name = (imageBase + bl[0]).ToString("X8") + ".bin";
      if (ok && got == buf.Length) {
        File.WriteAllBytes(Path.Combine(outDir, name), buf);
        Console.WriteLine("OK " + name + " size=" + got);
      } else {
        Console.WriteLine("FAIL " + name + " ok=" + ok + " got=" + got + " err=" + Marshal.GetLastWin32Error());
      }
    }
    CloseHandle(h);
  }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
$p = Get-Process game -ErrorAction SilentlyContinue
if (-not $p) {
  Write-Host 'self-start /go'
  Start-Process -FilePath 'D:\YunGame\G\Raiden4\game_starter.exe' -ArgumentList '/go' -WorkingDirectory 'D:\YunGame\G\Raiden4'
  for ($i = 0; $i -lt 30; $i++) { Start-Sleep 2; $p = Get-Process game -ErrorAction SilentlyContinue; if ($p) { break } }
}
if (-not $p) { Write-Host 'no game'; exit 1 }
$tp = $null
for ($i = 0; $i -lt 40; $i++) {
  $p.Refresh()
  $tp = $p.Modules | Where-Object { $_.ModuleName -eq 'TeknoParrot.dll' } | Select-Object -First 1
  if ($tp) { break }
  Start-Sleep 2
}
if (-not $tp) { Write-Host 'no TP module'; exit 1 }
$base = $tp.BaseAddress.ToInt64()
Write-Host ('pid=' + $p.Id + ' TP base=' + $base.ToString('X8'))
[RdataDump]::Run($p.Id, $base, $OutDir)