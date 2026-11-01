#include <windows.h>
#include <dxgi.h>
#include <cstdio>

int main(){
    IDXGIFactory1* f = NULL;
    HRESULT hr = CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&f);
    printf("CreateDXGIFactory1=0x%08X\n", (unsigned)hr);
    if(!f) return 1;
    for(UINT i=0;;i++){
        IDXGIAdapter1* a = NULL;
        if(f->EnumAdapters1(i, &a) == DXGI_ERROR_NOT_FOUND) break;
        DXGI_ADAPTER_DESC1 d; a->GetDesc1(&d);
        printf("DXGI %u: LUID=%08lX:%08lX flags=0x%X vendor=0x%04X device=0x%04X\n",
            i, (unsigned long)d.AdapterLuid.HighPart, (unsigned long)d.AdapterLuid.LowPart,
            d.Flags, d.VendorId, d.DeviceId);
        a->Release();
    }
    f->Release();
    return 0;
}
