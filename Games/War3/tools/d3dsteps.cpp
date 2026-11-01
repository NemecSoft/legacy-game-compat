#include <windows.h>
#include <d3d9.h>
#include <cstdio>
int main(){
    IDirect3D9* d=Direct3DCreate9(D3D_SDK_VERSION);
    if(!d){ printf("create9 NULL\n"); return 1; }
    printf("Adapters=%u\n", d->GetAdapterCount());
    D3DADAPTER_IDENTIFIER9 id; ZeroMemory(&id,sizeof(id));
    printf("GetAdapterIdentifier=0x%08X\n",(unsigned)d->GetAdapterIdentifier(0,0,&id));
    D3DDISPLAYMODE dm; ZeroMemory(&dm,sizeof(dm));
    printf("GetAdapterDisplayMode=0x%08X  %ux%u fmt=%d\n",(unsigned)d->GetAdapterDisplayMode(0,&dm),dm.Width,dm.Height,dm.Format);
    printf("GetAdapterModeCount(X8R8G8B8)=%u\n", d->GetAdapterModeCount(0,D3DFMT_X8R8G8B8));
    D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
    HRESULT hc=d->GetDeviceCaps(0,D3DDEVTYPE_HAL,&caps);
    printf("GetDeviceCaps(HAL)=0x%08X deviceType=%u\n",(unsigned)hc,caps.DeviceType);
    printf("CheckDeviceType windowed=0x%08X\n",(unsigned)d->CheckDeviceType(0,D3DDEVTYPE_HAL,D3DFMT_X8R8G8B8,D3DFMT_X8R8G8B8,TRUE));
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow();
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    printf("CreateDevice(HAL)=0x%08X\n",(unsigned)d->CreateDevice(0,D3DDEVTYPE_HAL,GetDesktopWindow(),D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev));
    if(dev) dev->Release();
    dev=NULL;
    ZeroMemory(&pp,sizeof(pp)); pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow(); pp.Windowed=TRUE;
    printf("CreateDevice(SW)=0x%08X\n",(unsigned)d->CreateDevice(0,D3DDEVTYPE_SW,GetDesktopWindow(),D3DCREATE_SOFTWARE_VERTEXPROCESSING,&pp,&dev));
    if(dev) dev->Release();
    d->Release();
    return 0;
}
