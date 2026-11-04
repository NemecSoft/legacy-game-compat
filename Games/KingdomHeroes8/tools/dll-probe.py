# -*- coding: utf-8 -*-
# 看一个 DLL 里有哪些关键字符串 / 导出名，判断它挂钩点与协议
import re
import struct
import sys


def sections(d):
    pe = struct.unpack_from('<I', d, 0x3c)[0]
    nsec = struct.unpack_from('<H', d, pe + 6)[0]
    optsz = struct.unpack_from('<H', d, pe + 20)[0]
    magic = struct.unpack_from('<H', d, pe + 24)[0]
    so = pe + 24 + optsz
    secs = []
    for i in range(nsec):
        o = so + i * 40
        name = d[o:o + 8].split(b'\x00')[0].decode('latin1', 'replace')
        vsz, va, rsz, ro = struct.unpack_from('<IIII', d, o + 8)
        secs.append((name, va, vsz, ro, rsz))
    return pe, magic, secs, pe + 24 + (112 if magic == 0x20b else 96)


def r2o(secs, rva):
    for name, va, vsz, ro, rsz in secs:
        if va <= rva < va + max(rsz, vsz):
            return ro + (rva - va)
    return None


def exports(d):
    pe, magic, secs, dd = sections(d)
    rva, size = struct.unpack_from('<II', d, dd + 32)
    off = r2o(secs, rva)
    out = []
    if not off:
        return out
    nfn, nname = struct.unpack_from('<II', d, off + 20)
    addr, names, ordn = struct.unpack_from('<III', d, off + 28)
    ao, no, oo = r2o(secs, addr), r2o(secs, names), r2o(secs, ordn)
    if not (ao and no and oo):
        return out
    for i in range(nname):
        n = struct.unpack_from('<I', d, no + i * 4)[0]
        o = struct.unpack_from('<H', d, oo + i * 2)[0]
        e = r2o(secs, n)
        if e:
            out.append(d[e:d.index(b'\x00', e)].decode('latin1', 'replace'))
    return out


def main():
    path = sys.argv[1]
    d = open(path, 'rb').read()
    print('size', len(d))
    ex = exports(d)
    print('exports(%d): %s' % (len(ex), ', '.join(ex[:40])))
    keys = ['STEAM_MASTER_IPC_NAME_OVERRIDE', 'SG8_MINIPIPE_DIR', 'SG8_STEAM_RUNTIME',
            'STEAMAPPID', 'STEAMID', 'MiniPipe', 'challenge', 'SG8Offline', 'Transient',
            'steam_api64', 'steamclient', 'GetSteamRealm', 'CreateNamedPipe', 'LoadLibrary',
            'SetWindowsHookEx', 'QueueUserAPC', 'NtCreateKey', 'RegOpenKey', 'RtlAdjustPrivilege']
    for k in keys:
        for enc, tag in ((k.encode('ascii'), 'a'), (k.encode('utf-16-le'), 'u')):
            c = d.count(enc)
            if c:
                print('%-34s %s x%d' % (k, tag, c))
    print('--- 名字类字符串（含 Steam/Pipe/SG8/Mini） ---')
    seen = set()
    for m in re.finditer(rb'[A-Za-z_][A-Za-z0-9_\.\-]{5,60}', d):
        s = m.group().decode('latin1')
        if re.search(r'(Steam|Pipe|SG8|Mini|Offline|ticket|Ticket)', s):
            if s not in seen:
                seen.add(s)
    for s in sorted(seen)[:60]:
        print('  ', s)


main()
