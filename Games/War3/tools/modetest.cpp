#include <windows.h>
#include <d3d9.h>
#include <cstdio>
static LRESULT CALLBACK WP(HWND h,UINT m,WPARAM w,LPARAM l){ return DefWindowProc(h,m,w,l); }
static void probe(const char* tag){
    IDirect3D9* d3d=Direct3DCreate9(D3D_SDK_VERSION);
    if(!d3d){ printf("[%s] create9 null\n",tag); return; }
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=GetDesktopWindow();
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT h=d3d->CreateDevice(0,D3DDEVTYPE_HAL,GetDesktopWindow(),D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    printf("[%s] CreateDevice=0x%08X\n",tag,(unsigned)h);
    if(dev) dev->Release();
    d3d->Release();
}
int main(){
    DEVMODEA orig; ZeroMemory(&orig,sizeof(orig)); orig.dmSize=sizeof(orig);
    EnumDisplaySettingsA(NULL,ENUM_CURRENT_SETTINGS,&orig);
    printf("orig %lux%lu %luHz\n",orig.dmPelsWidth,orig.dmPelsHeight,orig.dmDisplayFrequency);
    struct { int w,h,hz; } modes[] = {{1920,1080,59},{1920,1080,60},{1600,900,60},{1366,768,60},{1024,768,60},{800,600,60},{640,480,60}};
    for(auto& m : modes){
        DEVMODEA dm=orig; dm.dmPelsWidth=m.w; dm.dmPelsHeight=m.h; dm.dmDisplayFrequency=m.hz;
        dm.dmFields=DM_PELSWIDTH|DM_PELSHEIGHT|DM_DISPLAYFREQUENCY;
        LONG r=ChangeDisplaySettingsA(&dm,0);
        char tag[64]; sprintf_s(tag,"%dx%d@%d r=%ld",m.w,m.h,m.hz,r);
        if(r==DISP_CHANGE_SUCCESSFUL){ Sleep(800); probe(tag); }
        else printf("[%s] mode rejected\n",tag);
    }
    DEVMODEA back=orig; back.dmFields=DM_PELSWIDTH|DM_PELSHEIGHT|DM_DISPLAYFREQUENCY;
    ChangeDisplaySettingsA(&back,CDS_UPDATEREGISTRY);
    printf("restored\n");
    return 0;
}
