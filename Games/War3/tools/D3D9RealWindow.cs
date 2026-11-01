using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class D3D9RealWindow
{
    [DllImport("d3d9.dll")]
    static extern IntPtr Direct3DCreate9(uint sdkVersion);

    [StructLayout(LayoutKind.Sequential)]
    struct D3DDISPLAYMODE { public uint Width, Height, RefreshRate, Format; }

    [StructLayout(LayoutKind.Sequential)]
    struct D3DPRESENT_PARAMETERS
    {
        public uint BackBufferWidth;
        public uint BackBufferHeight;
        public uint BackBufferFormat;
        public uint BackBufferCount;
        public uint MultiSampleType;
        public uint MultiSampleQuality;
        public uint SwapEffect;
        public IntPtr hDeviceWindow;
        public int Windowed;
        public int EnableAutoDepthStencil;
        public uint AutoDepthStencilFormat;
        public uint Flags;
        public uint FullScreen_RefreshRateInHz;
        public uint PresentationInterval;
    }

    delegate int GetCountDel(IntPtr self);
    delegate int GetModeDel(IntPtr self, uint adapter, out D3DDISPLAYMODE mode);
    delegate int CreateDevDel(IntPtr self, uint adapter, uint devType, IntPtr hwnd, uint behavior, ref D3DPRESENT_PARAMETERS pp, out IntPtr device);
    delegate int ReleaseDel(IntPtr self);

    static T Fn<T>(IntPtr obj, int index)
    {
        IntPtr vtbl = Marshal.ReadIntPtr(obj);
        IntPtr fn = Marshal.ReadIntPtr(vtbl, index * IntPtr.Size);
        return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
    }

    static void Main()
    {
        Form form = new Form();
        form.Text = "D3D9 real-window probe";
        form.Width = 800; form.Height = 600;
        form.Show();
        Application.DoEvents();

        IntPtr d3d = Direct3DCreate9(32);
        Console.WriteLine("Direct3DCreate9=" + d3d);
        if (d3d == IntPtr.Zero) { form.Close(); return; }
        UInt32 n = (UInt32)Fn<GetCountDel>(d3d, 4)(d3d);
        Console.WriteLine("adapter count = " + n);

        D3DDISPLAYMODE mode;
        int hm = Fn<GetModeDel>(d3d, 8)(d3d, 0, out mode);
        Console.WriteLine(string.Format("GetAdapterDisplayMode=0x{0:X8} {1}x{2}@{3} fmt={4}", hm, mode.Width, mode.Height, mode.RefreshRate, mode.Format));

        D3DPRESENT_PARAMETERS pp = new D3DPRESENT_PARAMETERS();
        pp.BackBufferWidth = 800; pp.BackBufferHeight = 600;
        pp.BackBufferFormat = 22; pp.BackBufferCount = 1;
        pp.SwapEffect = 2; pp.hDeviceWindow = form.Handle;
        pp.Windowed = 1; pp.PresentationInterval = 1;

        IntPtr dev;
        int hr = Fn<CreateDevDel>(d3d, 16)(d3d, 0, 1, form.Handle, 0x40, ref pp, out dev);
        Console.WriteLine(string.Format("CreateDevice(windowed 800x600, real hwnd)=0x{0:X8} dev={1}", hr, dev));
        if (dev != IntPtr.Zero) Fn<ReleaseDel>(dev, 2)(dev);

        pp.BackBufferWidth = 640; pp.BackBufferHeight = 480;
        pp.Windowed = 0; pp.FullScreen_RefreshRateInHz = 60;
        dev = IntPtr.Zero;
        hr = Fn<CreateDevDel>(d3d, 16)(d3d, 0, 1, form.Handle, 0x40, ref pp, out dev);
        Console.WriteLine(string.Format("CreateDevice(fullscreen 640x480@60)=0x{0:X8} dev={1}", hr, dev));
        if (dev != IntPtr.Zero) Fn<ReleaseDel>(dev, 2)(dev);

        Fn<ReleaseDel>(d3d, 2)(d3d);
        form.Close();
        Console.WriteLine("0x00000000 = OK   0x8876086A = D3DERR_NOTAVAILABLE   0x8876086C = D3DERR_INVALIDCALL");
    }
}
