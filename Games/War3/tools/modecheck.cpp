#include <windows.h>
#include <cstdio>
int main(){
    DEVMODEA dm; ZeroMemory(&dm,sizeof(dm)); dm.dmSize=sizeof(dm);
    if(EnumDisplaySettingsA(NULL, ENUM_CURRENT_SETTINGS, &dm))
        printf("current: %lux%lu %ubpp %luHz\n", dm.dmPelsWidth, dm.dmPelsHeight, dm.dmBitsPerPel, dm.dmDisplayFrequency);
    else printf("current: query failed %lu\n", GetLastError());
    printf("modes at 1920x1080 32bpp:\n");
    for(DWORD i=0;;i++){
        DEVMODEA m; ZeroMemory(&m,sizeof(m)); m.dmSize=sizeof(m);
        if(!EnumDisplaySettingsA(NULL, i, &m)) break;
        if(m.dmPelsWidth==1920 && m.dmPelsHeight==1080 && m.dmBitsPerPel==32)
            printf("  %lux%lu %lubpp %luHz flags=0x%lx\n", m.dmPelsWidth,m.dmPelsHeight,m.dmBitsPerPel,m.dmDisplayFrequency,m.dmFields);
    }
    printf("total modes: %d\n", (int)0);
    int n=0; for(DWORD i=0;;i++){ DEVMODEA m; ZeroMemory(&m,sizeof(m)); m.dmSize=sizeof(m); if(!EnumDisplaySettingsA(NULL,i,&m)) break; n++; }
    printf("total modes = %d\n", n);
    return 0;
}
