#include <windows.h>
#include <d3d9.h>
#include <cstdio>
static void path(HMODULE h, const char* tag){ char b[MAX_PATH]={0}; GetModuleFileNameA(h,b,MAX_PATH); printf("%s: %s\n", tag, b); }
int main(){
    IDirect3D9* d3d=Direct3DCreate9(D3D_SDK_VERSION);
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow();
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT hr=d3d->CreateDevice(0,D3DDEVTYPE_HAL,GetDesktopWindow(),D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    printf("CreateDevice=0x%08X\n",(unsigned)hr);
    HMODULE u=GetModuleHandleA("atiumd64.dll"); if(u) path(u,"atiumd64"); else printf("atiumd64 not loaded\n");
    HMODULE x=GetModuleHandleA("atidxx64.dll"); if(x) path(x,"atidxx64"); else printf("atidxx64 not loaded\n");
    return 0;
}
