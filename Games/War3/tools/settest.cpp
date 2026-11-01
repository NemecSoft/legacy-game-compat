#include <windows.h>
#include <d3d9.h>
#include <cstdio>
#include <cstdlib>
static void probe(const char* tag){
    IDirect3D9* d3d = Direct3DCreate9(D3D_SDK_VERSION);
    if(!d3d){ printf("[%s] Direct3DCreate9 NULL\n", tag); return; }
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow();
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT h1 = d3d->CheckDeviceType(0, D3DDEVTYPE_HAL, D3DFMT_X8R8G8B8, D3DFMT_X8R8G8B8, TRUE);
    HRESULT h2 = d3d->CreateDevice(0, D3DDEVTYPE_HAL, GetDesktopWindow(), D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, &dev);
    printf("[%s] CheckDeviceType=0x%08X CreateDevice=0x%08X\n", tag, (unsigned)h1, (unsigned)h2);
    if(dev) dev->Release();
    d3d->Release();
}
int main(int argc, char** argv){
    int hz = argc>1 ? atoi(argv[1]) : 0;
    DEVMODEA dm; ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
    EnumDisplaySettingsA(NULL, ENUM_CURRENT_SETTINGS, &dm);
    printf("before: %lux%lu %lubpp %luHz\n", dm.dmPelsWidth, dm.dmPelsHeight, dm.dmBitsPerPel, dm.dmDisplayFrequency);
    probe("before");
    if(hz>0){
        DEVMODEA m; ZeroMemory(&m,sizeof(m)); m.dmSize=sizeof(m);
        EnumDisplaySettingsA(NULL, ENUM_CURRENT_SETTINGS, &m);
        m.dmDisplayFrequency = hz; m.dmFields |= DM_DISPLAYFREQUENCY;
        LONG r = ChangeDisplaySettingsA(&m, CDS_UPDATEREGISTRY);
        printf("ChangeDisplaySettings(%dHz) -> %ld\n", hz, r);
        Sleep(2000);
        ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
        EnumDisplaySettingsA(NULL, ENUM_CURRENT_SETTINGS, &dm);
        printf("after: %lux%lu %lubpp %luHz\n", dm.dmPelsWidth, dm.dmPelsHeight, dm.dmBitsPerPel, dm.dmDisplayFrequency);
        probe("after");
    }
    return 0;
}
