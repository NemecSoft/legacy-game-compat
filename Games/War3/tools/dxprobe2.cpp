#include <windows.h>
#include <d3d9.h>
#include <psapi.h>
#include <cstdio>
#include <string>

static const char* base(const char* p) {
    const char* s = p;
    for (const char* q = p; *q; ++q) if (*q == '\\' || *q == '/') s = q + 1;
    return s;
}

static void listModules() {
    printf("=== loaded modules (own process) ===\n");
    HMODULE mods[512]; DWORD need = 0;
    if (!EnumProcessModules(GetCurrentProcess(), mods, sizeof(mods), &need)) return;
    size_t n = need / sizeof(HMODULE);
    for (size_t i = 0; i < n; i++) {
        char path[MAX_PATH] = {0};
        GetModuleFileNameA(mods[i], path, MAX_PATH);
        std::string s = path;
        bool system = (s.rfind("C:\\Windows\\", 0) == 0);
        if (!system || s.find("amdihk") != std::string::npos)
            printf("  %s\n", path);
    }
}

int main() {
    printf("=== D3D9Ex ===\n");
    HMODULE h = LoadLibraryA("d3d9.dll");
    printf("LoadLibrary(d3d9.dll) module = %p\n", (void*)h);
    char path[MAX_PATH] = {0};
    if (h) { GetModuleFileNameA(h, path, MAX_PATH); printf("  resolved to: %s\n", path); }
    typedef HRESULT (WINAPI *PFN_CREATE9EX)(UINT, IDirect3D9Ex**);
    PFN_CREATE9EX p9ex = h ? (PFN_CREATE9EX)GetProcAddress(h, "Direct3DCreate9Ex") : NULL;
    if (p9ex) {
        IDirect3D9Ex* ex = NULL;
        HRESULT r = p9ex(D3D_SDK_VERSION, &ex);
        printf("Direct3DCreate9Ex = 0x%08X\n", (unsigned)r);
        if (SUCCEEDED(r) && ex) {
            UINT n = ex->GetAdapterCount();
            printf("  adapter count = %u\n", n);
            for (UINT i = 0; i < n; i++) {
                D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp, sizeof(pp));
                pp.BackBufferWidth = 640; pp.BackBufferHeight = 480;
                pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
                pp.SwapEffect = D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow = GetDesktopWindow();
                pp.Windowed = TRUE; pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
                IDirect3DDevice9Ex* dev = NULL;
                HRESULT ch = ex->CreateDeviceEx(i, D3DDEVTYPE_HAL, GetDesktopWindow(),
                    D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, NULL, &dev);
                printf("  [%u] CreateDeviceEx(windowed) = 0x%08X\n", i, (unsigned)ch);
                if (dev) dev->Release();
            }
            ex->Release();
        }
    } else {
        printf("Direct3DCreate9Ex not found\n");
    }

    printf("=== D3D9 (plain) + module list ===\n");
    IDirect3D9* d3d = Direct3DCreate9(D3D_SDK_VERSION);
    if (d3d) {
        D3DPRESENT_PARAMETERS pp; ZeroMemory(&pp, sizeof(pp));
        pp.BackBufferWidth = 640; pp.BackBufferHeight = 480;
        pp.BackBufferFormat = D3DFMT_X8R8G8B8; pp.BackBufferCount = 1;
        pp.SwapEffect = D3DSWAPEFFECT_DISCARD; pp.hDeviceWindow = GetDesktopWindow();
        pp.Windowed = TRUE; pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
        IDirect3DDevice9* dev = NULL;
        HRESULT ch = d3d->CreateDevice(0, D3DDEVTYPE_HAL, GetDesktopWindow(),
            D3DCREATE_HARDWARE_VERTEXPROCESSING, &pp, &dev);
        printf("CreateDevice = 0x%08X\n", (unsigned)ch);
        if (dev) dev->Release();
        d3d->Release();
    }
    listModules();
    return 0;
}