#include <windows.h>
#include <dxgi.h>
#include <d3d12.h>
#include <cstdio>

int main(){
    IDXGIFactory1* f = NULL;
    HRESULT hr = CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&f);
    printf("CreateDXGIFactory1=0x%08X\n", (unsigned)hr);
    if(!f) return 1;
    for(UINT i=0;;i++){
        IDXGIAdapter1* a = NULL;
        if(f->EnumAdapters1(i,&a) == DXGI_ERROR_NOT_FOUND) break;
        DXGI_ADAPTER_DESC1 d; a->GetDesc1(&d);
        printf("ADAPTER %u vendor=0x%04X device=0x%04X flags=0x%X LUID=%08lX:%08lX\n",
            i, d.VendorId, d.DeviceId, d.Flags,
            (unsigned long)d.AdapterLuid.HighPart, (unsigned long)d.AdapterLuid.LowPart);
        ID3D12Device* dev = NULL;
        hr = D3D12CreateDevice(a, D3D_FEATURE_LEVEL_11_0, __uuidof(ID3D12Device), (void**)&dev);
        printf("  D3D12CreateDevice(FL11_0)=0x%08X dev=%p\n", (unsigned)hr, (void*)dev);
        if(dev){
            D3D12_FEATURE_DATA_DISPLAYABLE disp; ZeroMemory(&disp,sizeof(disp));
            HRESULT h = dev->CheckFeatureSupport(D3D12_FEATURE_DISPLAYABLE, &disp, sizeof(disp));
            printf("  CheckFeatureSupport(DISPLAYABLE)=0x%08X DisplayableTexture=%d SharedResourceCompatibilityTier=%d\n",
                (unsigned)h, (int)disp.DisplayableTexture, (int)disp.SharedResourceCompatibilityTier);
            D3D12_FEATURE_DATA_ARCHITECTURE arch; ZeroMemory(&arch,sizeof(arch));
            h = dev->CheckFeatureSupport(D3D12_FEATURE_ARCHITECTURE, &arch, sizeof(arch));
            printf("  ARCHITECTURE hr=0x%08X NodeIndex=%u TileBasedRenderer=%d UMA=%d CacheCoherentUMA=%d\n",
                (unsigned)h, arch.NodeIndex, (int)arch.TileBasedRenderer, (int)arch.UMA, (int)arch.CacheCoherentUMA);
            dev->Release();
        }
        a->Release();
    }
    f->Release();
    return 0;
}
