# -*- coding: utf-8 -*-
# 32 位老游戏的导入表探针：找出「拿到 0 就除」的可疑 API 出自哪个 DLL
import os
import struct
import sys


def imports(path):
    d = open(path, 'rb').read()
    pe = struct.unpack_from('<I', d, 0x3c)[0]
    optsz = struct.unpack_from('<H', d, pe + 20)[0]
    magic = struct.unpack_from('<H', d, pe + 24)[0]
    nsec = struct.unpack_from('<H', d, pe + 6)[0]
    so = pe + 24 + optsz
    secs = []
    for i in range(nsec):
        o = so + i * 40
        vsz, va, rsz, ro = struct.unpack_from('<IIII', d, o + 8)
        secs.append((va, vsz, ro, rsz))

    def r2o(rva):
        for va, vsz, ro, rsz in secs:
            if va <= rva < va + max(rsz, vsz):
                return ro + (rva - va)
        return None

    dd = pe + 24 + (112 if magic == 0x20b else 96)
    rva, _ = struct.unpack_from('<II', d, dd + 8)
    off = r2o(rva)
    out = {}
    k = 0
    while off is not None and k < 40:
        base = off + k * 20
        oft, ts, name, first = struct.unpack_from('<IIII', d, base)
        if oft == 0 and name == 0:
            break
        no = r2o(name)
        dll = d[no:d.index(b'\x00', no)].decode('latin1') if no is not None else '?'
        to = r2o(oft or first)
        fs = []
        j = 0
        while to is not None and j < 600:
            v = struct.unpack_from('<I', d, to + j * 4)[0]
            if v == 0:
                break
            if not (v & 0x80000000):
                fo = r2o((v & 0x7fffffff) + 2)
                if fo is not None:
                    fs.append(d[fo:d.index(b'\x00', fo)].decode('latin1'))
            j += 1
        out[dll] = fs
        k += 1
    return out


WATCH = ('DeviceCaps', 'DisplaySettings', 'DiskFreeSpace', 'VolumeInformation',
         'GetSystemMetrics', 'timeGet', 'Performance', 'LogicalDrive', 'DriveType',
         'GetTickCount', 'GetSystemPowerStatus', 'GetProfileInt', 'GetProfileString')

for p in sys.argv[1:]:
    print('====', os.path.basename(p))
    try:
        tab = imports(p)
    except Exception as ex:
        print('  解析失败', ex)
        continue
    for dll, fs in sorted(tab.items()):
        keep = [f for f in fs if any(w in f for w in WATCH)]
        print('  %-18s 导入%3d  可疑: %s' % (dll, len(fs), ', '.join(sorted(set(keep))) or '-'))
