using System;
using System.Runtime.InteropServices;

// Minimal D3D9 device probe.
// Tells you whether the machine can create a D3D9 hardware device at all,
// which is the first thing to check when an old game says "graphics init failed".
//
// Build 32-bit (games are 32-bit, and only then will a local 32-bit wrapper DLL be used):
//   csc -platform:x86 -out:D3D9Probe32.exe D3D9Probe.cs

class D3D9Probe
{
    [DllImport("d3d9.dll")]
    static extern IntPtr Direct3DCreate9(uint sdkVersion);

    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    static IntPtr g_wnd = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    struct D3DPRESENT_PARAMETERS
    {
        public uint BackBufferWidth;
        public uint BackBufferHeight;
        public uint BackBufferFormat;      // D3DFMT_X8R8G8B8 = 22
        public uint BackBufferCount;
        public uint MultiSampleType;
        public uint MultiSampleQuality;
        public uint SwapEffect;            // DISCARD = 2
        public IntPtr hDeviceWindow;
        public int Windowed;
        public int EnableAutoDepthStencil;
        public uint AutoDepthStencilFormat; // D3DFMT_D24S8 = 75
        public uint Flags;
        public uint FullScreen_RefreshRateInHz;
        public uint PresentationInterval;   // INTERVAL_ONE = 1
    }

    delegate int CreateDeviceDel(IntPtr self, uint adapter, uint devType, IntPtr hFocus,
                                  uint behavior, ref D3DPRESENT_PARAMETERS pp, out IntPtr device);
    delegate int ReleaseDel(IntPtr self);
    delegate int GetAdapterCountDel(IntPtr self);

    static int Create(IntPtr d3d9, uint adapter, bool windowed, uint w, uint h, uint refresh, uint behavior)
    {
        IntPtr vtbl = Marshal.ReadIntPtr(d3d9);
        IntPtr fn = Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size);   // IDirect3D9::CreateDevice
        CreateDeviceDel cd = (CreateDeviceDel)Marshal.GetDelegateForFunctionPointer(fn, typeof(CreateDeviceDel));

        D3DPRESENT_PARAMETERS pp = new D3DPRESENT_PARAMETERS();
        pp.BackBufferWidth = w;
        pp.BackBufferHeight = h;
        pp.BackBufferFormat = 22;
        pp.BackBufferCount = 1;
        pp.SwapEffect = 2;
        pp.hDeviceWindow = g_wnd;
        pp.Windowed = windowed ? 1 : 0;
        pp.EnableAutoDepthStencil = 1;
        pp.AutoDepthStencilFormat = 75;
        pp.FullScreen_RefreshRateInHz = windowed ? 0u : refresh;
        pp.PresentationInterval = 1;

        IntPtr dev;
        int hr = cd(d3d9, adapter, 1, pp.hDeviceWindow, behavior, ref pp, out dev);
        if (dev != IntPtr.Zero)
        {
            IntPtr v = Marshal.ReadIntPtr(dev);
            IntPtr rel = Marshal.ReadIntPtr(v, 2 * IntPtr.Size);
            ReleaseDel rd = (ReleaseDel)Marshal.GetDelegateForFunctionPointer(rel, typeof(ReleaseDel));
            rd(dev);
        }
        return hr;
    }

    static void Main()
    {
        IntPtr d3d9 = Direct3DCreate9(32);
        if (d3d9 == IntPtr.Zero) { Console.WriteLine("Direct3DCreate9 failed"); return; }

        IntPtr vtbl = Marshal.ReadIntPtr(d3d9);
        IntPtr fnCount = Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size);   // IDirect3D9::GetAdapterCount
        GetAdapterCountDel gc = (GetAdapterCountDel)Marshal.GetDelegateForFunctionPointer(fnCount, typeof(GetAdapterCountDel));
        int count = gc(d3d9);
        Console.WriteLine("adapters: " + count);

        g_wnd = GetConsoleWindow();
        if (g_wnd == IntPtr.Zero) g_wnd = GetDesktopWindow();
        Console.WriteLine("device window: " + g_wnd);

        for (uint a = 0; a < count; a++)
        {
            int fs = Create(d3d9, a, false, 1920, 1080, 60, 0x54);   // hw vertex + PURE + multithreaded
            int fs2 = Create(d3d9, a, false, 1920, 1080, 60, 0x44);  // hw vertex + multithreaded
            int win = Create(d3d9, a, true, 1920, 1080, 0, 0x44);
            Console.WriteLine(string.Format("adapter {0}: fullscreen(0x54)=0x{1:X8} fullscreen(0x44)=0x{2:X8} windowed=0x{3:X8}",
                a, fs, fs2, win));
        }
        Console.WriteLine("0x00000000 = OK   0x8876086A = D3DERR_NOTAVAILABLE   0x8876086C = D3DERR_INVALIDCALL");
    }
}
