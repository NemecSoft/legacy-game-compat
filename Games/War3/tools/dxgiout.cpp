#include <windows.h>
#include <dxgi1_2.h>
#include <cstdio>
#pragma comment(lib,"dxgi")
int main(){
    IDXGIFactory1* f=NULL;
    HRESULT hr=CreateDXGIFactory1(__uuidof(IDXGIFactory1),(void**)&f);
    printf("CreateDXGIFactory1=0x%08X\n",(unsigned)hr);
    if(!f) return 1;
    for(UINT i=0;;i++){
        IDXGIAdapter1* a=NULL;
        if(f->EnumAdapters1(i,&a)==DXGI_ERROR_NOT_FOUND) break;
        DXGI_ADAPTER_DESC1 d; a->GetDesc1(&d);
        wprintf(L"ADAPTER %u: %s  vid=0x%04X did=0x%04X flags=0x%X\n", i, d.Description, d.VendorId, d.DeviceId, d.Flags);
        for(UINT j=0;;j++){
            IDXGIOutput* o=NULL;
            if(a->EnumOutputs(j,&o)==DXGI_ERROR_NOT_FOUND) break;
            DXGI_OUTPUT_DESC od; o->GetDesc(&od);
            wprintf(L"   OUTPUT %u: %s attached=%d  %ld,%ld-%ld,%ld\n", j, od.DeviceName, od.AttachedToDesktop, od.DesktopCoordinates.left, od.DesktopCoordinates.top, od.DesktopCoordinates.right, od.DesktopCoordinates.bottom);
            o->Release();
        }
        a->Release();
    }
    f->Release();
    return 0;
}
