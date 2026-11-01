#include <windows.h>
#include <d3d9.h>
#include <cstdio>
int main(){
    IDirect3D9* d3d = Direct3DCreate9(D3D_SDK_VERSION);
    printf("d3d9 module: %p\n", (void*)GetModuleHandleA("d3d9.dll"));
    printf("atiumd64 loaded: %p\n", (void*)GetModuleHandleA("atiumd64.dll"));
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp, sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow();
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT hr = d3d ? d3d->CreateDevice(0, D3DDEVTYPE_HAL, GetDesktopWindow(), D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, &dev) : E_FAIL;
    printf("CreateDevice = 0x%08X\n", (unsigned)hr);
    printf("atiumd64 after create: %p\n", (void*)GetModuleHandleA("atiumd64.dll"));
    printf("atidxx64 after create: %p\n", (void*)GetModuleHandleA("atidxx64.dll"));
    printf("aticfx64 after create: %p\n", (void*)GetModuleHandleA("aticfx64.dll"));
    return 0;
}
