#include <windows.h>
#include <d3d9.h>
#include <cstdio>
static LRESULT CALLBACK WP(HWND h,UINT m,WPARAM w,LPARAM l){ return DefWindowProc(h,m,w,l); }
int main(){
    WNDCLASSA wc={0}; wc.lpfnWndProc=WP; wc.hInstance=GetModuleHandle(NULL); wc.lpszClassName="d3dtest";
    RegisterClassA(&wc);
    HWND w=CreateWindowA("d3dtest","t",WS_OVERLAPPEDWINDOW,0,0,640,480,NULL,NULL,wc.hInstance,NULL);
    IDirect3D9* d3d=Direct3DCreate9(D3D_SDK_VERSION);
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=w;
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT h1=d3d->CheckDeviceType(0,D3DDEVTYPE_HAL,D3DFMT_X8R8G8B8,D3DFMT_X8R8G8B8,TRUE);
    HRESULT h2=d3d->CreateDevice(0,D3DDEVTYPE_HAL,w,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    printf("real-window CheckDeviceType=0x%08X CreateDevice=0x%08X\n",(unsigned)h1,(unsigned)h2);
    if(!dev){
        // 试 16 位色 + 全屏（War3 常用）
        pp.BackBufferFormat=D3DFMT_R5G6B5; pp.Windowed=FALSE; pp.FullScreen_RefreshRateInHz=60; pp.hDeviceWindow=w;
        HRESULT h3=d3d->CreateDevice(0,D3DDEVTYPE_HAL,w,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
        printf("fullscreen R5G6B5 640x480@60 CreateDevice=0x%08X\n",(unsigned)h3);
    }
    if(dev) dev->Release();
    if(d3d) d3d->Release();
    DestroyWindow(w);
    return 0;
}
