#include <windows.h>
#include <d3d9.h>
#include <cstdio>

#define MAX_D3D9ON12_QUEUES 2
typedef struct _D3D9ON12_ARGS {
    BOOL Enable9On12;
    IUnknown *pD3D12Device;
    IUnknown *ppD3D12Queues[MAX_D3D9ON12_QUEUES];
    UINT NumQueues;
    UINT NodeMask;
} D3D9ON12_ARGS;

typedef IDirect3D9* (WINAPI *PFN_CREATE9ON12)(UINT, D3D9ON12_ARGS*, UINT);
typedef HRESULT (WINAPI *PFN_CREATE9ON12EX)(UINT, D3D9ON12_ARGS*, UINT, IDirect3D9Ex**);

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l){ return DefWindowProcW(h,m,w,l); }
static void show(const char* tag, HRESULT hr){ printf("%-46s 0x%08X\n", tag, (unsigned)hr); }

int main(){
    WNDCLASSW wc; ZeroMemory(&wc,sizeof(wc));
    wc.lpfnWndProc = WndProc;
    wc.hInstance = GetModuleHandleW(NULL);
    wc.lpszClassName = L"d3don12probe";
    wc.hCursor = LoadCursorW(NULL, MAKEINTRESOURCEW(32512));
    RegisterClassW(&wc);
    HWND hwnd = CreateWindowW(L"d3don12probe", L"D3D9On12 probe", WS_OVERLAPPEDWINDOW,
                              40, 40, 800, 600, NULL, NULL, wc.hInstance, NULL);
    printf("hwnd=%p\n", (void*)hwnd);
    if(hwnd){ ShowWindow(hwnd, SW_SHOWNORMAL); UpdateWindow(hwnd); }

    HMODULE h9 = LoadLibraryW(L"d3d9.dll");
    printf("LoadLibrary(d3d9.dll)=%p\n", (void*)h9);
    PFN_CREATE9ON12 p = (PFN_CREATE9ON12)GetProcAddress(h9, "Direct3DCreate9On12");
    PFN_CREATE9ON12EX pex = (PFN_CREATE9ON12EX)GetProcAddress(h9, "Direct3DCreate9On12Ex");
    printf("Direct3DCreate9On12=%p Direct3DCreate9On12Ex=%p\n", (void*)p, (void*)pex);
    if(!p){ puts("no export"); return 2; }

    D3D9ON12_ARGS args; ZeroMemory(&args,sizeof(args));
    args.Enable9On12 = TRUE;
    args.NodeMask = 0;
    IDirect3D9* d9 = p(D3D_SDK_VERSION, &args, 1);
    printf("Direct3DCreate9On12 -> %p\n", (void*)d9);
    if(!d9){
        IDirect3D9Ex* ex = NULL;
        HRESULT hre = pex ? pex(D3D_SDK_VERSION, &args, 1, &ex) : E_NOTIMPL;
        printf("Direct3DCreate9On12Ex hr=0x%08X ex=%p\n", (unsigned)hre, (void*)ex);
        if(ex){ d9 = ex; }
        else { puts("9on12 unavailable"); return 3; }
    }

    UINT n = d9->GetAdapterCount();
    printf("GetAdapterCount = %u\n", n);
    D3DDISPLAYMODE dm; ZeroMemory(&dm,sizeof(dm));
    HRESULT hr = d9->GetAdapterDisplayMode(0,&dm); show("GetAdapterDisplayMode", hr);
    printf("  mode after call: %ux%u fmt=%u\n", dm.Width, dm.Height, dm.Format);
    D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
    show("GetDeviceCaps(HAL)", d9->GetDeviceCaps(0,D3DDEVTYPE_HAL,&caps));
    printf("  caps.DeviceType=%u VertexShader=%u PixelShader=%u\n", caps.DeviceType, caps.VertexShaderVersion, caps.PixelShaderVersion);
    show("CheckDeviceType(windowed X8R8G8B8)", d9->CheckDeviceType(0,D3DDEVTYPE_HAL,D3DFMT_X8R8G8B8,D3DFMT_X8R8G8B8,TRUE));

    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=800; pp.BackBufferHeight=600; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=hwnd;
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    hr = d9->CreateDevice(0,D3DDEVTYPE_HAL,hwnd,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    show("CreateDevice(windowed, real hwnd)", hr);
    if(SUCCEEDED(hr) && dev){
        show("Clear", dev->Clear(0,NULL,D3DCLEAR_TARGET,D3DCOLOR_XRGB(0,128,0),1.0f,0));
        show("Present", dev->Present(NULL,NULL,NULL,NULL));
        dev->Release(); dev=NULL;
    }

    ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=hwnd;
    pp.Windowed=FALSE; pp.FullScreen_RefreshRateInHz=60; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    hr = d9->CreateDevice(0,D3DDEVTYPE_HAL,hwnd,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    show("CreateDevice(fullscreen 640x480@60)", hr);
    if(SUCCEEDED(hr) && dev){ dev->Release(); dev=NULL; }

    d9->Release();
    if(hwnd) DestroyWindow(hwnd);
    return 0;
}
