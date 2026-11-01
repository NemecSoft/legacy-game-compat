#include <windows.h>
#include <d3d12.h>
#include <cstdio>
int main(){
    ID3D12Device* dev=NULL;
    HRESULT hr=D3D12CreateDevice(NULL, D3D_FEATURE_LEVEL_11_0, __uuidof(ID3D12Device), (void**)&dev);
    printf("D3D12CreateDevice(FL11_0) = 0x%08X %s\n",(unsigned)hr, hr==0?"OK":"FAIL");
    if(dev) dev->Release();
    return 0;
}
