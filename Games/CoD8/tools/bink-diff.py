# 全量导出 diff：hook 版 vs org 版
import struct

def exports(path):
    with open(path, 'rb') as f:
        data = f.read()
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    nsec = struct.unpack_from('<H', data, pe+6)[0]
    opt_size = struct.unpack_from('<H', data, pe+20)[0]
    opt = pe + 24
    magic = struct.unpack_from('<H', data, opt)[0]
    ddir = opt + (96 if magic == 0x10b else 112)
    exp_rva, exp_size = struct.unpack_from('<II', data, ddir)
    sec0 = opt + opt_size
    secs = []
    for i in range(nsec):
        o = sec0 + i*40
        va, vsz, raw, rawsz = struct.unpack_from('<I', data, o+12)[0], struct.unpack_from('<I', data, o+8)[0], struct.unpack_from('<I', data, o+20)[0], struct.unpack_from('<I', data, o+16)[0]
        secs.append((va, vsz, raw, rawsz))
    def rva2off(rva):
        for va, vsz, raw, rawsz in secs:
            if va <= rva < va + max(vsz, rawsz):
                return raw + (rva - va)
    o = rva2off(exp_rva)
    nfuncs, nnames = struct.unpack_from('<II', data, o+20)[0], struct.unpack_from('<I', data, o+24)[0]
    aor = struct.unpack_from('<I', data, o+28)[0]  # ordinal base
    names_rva = struct.unpack_from('<I', data, o+32)[0]
    eator = struct.unpack_from('<I', data, o+36)[0]
    no = rva2off(names_rvo if False else names_rva)
    names = []
    for i in range(nnames):
        nrva = struct.unpack_from('<I', data, no+4*i)[0]
        off = rva2off(nrva)
        e = data.index(b'\0', off)
        names.append((struct.unpack_from('<H', data, rva2off(eator)+2*i)[0], data[off:e].decode(errors='replace'), struct.unpack_from('<I', data, rva2off(oft_rva) if False else rva2off(struct.unpack_from('<I', data, o)) or 0)))
    return names

base = r'D:\YunGame\CoD\CoD8'
a = sorted((o, n) for _, n, _ in exports(base + r'\binkw32.dll'))
b = sorted((o, n) for _, n, _ in exports(base + r'\binkw32org.dll'))
sa = set(a); sb = set(b)
print('hook 版有而 org 版没有:', sorted(sa - sb) or '无')
print('org 版有而 hook 版没有:', sorted(sb - sa) or '无')
print('两版导出数量:', len(a), len(b))
