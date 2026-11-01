#include <windows.h>
#include <d3d9.h>
#include <cstdio>

int main(){
    IDirect3D9* d = Direct3DCreate9(D3D_SDK_VERSION);
    if(!d){ puts("Direct3DCreate9 failed"); return 1; }
    UINT n = d->GetAdapterCount();
    printf("adapters=%u\n", n);
    for(UINT i=0;i<n;i++){
        D3DADAPTER_IDENTIFIER9 id; ZeroMemory(&id,sizeof(id));
        HRESULT hr = d->GetAdapterIdentifier(i,0,&id);
        printf("D3D adapter %u hr=0x%08X\n", i, (unsigned)hr);
        printf("  DeviceName=\"%s\"\n  Description=\"%s\"\n  Driver=\"%s\"\n", id.DeviceName, id.Description, id.Driver);
        printf("  Vendor=0x%04X Device=0x%04X SubSys=0x%08X Rev=0x%04X WHQL=%u\n",
            id.VendorId, id.DeviceId, (unsigned)id.SubSysId, id.Revision, id.WHQLLevel);
        D3DDISPLAYMODE dm; ZeroMemory(&dm,sizeof(dm));
        hr = d->GetAdapterDisplayMode(i,&dm);
        printf("  DisplayMode hr=0x%08X %ux%u fmt=%u\n", (unsigned)hr, dm.Width, dm.Height, dm.Format);
        UINT mc = d->GetAdapterModeCount(i,D3DFMT_X8R8G8B8);
        printf("  ModeCount=%u\n", mc);
        for(UINT m=0;m<mc && m<6;m++){
            D3DDISPLAYMODE mm; ZeroMemory(&mm,sizeof(mm));
            if(SUCCEEDED(d->EnumAdapterModes(i,D3DFMT_X8R8G8B8,m,&mm)))
                printf("    mode[%u] %ux%u@%u fmt=%u\n", m, mm.Width, mm.Height, mm.RefreshRate, mm.Format);
        }
    }
    d->Release();

    printf("--- GDI display devices ---\n");
    DISPLAY_DEVICEW dd; ZeroMemory(&dd,sizeof(dd)); dd.cb=sizeof(dd);
    for(DWORD i=0; EnumDisplayDevicesW(NULL,i,&dd,0); i++){
        wprintf(L"GDI %u name=%ls str=%ls id=%ls flags=0x%X\n", i, dd.DeviceName, dd.DeviceString, dd.DeviceID, dd.StateFlags);
        DISPLAY_DEVICEW mon; ZeroMemory(&mon,sizeof(mon)); mon.cb=sizeof(mon);
        for(DWORD j=0; EnumDisplayDevicesW(dd.DeviceName,j,&mon,0); j++)
            wprintf(L"    MON %u name=%ls str=%ls id=%ls flags=0x%X\n", j, mon.DeviceName, mon.DeviceString, mon.DeviceID, mon.StateFlags);
        ZeroMemory(&dd,sizeof(dd)); dd.cb=sizeof(dd);
    }

    printf("--- current display settings ---\n");
    DEVMODEW cur; ZeroMemory(&cur,sizeof(cur)); cur.dmSize=sizeof(cur);
    if(EnumDisplaySettingsW(NULL, ENUM_CURRENT_SETTINGS, &cur))
        wprintf(L"current %lux%lu %luhz %lubpp\n", cur.dmPelsWidth, cur.dmPelsHeight, cur.dmDisplayFrequency, cur.dmBitsPerPel);
    else printf("EnumDisplaySettings(NULL,current) failed err=%lu\n", GetLastError());
    return 0;
}
