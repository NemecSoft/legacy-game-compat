// RoadRash launch gate proxy DLL.
// Replaces the game's own AWEMan32.dll. DllMain checks for a secret env var
// injected by game_starter.exe. If present: forward 4 exports (by ordinal) to
// the real DLL (AWEMan32_real.dll) so the game runs. If absent (double-click):
// terminate silently so the game never starts.
// Env var is used instead of a command-line token because this game pops an
// "Invalid argument" dialog on unknown args; env vars have no such side effect.
// The game imports AWEManager by Ordinal 2, so this proxy must export by
// ordinal too. Real DLL has only 4 exports with names that MSVC .def forwarding
// cannot reference reliably, so stubs resolve real addresses by ordinal at load.

#include <windows.h>

#define GATE_ENV  "YUN_GATE_RR"
#define GATE_VAL  "RR-GATE-OK"
#define REAL_DLL  "AWEMan32_real.dll"

// Real DLL export ordinals (base = 2):
//   2 -> AWEManager       (rva 0x17D0)
//   3 -> AWEGetObject32   (rva 0x1F50)
//   4 -> AWESetObject16   (rva 0x1EB0)
//   5 -> AWEGetObject16   (rva 0x1F00)
typedef FARPROC PFN;
static PFN real_fn[4] = { NULL, NULL, NULL, NULL };   // index 0..3 -> ordinal 2..5
static HMODULE hReal = NULL;

static void load_real(void)
{
    if (hReal != NULL) return;
    hReal = LoadLibraryA(REAL_DLL);
    if (hReal == NULL) return;
    real_fn[0] = GetProcAddress(hReal, (LPCSTR)2);
    real_fn[1] = GetProcAddress(hReal, (LPCSTR)3);
    real_fn[2] = GetProcAddress(hReal, (LPCSTR)4);
    real_fn[3] = GetProcAddress(hReal, (LPCSTR)5);
}

// Jmp stubs: hand control to the real function with all registers and the
// original stack untouched, so any calling convention / signature the game
// expects is preserved.
__declspec(naked) void stub_2(void) { __asm { jmp dword ptr [real_fn]      } }
__declspec(naked) void stub_3(void) { __asm { jmp dword ptr [real_fn + 4]  } }
__declspec(naked) void stub_4(void) { __asm { jmp dword ptr [real_fn + 8]  } }
__declspec(naked) void stub_5(void) { __asm { jmp dword ptr [real_fn + 12] } }

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        char buf[64];
        DWORD n = GetEnvironmentVariableA(GATE_ENV, buf, sizeof(buf));
        if (n == 0 || n >= sizeof(buf) || lstrcmpA(buf, GATE_VAL) != 0)
        {
            TerminateProcess(GetCurrentProcess(), 0);
            return FALSE;
        }
        DisableThreadLibraryCalls(hModule);
        load_real();
        if (hReal == NULL ||
            real_fn[0] == NULL || real_fn[1] == NULL ||
            real_fn[2] == NULL || real_fn[3] == NULL)
        {
            TerminateProcess(GetCurrentProcess(), 0);
            return FALSE;
        }
    }
    else if (reason == DLL_PROCESS_DETACH)
    {
        if (hReal != NULL) FreeLibrary(hReal);
    }
    return TRUE;
}
