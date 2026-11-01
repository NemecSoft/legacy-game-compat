#include <windows.h>
#include <d3d9.h>
#include <cstdio>

static LRESULT CALLBACK WP(HWND h,UINT m,WPARAM w,LPARAM l){ return DefWindowProcW(h,m,w,l); }

static HWND makeWindow(){
    WNDCLASSW wc; ZeroMemory(&wc,sizeof(wc));
    wc.lpfnWndProc=WP; wc.hInstance=GetModuleHandleW(NULL);
    wc.lpszClassName=L"mode2win"; wc.hCursor=LoadCursorW(NULL,MAKEINTRESOURCEW(32512));
    RegisterClassW(&wc);
    HWND h=CreateWindowW(L"mode2win",L"mode2",WS_OVERLAPPEDWINDOW,40,40,800,600,NULL,NULL,wc.hInstance,NULL);
    if(h){ ShowWindow(h,SW_SHOWNORMAL); UpdateWindow(h); }
    return h;
}

static void testD3D(const char* tag){
    IDirect3D9* d=Direct3DCreate9(D3D_SDK_VERSION);
    if(!d){ printf("%s Direct3DCreate9 failed\n",tag); return; }
    D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
    HRESULT hc=d->GetDeviceCaps(0,D3DDEVTYPE_HAL,&caps);
    HWND h=makeWindow();
    D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
    pp.BackBufferWidth=800; pp.BackBufferHeight=600; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
    pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=h;
    pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
    IDirect3DDevice9* dev=NULL;
    HRESULT hd=d->CreateDevice(0,D3DDEVTYPE_HAL,h,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
    printf("%-10s caps=0x%08X create=0x%08X\n",tag,(unsigned)hc,(unsigned)hd);
    if(dev) dev->Release();
    if(h) DestroyWindow(h);
    d->Release();
}

int main(){
    DEVMODEW cur; ZeroMemory(&cur,sizeof(cur)); cur.dmSize=sizeof(cur);
    if(!EnumDisplaySettingsW(NULL,ENUM_CURRENT_SETTINGS,&cur)){ printf("EnumDisplaySettings failed\n"); return 1; }
    wprintf(L"current: %lux%lu @ %luhz %lubpp\n",cur.dmPelsWidth,cur.dmPelsHeight,cur.dmDisplayFrequency,cur.dmBitsPerPel);

    printf("--- all 1920x1080 modes ---\n");
    for(int i=0;;i++){
        DEVMODEW dm; ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
        if(!EnumDisplaySettingsW(NULL,i,&dm)) break;
        if(dm.dmPelsWidth==1920 && dm.dmPelsHeight==1080)
            printf("  idx=%d %lux%lu @ %luhz\n",i,dm.dmPelsWidth,dm.dmPelsHeight,dm.dmDisplayFrequency);
    }

    testD3D("before");

    DEVMODEW want=cur;
    want.dmPelsWidth=1920; want.dmPelsHeight=1080; want.dmDisplayFrequency=60;
    want.dmFields=DM_PELSWIDTH|DM_PELSHEIGHT|DM_DISPLAYFREQUENCY;
    LONG r=ChangeDisplaySettingsW(&want,0);
    printf("ChangeDisplaySettings(1920x1080@60) -> %ld\n",(long)r);
    DEVMODEW now; ZeroMemory(&now,sizeof(now)); now.dmSize=sizeof(now);
    EnumDisplaySettingsW(NULL,ENUM_CURRENT_SETTINGS,&now);
    wprintf(L"after switch: %lux%lu @ %luhz\n",now.dmPelsWidth,now.dmPelsHeight,now.dmDisplayFrequency);
    testD3D("at60");

    DEVMODEW want720=cur;
    want720.dmPelsWidth=1280; want720.dmPelsHeight=720; want720.dmDisplayFrequency=60;
    want720.dmFields=DM_PELSWIDTH|DM_PELSHEIGHT|DM_DISPLAYFREQUENCY;
    r=ChangeDisplaySettingsW(&want720,0);
    printf("ChangeDisplaySettings(1280x720@60) -> %ld\n",(long)r);
    testD3D("at720p");

    r=ChangeDisplaySettingsW(&cur,0);
    printf("restore -> %ld\n",(long)r);
    return 0;
}
