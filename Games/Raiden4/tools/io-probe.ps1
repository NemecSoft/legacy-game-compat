param([int]$TargetPid = 0)
$src = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class IoProbe {
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool b, int c);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr baseAddr, byte[] buf, int size, out int read);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static string Hex(byte[] b, int n) {
    StringBuilder sb = new StringBuilder();
    for (int i = 0; i < n && i < b.Length; i++) sb.Append(b[i].ToString("x2") + " ");
    return sb.ToString();
  }
  public static void Run(int pid, long modBase) {
    IntPtr h = OpenProcess(0x00100410, false, pid);
    if (h == IntPtr.Zero) { Console.WriteLine("OpenProcess fail"); return; }
    byte[] names = new byte[0x480];
    int got;
    if (ReadProcessMemory(h, new IntPtr(modBase + 0x10a40), names, names.Length, out got)) {
      Console.WriteLine("== name table @+10a40 got=" + got);
      for (int i = 0; i < 4; i++) {
        int off = i * 0x114;
        int end = Array.IndexOf(names, (byte)0, off, Math.Min(0x114, names.Length - off));
        if (end < 0) end = off + 0x40;
        string s = Encoding.ASCII.GetString(names, off, end - off);
        Console.WriteLine("entry" + i + ": " + (s.Length > 0 ? s : "(empty)") + "  hex=" + Hex(names, off + 24).Substring(off * 3, 72));
      }
    } else Console.WriteLine("name table read fail");
    byte[] tbl = new byte[0x100];
    if (ReadProcessMemory(h, new IntPtr(modBase + 0xf000), tbl, tbl.Length, out got)) {
      Console.WriteLine("== handle table @+f000");
      for (int i = 0; i < 0x100; i += 16) Console.WriteLine((modBase + 0xf000 + i).ToString("x8") + "  " + Hex(tbl, i + 16).Substring(i * 3, 48));
    }
    byte[] ptrs = new byte[0x60];
    if (ReadProcessMemory(h, new IntPtr(modBase + 0x10ec0), ptrs, ptrs.Length, out got)) {
      Console.WriteLine("== mutex names @+10ec0: " + Encoding.ASCII.GetString(ptrs).Replace("\0", "|"));
    }
    CloseHandle(h);
  }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
$p = if ($TargetPid) { Get-Process -Id $TargetPid } else { Get-Process game -ErrorAction SilentlyContinue }
if (-not $p) { Write-Host 'no game'; exit 1 }
$m = $p.Modules | Where-Object { $_.ModuleName -eq 'iDmacDrv32.dll' } | Select-Object -First 1
if (-not $m) { Write-Host 'no iDmacDrv32 module'; exit 1 }
Write-Host ('pid=' + $p.Id + ' iDmac base=' + $m.BaseAddress.ToInt64().ToString('X8'))
[IoProbe]::Run($p.Id, $m.BaseAddress.ToInt64())