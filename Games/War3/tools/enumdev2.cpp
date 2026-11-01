#include <windows.h>
#include <cstdio>
int main(){
    for(int i=0;i<12;i++){
        DISPLAY_DEVICEW dd; ZeroMemory(&dd,sizeof(dd)); dd.cb=sizeof(dd);
        if(!EnumDisplayDevicesW(NULL,i,&dd,0)) break;
        char name[64]={0}, str[256]={0};
        WideCharToMultiByte(CP_ACP,0,dd.DeviceName,-1,name,64,NULL,NULL);
        WideCharToMultiByte(CP_ACP,0,dd.DeviceString,-1,str,256,NULL,NULL);
        DEVMODEW dm; ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
        BOOL ok=EnumDisplaySettingsW(dd.DeviceName, ENUM_CURRENT_SETTINGS, &dm);
        printf("DEV %d  %-16s flags=0x%02lx  \"%s\"  cur=%s", i, name, dd.StateFlags, str, ok?"OK":"FAIL");
        if(ok) printf(" %lux%lu %lubpp %luHz\n", dm.dmPelsWidth, dm.dmPelsHeight, dm.dmBitsPerPel, dm.dmDisplayFrequency);
        else printf(" (err=%lu)\n", GetLastError());
    }
    return 0;
}
