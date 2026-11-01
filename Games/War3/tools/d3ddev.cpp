#include <windows.h>
#include <d3d9.h>
#include <cstdio>

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l){ return DefWindowProcW(h,m,w,l); }

static void show(const char* tag, HRESULT hr){ printf("%-46s 0x%08X\n", tag, (unsigned)hr); }

int main(){
    WNDCLASSW wc; ZeroMemory(&wc,sizeof(wc));
    wc.lpfnWndProc = WndProc;
    wc.hInstance = GetModuleHandleW(NULL);
    wc.lpszClassName = L"d3ddevprobe";
    wc.hCursor = LoadCursorW(NULL, MAKEINTRESOURCEW(32512));
    RegisterClassW(&wc);
    HWND hwnd = CreateWindowW(L"d3ddevprobe", L"d3d9 device probe", WS_OVERLAPPEDWINDOW,
                              40, 40, 800, 600, NULL, NULL, wc.hInstance, NULL);
    printf("hwnd=%p\n", (void*)hwnd);
    if(hwnd){ ShowWindow(hwnd, SW_SHOWNORMAL); UpdateWindow(hwnd); }

    IDirect3D9* d9 = Direct3DCreate9(D3D_SDK_VERSION);
    if(!d9){ puts("Direct3DCreate9 failed"); return 1; }

    UINT n = d9->GetAdapterCount();
    printf("GetAdapterCount = %u\n", n);

    D3DDISPLAYMODE dm; ZeroMemory(&dm,sizeof(dm));
    HRESULT hr = d9->GetAdapterDisplayMode(0, &dm);
    show("GetAdapterDisplayMode", hr);
    printf("  mode after call: %ux%u fmt=%u refresh=%u\n", dm.Width, dm.Height, dm.Format, dm.RefreshRate);

    D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
    show("GetDeviceCaps(HAL)", d9->GetDeviceCaps(0, D3DDEVTYPE_HAL, &caps));
    printf("  caps.DeviceType=%u VertexShader=%u PixelShader=%u\n", caps.DeviceType, caps.VertexShaderVersion, caps.PixelShaderVersion);

    show("CheckDeviceType(windowed X8R8G8B8)", d9->CheckDeviceType(0, D3DDEVTYPE_HAL, D3DFMT_X8R8G8B8, D3DFMT_X8R8G8B8, TRUE));
    show("CheckDeviceType(fullscreen X8R8G8B8)", d9->CheckDeviceType(0, D3DDEVTYPE_HAL, D3DFMT_X8R8G8B8, D3DFMT_X8R8G8B8, FALSE));
    show("CheckDeviceType(windowed A8R8G8B8)", d9->CheckDeviceType(0, D3DDEVTYPE_HAL, D3DFMT_A8R8G8B8, D3DFMT_A8R8G8B8, TRUE));

    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth = 800; pp.BackBufferHeight = 600;
    pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
    pp.SwapEffect = D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow = hwnd;
    pp.Windowed = TRUE; pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev = NULL;
    hr = d9->CreateDevice(0, D3DDEVTYPE_HAL, hwnd, D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, &dev);
    show("CreateDevice(windowed, HW vertex, real hwnd)", hr);
    if(SUCCEEDED(hr) && dev){
        dev->Clear(0, NULL, D3DCLEAR_TARGET, D3DCOLOR_XRGB(0,128,0), 1.0f, 0);
        show("Present(windowed)", dev->Present(NULL,NULL,NULL,NULL));
        dev->Release(); dev = NULL;
    }

    ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth = 800; pp.BackBufferHeight = 600;
    pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
    pp.SwapEffect = D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow = hwnd;
    pp.Windowed = TRUE; pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
    hr = d9->CreateDevice(0, D3DDEVTYPE_HAL, hwnd, D3DCREATE_SOFTWARE_VERTEXPROCESSING, &pp, &dev);
    show("CreateDevice(windowed, SW vertex, real hwnd)", hr);
    if(SUCCEEDED(hr) && dev){ dev->Release(); dev = NULL; }

    ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth = 640; pp.BackBufferHeight = 480;
    pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
    pp.SwapEffect = D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow = hwnd;
    pp.Windowed = FALSE; pp.FullScreen_RefreshRateInHz = 60;
    pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
    hr = d9->CreateDevice(0, D3DDEVTYPE_HAL, hwnd, D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, &dev);
    show("CreateDevice(fullscreen 640x480@60, real hwnd)", hr);
    if(SUCCEEDED(hr) && dev){ dev->Release(); dev = NULL; }

    d9->Release();
    if(hwnd) DestroyWindow(hwnd);
    return 0;
}
