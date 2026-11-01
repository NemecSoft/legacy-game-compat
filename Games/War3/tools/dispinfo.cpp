#include <windows.h>
#include <cstdio>
#include <vector>
static BOOL CALLBACK MCB(HMONITOR h, HDC dc, LPRECT r, LPARAM l){
    MONITORINFOEXW mi; mi.cbSize=sizeof(mi); GetMonitorInfoW(h,&mi);
    printf("MONITOR %ls %ld,%ld-%ld,%ld primary=%d\n", mi.szDevice, mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right, mi.rcMonitor.bottom, (mi.dwFlags&MONITORINFOF_PRIMARY)?1:0);
    return TRUE;
}
int main(){
    printf("SM_CMONITORS=%d SM_CXSCREEN=%d SM_CYSCREEN=%d\n", GetSystemMetrics(SM_CMONITORS), GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    EnumDisplayMonitors(NULL,NULL,MCB,0);
    DISPLAY_DEVICEW dd; dd.cb=sizeof(dd);
    for(DWORD i=0; EnumDisplayDevicesW(NULL,i,&dd,0); i++){
        printf("ADAPTER %lu %ls flags=0x%lx id=%ls\n", i, dd.DeviceString, dd.StateFlags, dd.DeviceID);
        DISPLAY_DEVICEW mon; mon.cb=sizeof(mon);
        for(DWORD j=0; EnumDisplayDevicesW(dd.DeviceName,j,&mon,0); j++)
            printf("   MON %lu %ls flags=0x%lx\n", j, mon.DeviceString, mon.StateFlags);
    }
    UINT32 np=0, nm=0;
    LONG r=GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS,&np,&nm);
    printf("GetDisplayConfigBufferSizes r=%ld paths=%u modes=%u\n", r, np, nm);
    if(r==ERROR_SUCCESS && np){
        std::vector<DISPLAYCONFIG_PATH_INFO> paths(np); std::vector<DISPLAYCONFIG_MODE_INFO> modes(nm);
        r=QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS,&np,paths.data(),&nm,modes.data(),NULL);
        printf("QueryDisplayConfig r=%ld paths=%u\n", r, np);
        for(UINT32 i=0;i<np;i++){
            DISPLAYCONFIG_SOURCE_DEVICE_NAME src; src.header.type=DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME; src.header.size=sizeof(src); src.header.adapterId=paths[i].sourceInfo.adapterId; src.header.id=paths[i].sourceInfo.id;
            DISPLAYCONFIG_TARGET_DEVICE_NAME tgt; tgt.header.type=DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME; tgt.header.size=sizeof(tgt); tgt.header.adapterId=paths[i].targetInfo.adapterId; tgt.header.id=paths[i].targetInfo.id;
            DisplayConfigGetDeviceInfo(&src.header); DisplayConfigGetDeviceInfo(&tgt.header);
            printf("PATH %u src=%ls tgt=%ls outTech=%d available=%d active=%d statusFlags=0x%x\n", i, src.viewGdiDeviceName, tgt.monitorFriendlyDeviceName, tgt.outputTechnology, (int)tgt.outputTechnology, paths[i].targetInfo.statusFlags);
        }
    }
    return 0;
}
