#include <windows.h>
#include <d3d9.h>
#include <cstdio>

typedef HRESULT (WINAPI *PFN_CREATE9EX)(UINT, IDirect3D9Ex**);

int main(){
    HMODULE h = LoadLibraryA("d3d9.dll");
    PFN_CREATE9EX p = (PFN_CREATE9EX)GetProcAddress(h, "Direct3DCreate9Ex");
    printf("Direct3DCreate9Ex=%p\n", (void*)p);
    if(!p) return 1;
    IDirect3D9Ex* ex = NULL;
    HRESULT hr = p(D3D_SDK_VERSION, &ex);
    printf("create hr=0x%08X ex=%p\n", (unsigned)hr, (void*)ex);
    if(!ex) return 1;
    UINT n = ex->GetAdapterCount();
    printf("adapters=%u\n", n);
    for(UINT i=0;i<n;i++){
        LUID l; ZeroMemory(&l,sizeof(l));
        HRESULT h2 = ex->GetAdapterLUID(i, &l);
        HMONITOR mon = ex->GetAdapterMonitor(i);
        printf("adapter %u: GetAdapterLUID=0x%08X LUID=%08lX:%08lX monitor=%p\n",
            i, (unsigned)h2, (unsigned long)l.HighPart, (unsigned long)l.LowPart, (void*)mon);
    }
    ex->Release();
    return 0;
}
