// D3D8 探针：只建一个窗口模式的 IDirect3DDevice8，不启动游戏、不改桌面分辨率。
// 用法：把本程序编译成 32 位 exe，连同候选层 DLL 放同一目录再跑；目录里放哪个层就测哪个层。
// 输出 0x00000000 = 该层能替代系统 D3D8 建设备；非 0 = 该层在这台机器上不通。
// 编译：pwsh -NoProfile -File tools\build-probe.ps1
using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class D3D8Probe
{
    // D3D8 的 D3DPRESENT_PARAMETERS：13 个 32 位字段 = 52 字节
    // （D3D9 才多一个 MultiSampleQuality；少算/多算字段会让 CreateDevice 直接 INVALIDCALL）
    [StructLayout(LayoutKind.Sequential)]
    private struct PresentParams
    {
        public uint BackBufferWidth;
        public uint BackBufferHeight;
        public uint BackBufferFormat;          // 22 = D3DFMT_X8R8G8B8
        public uint BackBufferCount;
        public uint MultiSampleType;           // 0 = D3DMULTISAMPLE_NONE
        public uint SwapEffect;                // 1 = D3DSWAPEFFECT_DISCARD
        public IntPtr hDeviceWindow;
        public int Windowed;
        public int EnableAutoDepthStencil;
        public uint AutoDepthStencilFormat;    // 77 = D3DFMT_D24X8
        public uint Flags;
        public uint FullScreen_RefreshRateInHz;
        public uint FullScreen_PresentationInterval;
    }

    [DllImport("d3d8.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr Direct3DCreate8(uint sdkVersion);

    [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName,
        int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr h);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetModuleHandle(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetModuleFileName(IntPtr h, StringBuilder sb, int cap);

    // IDirect3D8::CreateDevice 是虚表第 15 号槽（0 起算，IUnknown 3 个 + 12 个 IDirect3D8 方法）
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateDeviceFn(
        IntPtr self, uint adapter, uint deviceType, IntPtr focusWindow, uint behaviorFlags,
        ref PresentParams pp, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseFn(IntPtr self);

    private static int Main(string[] args)
    {
        string tag = args.Length > 0 ? args[0] : "native";
        Console.WriteLine("D3D8 probe / " + tag);
        Console.WriteLine("PresentParams size = " + Marshal.SizeOf(typeof(PresentParams)) + " (expect 52)");

        IntPtr mod = GetModuleHandle("d3d8.dll");
        if (mod != IntPtr.Zero)
        {
            var sb = new StringBuilder(512);
            GetModuleFileName(mod, sb, sb.Capacity);
            Console.WriteLine("loaded d3d8.dll = " + sb);
        }

        // 窗口模式必须给一个真实窗口句柄，传 NULL 会直接 D3DERR_INVALIDCALL
        IntPtr hwnd = CreateWindowEx(0, "STATIC", "d3d8probe", 0, 0, 0, 1, 1,
                                     IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine("focus window = 0x" + hwnd.ToString("X"));
        if (hwnd == IntPtr.Zero) { Console.WriteLine("建窗口失败"); return 4; }

        IntPtr d3d;
        try
        {
            d3d = Direct3DCreate8(220);   // D3D_SDK_VERSION (DX8.1)
        }
        catch (Exception ex)
        {
            Console.WriteLine("Direct3DCreate8 threw: " + ex.GetType().Name + " " + ex.Message);
            return 2;
        }
        if (d3d == IntPtr.Zero) { Console.WriteLine("Direct3DCreate8 returned NULL"); return 3; }
        Console.WriteLine("Direct3DCreate8 OK");
        // 调用之后再查一次：DLL 是延迟加载的，之前查会是空
        IntPtr mod2 = GetModuleHandle("d3d8.dll");
        if (mod2 != IntPtr.Zero)
        {
            var sb2 = new StringBuilder(512);
            GetModuleFileName(mod2, sb2, sb2.Capacity);
            Console.WriteLine("actually loaded d3d8.dll = " + sb2);
        }

        IntPtr vtable = Marshal.ReadIntPtr(d3d, 0);
        var create = (CreateDeviceFn)Marshal.GetDelegateForFunctionPointer(
            Marshal.ReadIntPtr(vtable, 15 * IntPtr.Size), typeof(CreateDeviceFn));
        var release = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(
            Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size), typeof(ReleaseFn));

        PresentParams pp = new PresentParams();
        pp.BackBufferWidth = 800;
        pp.BackBufferHeight = 600;
        pp.BackBufferFormat = 22;
        pp.BackBufferCount = 1;
        pp.MultiSampleType = 0;
        pp.SwapEffect = 1;
        pp.hDeviceWindow = hwnd;
        pp.Windowed = 1;                    // 窗口模式：绝不切桌面分辨率
        pp.EnableAutoDepthStencil = 1;
        pp.AutoDepthStencilFormat = 77;
        pp.Flags = 0;
        pp.FullScreen_RefreshRateInHz = 0;
        pp.FullScreen_PresentationInterval = 0;

        IntPtr device;
        int hr = create(d3d, 0, 1 /*D3DDEVTYPE_HAL*/, hwnd,
                        0x20 /*D3DCREATE_SOFTWARE_VERTEXPROCESSING*/, ref pp, out device);
        Console.WriteLine("CreateDevice(windowed 800x600 X8R8G8B8 D24X8) = 0x" + hr.ToString("X8"));

        if (hr == 0 && device != IntPtr.Zero)
        {
            IntPtr devVt = Marshal.ReadIntPtr(device, 0);
            var devRelease = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(devVt, 2 * IntPtr.Size), typeof(ReleaseFn));
            devRelease(device);
        }
        release(d3d);
        DestroyWindow(hwnd);
        Console.WriteLine(hr == 0 ? "RESULT: OK" : "RESULT: FAIL");
        return hr == 0 ? 0 : 1;
    }
}
