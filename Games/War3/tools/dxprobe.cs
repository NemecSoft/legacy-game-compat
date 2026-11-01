using System;
using System.Runtime.InteropServices;
using System.Text;

class DxProbe
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct D3DADAPTER_IDENTIFIER9
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Driver;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public long DriverVersion;
        public int VendorId, DeviceId, SubSysId, Revision;
        public Guid DeviceIdentifier;
        public int WHQLLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct D3DPRESENT_PARAMETERS
    {
        public int BackBufferWidth, BackBufferHeight, BackBufferFormat, BackBufferCount;
        public int MultiSampleType, MultiSampleQuality;
        public int SwapEffect;
        public IntPtr hDeviceWindow;
        public int Windowed, EnableAutoDepthStencil, AutoDepthStencilFormat, Flags;
        public int FullScreen_RefreshRateInHz, PresentationInterval;
    }

    [DllImport("d3d9.dll")] static extern IntPtr Direct3DCreate9(int sdk);
    [DllImport("d3d11.dll")] static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr sw,
        uint flags, IntPtr featureLevels, uint numLevels, uint sdkVersion, out IntPtr device, out int fl, out IntPtr ctx);

    [ComImport, Guid("a65b8071-3bfe-4213-9a5b-491da4461ca7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ID3DX9
    {
        int RegisterSoftwareDevice(IntPtr f);
        int GetAdapterCount();
        int GetAdapterIdentifier(int a, int flags, out D3DADAPTER_IDENTIFIER9 id);
        int GetAdapterModeCount(int a, int fmt);
        int EnumAdapterModes(int a, int fmt, int i, IntPtr mode);
        int GetAdapterDisplayMode(int a, IntPtr mode);
        int CheckDeviceType(int a, int type, int fmt, int back, int windowed);
        int CheckDeviceFormat(int a, int type, int fmt, int usage, int rtype, int cfmt);
        int CheckDeviceMultiSampleType(int a, int type, int fmt, int windowed, int ms, IntPtr q);
        int CheckDepthStencilMatch(int a, int type, int afmt, int rfmt, int dfmt);
        int CheckDeviceFormatConversion(int a, int type, int sfmt, int tfmt);
        int GetDeviceCaps(int a, int type, IntPtr caps);
        int GetAdapterMonitor(int a);
        int CreateDevice(int a, int type, IntPtr hwnd, int behavior, ref D3DPRESENT_PARAMETERS pp, out IntPtr dev);
    }

    const int D3DDEVTYPE_HAL = 1;
    const int D3DSWAPEFFECT_DISCARD = 1;
    const int D3DFMT_X8R8G8B8 = 22;
    const int D3DDEVCAPS_HWTRANSFORMANDLIGHT = 0x00000020;
    const int D3DCREATE_SOFTWARE_VERTEXPROCESSING = 0x20;
    const int D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x40;

    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();

    static string Hex(int hr) { return "0x" + ((uint)hr).ToString("X8"); }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== D3D9 adapters ===");
        IntPtr p = IntPtr.Zero;
        try { p = Direct3DCreate9(32); }
        catch (Exception ex) { Console.WriteLine("Direct3DCreate9 threw: " + ex.Message); }
        Console.WriteLine("Direct3DCreate9(32) = " + (p == IntPtr.Zero ? "null" : "ok"));
        if (p != IntPtr.Zero)
        {
            ID3DX9 d3d = (ID3DX9)Marshal.GetObjectForIUnknown(p);
            int n = d3d.GetAdapterCount();
            Console.WriteLine("adapter count = " + n);
            for (int i = 0; i < n; i++)
            {
                D3DADAPTER_IDENTIFIER9 id;
                int hr = d3d.GetAdapterIdentifier(i, 0, out id);
                Console.WriteLine(string.Format("  [{0}] hr={1} \"{2}\" driver=\"{3}\" vendor=0x{4:X4} device=0x{5:X4} monitor=0x{6:X}",
                    i, Hex(hr), hr == 0 ? id.Description : "?", hr == 0 ? id.Driver : "?", id.VendorId, id.DeviceId, d3d.GetAdapterMonitor(i)));
                foreach (int createFlags in new int[] { D3DCREATE_HARDWARE_VERTEXPROCESSING, D3DCREATE_SOFTWARE_VERTEXPROCESSING })
                {
                    D3DPRESENT_PARAMETERS pp = new D3DPRESENT_PARAMETERS();
                    pp.BackBufferWidth = 640; pp.BackBufferHeight = 480;
                    pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
                    pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
                    pp.hDeviceWindow = GetDesktopWindow();
                    pp.Windowed = 1;
                    pp.PresentationInterval = -2147483648; // IMMEDIATE
                    IntPtr dev;
                    int chr = d3d.CreateDevice(i, D3DDEVTYPE_HAL, IntPtr.Zero, createFlags, ref pp, out dev);
                    Console.WriteLine(string.Format("      CreateDevice(windowed, flags=0x{0:X}) = {1}", createFlags, Hex(chr)));
                }
            }
        }
        Console.WriteLine("=== D3D11 hardware device ===");
        IntPtr d11; int fl; IntPtr ctx;
        int hr11 = D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, out d11, out fl, out ctx);
        Console.WriteLine("D3D11CreateDevice(HARDWARE, 11.0) = " + Hex(hr11) + (hr11 == 0 ? " featureLevel=0x" + fl.ToString("X") : ""));
        if (d11 != IntPtr.Zero) Marshal.Release(d11);
        if (ctx != IntPtr.Zero) Marshal.Release(ctx);
    }
}