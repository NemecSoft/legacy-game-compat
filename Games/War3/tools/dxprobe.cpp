#include <windows.h>
#include <d3d9.h>
#include <d3d11.h>
#include <cstdio>

static void hr(const char* what, HRESULT h) {
    printf("  %-46s = 0x%08X%s\n", what, (unsigned)h, h == 0 ? "  OK" : "");
}

int main() {
    printf("=== D3D9 ===\n");
    IDirect3D9* d3d = Direct3DCreate9(D3D_SDK_VERSION);
    printf("Direct3DCreate9 = %s\n", d3d ? "ok" : "NULL");
    if (d3d) {
        UINT n = d3d->GetAdapterCount();
        printf("adapter count = %u\n", n);
        HWND desk = GetDesktopWindow();
        for (UINT i = 0; i < n; i++) {
            D3DADAPTER_IDENTIFIER9 id;
            ZeroMemory(&id, sizeof(id));
            HRESULT h = d3d->GetAdapterIdentifier(i, 0, &id);
            printf("[%u] GetAdapterIdentifier hr=0x%08X\n", i, (unsigned)h);
            if (SUCCEEDED(h))
                printf("     \"%s\"\n     driver: %s\n     vendor=0x%04X device=0x%04X rev=%d\n",
                       id.Description, id.Driver, id.VendorId, id.DeviceId, id.Revision);
            HMONITOR mon = d3d->GetAdapterMonitor(i);
            MONITORINFO mi; mi.cbSize = sizeof(mi);
            if (mon && GetMonitorInfo(mon, &mi))
                printf("     monitor: %ld,%ld - %ld,%ld primary=%d\n",
                       mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right, mi.rcMonitor.bottom,
                       (mi.dwFlags & MONITORINFOF_PRIMARY) ? 1 : 0);
            else
                printf("     monitor: hmon=0x%p (no info)\n", (void*)mon);

            hr("CheckDeviceType(HAL, X8R8G8B8, windowed=TRUE)",
               d3d->CheckDeviceType(i, D3DDEVTYPE_HAL, D3DFMT_X8R8G8B8, D3DFMT_X8R8G8B8, TRUE));
            hr("CheckDeviceType(HAL, X8R8G8B8, windowed=FALSE)",
               d3d->CheckDeviceType(i, D3DDEVTYPE_HAL, D3DFMT_X8R8G8B8, D3DFMT_X8R8G8B8, FALSE));
            hr("CheckDeviceType(HAL, R5G6B5, fullscreen)",
               d3d->CheckDeviceType(i, D3DDEVTYPE_HAL, D3DFMT_R5G6B5, D3DFMT_R5G6B5, FALSE));

            for (int mode = 0; mode < 2; mode++) {
                D3DPRESENT_PARAMETERS pp;
                ZeroMemory(&pp, sizeof(pp));
                pp.BackBufferWidth = 640;
                pp.BackBufferHeight = 480;
                pp.BackBufferFormat = D3DFMT_X8R8G8B8;
                pp.BackBufferCount = 1;
                pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
                pp.hDeviceWindow = desk;
                pp.Windowed = TRUE;
                pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
                DWORD flags = mode == 0 ? D3DCREATE_HARDWARE_VERTEXPROCESSING : D3DCREATE_SOFTWARE_VERTEXPROCESSING;
                IDirect3DDevice9* dev = NULL;
                HRESULT ch = d3d->CreateDevice(i, D3DDEVTYPE_HAL, desk, flags, &pp, &dev);
                hr(mode == 0 ? "CreateDevice(640x480 windowed, HW T&L)" : "CreateDevice(640x480 windowed, SW T&L)", ch);
                if (dev) dev->Release();
            }
        }
        d3d->Release();
    }

    printf("=== D3D11 ===\n");
    ID3D11Device* dev11 = NULL; D3D_FEATURE_LEVEL fl; ID3D11DeviceContext* ctx = NULL;
    HRESULT h11 = D3D11CreateDevice(NULL, D3D_DRIVER_TYPE_HARDWARE, NULL, 0, NULL, 0, D3D11_SDK_VERSION, &dev11, &fl, &ctx);
    printf("  D3D11CreateDevice(HARDWARE) = 0x%08X%s\n", (unsigned)h11, h11 == 0 ? "  OK" : "");
    if (SUCCEEDED(h11)) printf("  feature level = 0x%X\n", (unsigned)fl);
    if (dev11) dev11->Release();
    if (ctx) ctx->Release();
    return 0;
}