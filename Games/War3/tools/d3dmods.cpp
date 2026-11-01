#include <windows.h>
#include <d3d9.h>
#include <psapi.h>
#include <cstdio>
#include <string>

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l){ return DefWindowProcW(h,m,w,l); }

static void listMods(){
    printf("--- loaded modules of interest ---\n");
    HMODULE mods[2048]; DWORD need=0;
    if(!EnumProcessModules(GetCurrentProcess(), mods, sizeof(mods), &need)){ printf("EnumProcessModules failed %lu\n", GetLastError()); return; }
    size_t n = need / sizeof(HMODULE);
    for(size_t i=0;i<n;i++){
        char path[MAX_PATH]={0};
        GetModuleFileNameA(mods[i], path, MAX_PATH);
        std::string s = path;
        for(auto& c : s) c = (char)tolower((unsigned char)c);
        if(s.find("atiumd")!=std::string::npos || s.find("atidxx")!=std::string::npos ||
           s.find("amdihk")!=std::string::npos || s.find("amdhdl")!=std::string::npos ||
           s.find("d3d9on12")!=std::string::npos || s.find("amdxc")!=std::string::npos ||
           s.find("\\d3d9.dll")!=std::string::npos || s.find("d3d12")!=std::string::npos)
            printf("  %s\n", path);
    }
}

int main(){
    WNDCLASSW wc; ZeroMemory(&wc,sizeof(wc));
    wc.lpfnWndProc=WndProc; wc.hInstance=GetModuleHandleW(NULL);
    wc.lpszClassName=L"d3dmods"; wc.hCursor=LoadCursorW(NULL,MAKEINTRESOURCEW(32512));
    RegisterClassW(&wc);
    HWND hwnd=CreateWindowW(L"d3dmods",L"mods",WS_OVERLAPPEDWINDOW,30,30,640,480,NULL,NULL,wc.hInstance,NULL);
    if(hwnd){ ShowWindow(hwnd,SW_SHOWNORMAL); UpdateWindow(hwnd); }

    IDirect3D9* d9=Direct3DCreate9(D3D_SDK_VERSION);
    printf("Direct3DCreate9=%p\n",(void*)d9);
    if(d9){
        D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
        HRESULT hc=d9->GetDeviceCaps(0,D3DDEVTYPE_HAL,&caps);
        printf("GetDeviceCaps=0x%08X\n",(unsigned)hc);
        listMods();
        D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp,sizeof(pp));
        pp.BackBufferWidth=640; pp.BackBufferHeight=480; pp.BackBufferFormat=D3DFMT_X8R8G8B8;
        pp.BackBufferCount=1; pp.SwapEffect=D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow=hwnd;
        pp.Windowed=TRUE; pp.PresentationInterval=D3DPRESENT_INTERVAL_IMMEDIATE;
        IDirect3DDevice9* dev=NULL;
        HRESULT hd=d9->CreateDevice(0,D3DDEVTYPE_HAL,hwnd,D3DCREATE_HARDWARE_VERTEXPROCESSING,&pp,&dev);
        printf("CreateDevice=0x%08X\n",(unsigned)hd);
        if(dev) dev->Release();
        d9->Release();
    }

    printf("=== On12 path ===\n");
    typedef IDirect3D9* (WINAPI *PFN)(UINT, void*, UINT);
    HMODULE h9=LoadLibraryW(L"d3d9.dll");
    PFN p=(PFN)GetProcAddress(h9,"Direct3DCreate9On12");
    struct ARGS { BOOL Enable9On12; IUnknown* pDev; IUnknown* q[2]; UINT nq; UINT mask; } args;
    ZeroMemory(&args,sizeof(args)); args.Enable9On12=TRUE;
    IDirect3D9* o12=p?p(D3D_SDK_VERSION,&args,1):NULL;
    printf("Direct3DCreate9On12=%p\n",(void*)o12);
    if(o12){
        D3DCAPS9 caps; ZeroMemory(&caps,sizeof(caps));
        printf("On12 GetDeviceCaps=0x%08X\n",(unsigned)o12->GetDeviceCaps(0,D3DDEVTYPE_HAL,&caps));
        listMods();
        o12->Release();
    }
    if(hwnd) DestroyWindow(hwnd);
    return 0;
}
