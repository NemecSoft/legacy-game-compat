# -*- coding: utf-8 -*-
# PE 摸底小工具：节表 / 导入表 / 导出表 / 版本信息 / overlay 里的 ZIP 目录 / 关键词串
import json
import re
import struct
import sys
import zlib


def read(path):
    with open(path, 'rb') as fh:
        return fh.read()


def sections(d):
    pe = struct.unpack_from('<I', d, 0x3c)[0]
    nsec = struct.unpack_from('<H', d, pe + 6)[0]
    optsz = struct.unpack_from('<H', d, pe + 20)[0]
    magic = struct.unpack_from('<H', d, pe + 24)[0]
    so = pe + 24 + optsz
    out = []
    for i in range(nsec):
        o = so + i * 40
        name = d[o:o + 8].split(b'\x00')[0].decode('latin1', 'replace')
        vsz, va, rsz, ro = struct.unpack_from('<IIII', d, o + 8)
        out.append({'name': name, 'va': va, 'vsz': vsz, 'raw': ro, 'rsz': rsz})
    dirs_off = pe + 24 + (112 if magic == 0x20b else 96)
    return pe, magic, out, dirs_off


def r2o(secs, rva):
    for s in secs:
        if s['va'] <= rva < s['va'] + max(s['rsz'], s['vsz']):
            return s['raw'] + (rva - s['va'])
    return None


def cstr(d, off, limit=400):
    if off is None or off >= len(d):
        return ''
    end = d.find(b'\x00', off, off + limit)
    if end < 0:
        end = min(off + limit, len(d))
    return d[off:end].decode('latin1', 'replace')


def imports(d, secs, dirs_off, is64):
    rva, size = struct.unpack_from('<II', d, dirs_off + 8)
    off = r2o(secs, rva)
    res = []
    esz = 8 if is64 else 4
    k = 0
    while off is not None and k < 64:
        base = off + k * 20
        oft = struct.unpack_from('<I', d, base)[0]
        name = struct.unpack_from('<I', d, base + 12)[0]
        first = struct.unpack_from('<I', d, base + 16)[0]
        if oft == 0 and name == 0:
            break
        dll = cstr(d, r2o(secs, name))
        thunk_rva = oft or first
        to = r2o(secs, thunk_rva)
        funcs = []
        j = 0
        while to is not None and j < 400:
            val = struct.unpack_from('<Q' if is64 else '<I', d, to + j * esz)[0]
            if val == 0:
                break
            if not (val & (0x8000000000000000 if is64 else 0x80000000)):
                fo = r2o(secs, (val & 0x7fffffff) + 2)
                funcs.append(cstr(d, fo, 120))
            j += 1
        res.append({'dll': dll, 'count': len(funcs), 'funcs': funcs})
        k += 1
    return res


def exports(d, secs, dirs_off):
    rva, size = struct.unpack_from('<II', d, dirs_off + 32)
    if rva == 0:
        return []
    off = r2o(secs, rva)
    if off is None:
        return []
    nfn, nname = struct.unpack_from('<II', d, off + 20)
    addr, names, ordn = struct.unpack_from('<III', d, off + 28)
    ao, no, oo = r2o(secs, addr), r2o(secs, names), r2o(secs, ordn)
    out = []
    if ao and no and oo:
        for i in range(min(nname, 400)):
            n = struct.unpack_from('<I', d, no + i * 4)[0]
            o = struct.unpack_from('<H', d, oo + i * 2)[0]
            a = struct.unpack_from('<I', d, ao + o * 4)[0]
            out.append({'name': cstr(d, r2o(secs, n), 160), 'rva': '0x%x' % a})
    return out


def version(d, secs, dirs_off):
    rva, size = struct.unpack_from('<II', d, dirs_off + 12 * 8)
    off = r2o(secs, rva)
    if off is None:
        return {}
    blob = d[off:off + size]
    found = {}
    # VS_VERSIONINFO 里的文本键值都是 UTF-16LE
    for m in re.finditer(rb'(?:[\x20-\x7e]\x00){2,64}\x00\x00', blob):
        key = m.group().decode('utf-16-le', 'replace').rstrip('\x00')
        nxt = blob[m.end():m.end() + 4]
        if len(nxt) >= 4:
            vlen = struct.unpack_from('<H', blob, m.end())[0]
            val = blob[m.end() + 2:m.end() + 2 + vlen * 2].decode('utf-16-le', 'replace')
            if key and len(key) < 40 and 0 < len(val) < 200:
                found[key] = val
    return found


def zip_entries(d, start):
    out = []
    i = d.find(b'PK\x03\x04', start)
    while i != -1 and len(out) < 200:
        try:
            n, m = struct.unpack_from('<HH', d, i + 26)
            name = d[i + 30:i + 30 + n].decode('utf-8', 'replace')
            comp, raw = struct.unpack_from('<II', d, i + 18)
            out.append({'name': name, 'comp': comp, 'csize': raw})
        except Exception:
            break
        i = d.find(b'PK\x03\x04', i + 4)
    return out


def main():
    path = sys.argv[1]
    d = read(path)
    pe, magic, secs, dirs_off = sections(d)
    is64 = magic == 0x20b
    end = max(s['raw'] + s['rsz'] for s in secs)
    info = {
        'file': path,
        'size': len(d),
        'arch': 'x64' if is64 else 'x86',
        'subsystem': struct.unpack_from('<H', d, pe + 24 + 68)[0],
        'overlay_start': end,
        'overlay_mb': round((len(d) - end) / 1048576.0, 1),
        'sections': [s['name'] for s in secs],
        'imports': imports(d, secs, dirs_off, is64),
        'exports': exports(d, secs, dirs_off)[:80],
        'version': version(d, secs, dirs_off),
    }
    print(json.dumps(info, ensure_ascii=False, indent=1))
    z = zip_entries(d, end)
    print('--- overlay zip entries: %d ---' % len(z))
    for e in z[:80]:
        print('%s  comp=%d csize=%d' % (e['name'], e['comp'], e['csize']))


main()
