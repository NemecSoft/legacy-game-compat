# 解析 .NET 元数据字符串堆（#Strings 成员名 / #US 用户字符串），
# 用于不加载程序集就看清自带启动器的启动契约（路径 / 参数 / 文案）。
import struct

p = r'D:\YunGame\Z\PokemonScarletandViolet\启动游戏.exe'
b = open(p, 'rb').read()
e_lfanew = struct.unpack_from('<I', b, 0x3C)[0]
coff = e_lfanew + 4
nsec = struct.unpack_from('<H', b, coff + 2)[0]
optsz = struct.unpack_from('<H', b, coff + 16)[0]
opt = coff + 20
magic = struct.unpack_from('<H', b, opt)[0]
ddir = opt + (112 if magic == 0x20b else 96)
cli_rva, cli_sz = struct.unpack_from('<II', b, ddir + 14 * 8)
secs = []
so = opt + optsz
for i in range(nsec):
    o = so + i * 40
    vsz, va, rsz, ro = struct.unpack_from('<IIII', b, o + 8)
    secs.append((va, vsz, ro, rsz))

def r2o(rva):
    for va, vsz, ro, rsz in secs:
        if va <= rva < va + max(vsz, rsz):
            return ro + (rva - va)
    raise ValueError('rva %x' % rva)

cli = r2o(cli_rva)
md_rva, md_sz = struct.unpack_from('<II', b, cli + 8)
md = r2o(md_rva)
assert b[md:md + 4] == b'BSJB'
vlen = struct.unpack_from('<I', b, md + 12)[0]
off = md + 16 + vlen
flags, nstreams = struct.unpack_from('<HH', b, off)
off += 4
streams = {}
for i in range(nstreams):
    s_off, s_sz = struct.unpack_from('<II', b, off)
    off += 8
    end = b.index(b'\0', off)
    name = b[off:end].decode()
    off = (end + 1 + 3) & ~3
    streams[name] = (md + s_off, s_sz)

so2, sz = streams['#Strings']
print('== #Strings（类型/方法/字段名） ==')
for n in b[so2:so2 + sz].split(b'\0'):
    if n:
        print(n.decode('utf-8', 'replace'))

so2, sz = streams['#US']
print('== #US（字符串字面量） ==')
i = 1
while i < sz:
    c = b[so2 + i]
    if c & 0x80 == 0:
        ln = c; i += 1
    elif c & 0xC0 == 0x80:
        ln = ((c & 0x3f) << 8) | b[so2 + i + 1]; i += 2
    else:
        ln = ((c & 0x1f) << 24) | (b[so2 + i + 1] << 16) | (b[so2 + i + 2] << 8) | b[so2 + i + 3]; i += 4
    if ln == 0:
        continue
    data = b[so2 + i:so2 + i + ln - 1]
    i += ln
    print(repr(data.decode('utf-16-le', 'replace')))