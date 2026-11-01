#include <windows.h>
#include <cstdio>
int main(){
    for(int i=0;i<10;i++){
        DISPLAY_DEVICEW dd; ZeroMemory(&dd,sizeof(dd)); dd.cb=sizeof(dd);
        if(!EnumDisplayDevicesW(NULL,i,&dd,0)) break;
        wprintf(L"DEV %d %s flags=0x%lx \"%s\"\n", i, dd.DeviceName, dd.StateFlags, dd.DeviceString);
        DEVMODEW dm; ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
        if(EnumDisplaySettingsW(dd.DeviceName, ENUM_CURRENT_SETTINGS, &dm))
            wprintf(L"    current %lux%lu %lubpp %luHz\n", dm.dmPelsWidth, dm.dmPelsHeight, dm.dmBitsPerPel, dm.dmDisplayFrequency);
        else
            wprintf(L"    current: FAILED err=%lu\n", GetLastError());
    }
    return 0;
}
