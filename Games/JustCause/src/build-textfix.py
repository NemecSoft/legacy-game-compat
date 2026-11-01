"""Build JCTextFix.asi from the vendor JCSubtitleOverlay.asi.

Vendor ASI layout (image base 0x10000000):
  1000349B  test eax,eax / jz 0x100034B8   <- MH_Initialize result check
  100034B8  push offset "d3d9.dll"         <- start of the D3D9 vtable-hook block
  10003554  mov esi, 0xC0BE0F02            <- start of the UTF-8 decoder patch block

The D3D9 block installs a CreateDevice vtable hook that crashes under DXVK
(0xC000001D / 0xC0000005). We replace 5 bytes at 0x1000349B with an unconditional
`jmp 0x10003554`, so the worker thread skips the whole D3D9 block and applies only
the UTF-8 decoder patch.

The decoder patch block also calls MH_CreateHook(match - 0x51, ...). On this exe
build the signature sits at JustCause.exe+0x10A727C and match-0x51 = 0x14A722B,
which is the last byte of a 6-byte `jns` -- MinHook builds a trampoline on a
mid-instruction address and the game dies with 0xC000001D. We replace the
`call MH_CreateHook` at 0x10003809 with `mov eax,1` (a MinHook error code) so the
inline 25-byte decoder patch stays and the hook is skipped.

Patch site choice matters: 0x100034B9 carries an IMAGE_REL_BASED_HIGHLOW entry, so
writing a rel32 jump there gets corrupted when the DLL is relocated. Both sites
used here have no relocation entries.
"""
import struct, sys

SRC = r"D:\AI\legacy-game-compat\Games\JustCause\src\vendor\JCSubtitleOverlay.asi"
DST = r"D:\AI\legacy-game-compat\Games\JustCause\src\JCTextFix.asi"

RVA_PATCH = 0x349B          # 0x1000349B
RVA_TARGET = 0x3554         # 0x10003554
EXPECT = bytes.fromhex("85c074 19b9".replace(" ", ""))

def rva2off(data, rva):
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    nsec = struct.unpack_from('<H', data, pe + 6)[0]
    opt = pe + 24
    opt_size = struct.unpack_from('<H', data, pe + 20)[0]
    for i in range(nsec):
        off = opt + opt_size + i * 40
        vsize, va, rawsize, rawptr = struct.unpack_from('<IIII', data, off + 8)
        if va <= rva < va + max(vsize, rawsize):
            return rawptr + (rva - va)
    raise SystemExit("rva %x not mapped" % rva)

data = bytearray(open(SRC, 'rb').read())

def patch(site, expect, new):
    off = rva2off(data, site)
    old = bytes(data[off:off + len(expect)])
    assert old == expect, "unexpected bytes at %x: %s" % (site, old.hex())
    data[off:off + len(new)] = new
    print("patched %x: %s -> %s" % (site, old.hex(), new.hex()))

patch(RVA_PATCH, EXPECT, b"\xE9" + struct.pack('<i', RVA_TARGET - (RVA_PATCH + 5)))
patch(0x3809, bytes.fromhex("e8f2e8ffff"), bytes.fromhex("b801000000"))

open(DST, 'wb').write(data)
print("wrote", DST)

