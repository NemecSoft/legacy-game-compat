# 对比 binkw32.dll（hook 版）与 binkw32org.dll 的 PE 机器类型、导入、导出
import struct, sys

def parse(path):
    with open(path, 'rb') as f:
        data = f.read()
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    assert data[pe:pe+4] == b'PE\0\0', path
    machine = struct.unpack_from('<H', data, pe+4)[0]
    nsec = struct.unpack_from('<H', data, pe+6)[0]
    opt_size = struct.unpack_from('<H', data, pe+20)[0]
    opt = pe + 24
    magic = struct.unpack_from('<H', data, opt)[0]
    ddir = opt + (96 if magic == 0x10b else 112)
    exp_rva, exp_size = struct.unpack_from('<II', data, ddir)
    imp_rva, imp_size = struct.unpack_from('<II', data, ddir+8)
    # section table
    sec0 = opt + opt_size
    secs = []
    for i in range(nsec):
        o = sec0 + i*40
        name = data[o:o+8].rstrip(b'\0').decode(errors='replace')
        va, vsz = struct.unpack_from('<I', data, o+12)[0], struct.unpack_from('<I', data, o+8)[0]
        raw, rawsz = struct.unpack_from('<I', data, o+20)[0], struct.unpack_from('<I', data, o+16)[0]
        secs.append((name, va, vsz, raw, rawsz))
    def rva2off(rva):
        for name, va, vsz, raw, rawsz in secs:
            if va <= rva < va + max(vsz, rawsz):
                return raw + (rva - va)
        return None
    def cstr(off):
        e = data.index(b'\0', off)
        return data[off:e].decode(errors='replace')
    print(f'== {path}')
    print(f'  机器: {machine:#06x} ({ "x86" if machine==0x14c else "x64" if machine==0x8664 else "?" })  大小: {len(data)}')
    # 导出
    if exp_rva:
        o = rva2off(exp_rva)
        if o is not None:
            name_rva = struct.unpack_from('<I', data, o+12)[0]
            nfuncs, nnames = struct.unpack_from('<II', data, o+20)[0], struct.unpack_from('<I', data, o+24)[0]
            dllname = cstr(rva2off(name_rva)) if name_rva else '?'
            print(f'  导出DLL自称: {dllname}  函数数: {nfuncs}  名称数: {nnames}')
            names_rva = struct.unpack_from('<I', data, o+32)[0]
            no = rva2off(names_rva)
            names = [cstr(rva2off(struct.unpack_from('<I', data, no+4*i)[0])) for i in range(nnames)]
            for n in names[:40]:
                print(f'    导出: {n}')
            if nnames > 40: print(f'    ...共 {nnames} 个')
    else:
        print('  无导出表')
    # 导入 DLL 名
    if imp_rva:
        o = rva2off(imp_rva)
        dlls = []
        while True:
            oft, t, fc, name_rva, fta = struct.unpack_from('<IIIII', data, o)
            if name_rva == 0: break
            dlls.append(cstr(rva2off(name_rva)))
            o += 20
        print('  导入: ' + ', '.join(dlls))

base = r'D:\YunGame\CoD\CoD8'
parse(base + r'\binkw32.dll')
parse(base + r'\binkw32org.dll')
