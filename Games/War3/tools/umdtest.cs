using System;
using System.Runtime.InteropServices;
class UmdTest {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern IntPtr LoadLibraryW(string path);
  [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr h, string n);
  static void Main() {
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    string[] paths = {
      @"C:\Windows\System32\DriverStore\FileRepository\u0201039.inf_amd64_89c3272f19eab06b\B025980\atiumdag.dll",
      @"C:\Windows\SysWOW64\atiumdag.dll",
      @"atiumdag.dll"
    };
    foreach (string p in paths) {
      IntPtr h = LoadLibraryW(p);
      Console.WriteLine(p + " -> 0x" + h.ToInt64().ToString("X") + " lastErr=" + Marshal.GetLastWin32Error());
      if (h != IntPtr.Zero) { Console.WriteLine("   GetCaps-ish symbol: " + GetProcAddress(h, "GetCaps")); }
    }
    IntPtr d3d = LoadLibraryW("d3d9.dll");
    Console.WriteLine("d3d9.dll -> 0x" + d3d.ToInt64().ToString("X"));
  }
}
