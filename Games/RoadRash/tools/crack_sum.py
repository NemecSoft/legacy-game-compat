import os, glob, struct, zlib
bak = r'D:\AI\legacy-game-compat\Games\RoadRash\backup\saves-20261031-刘沛松'
d = r'D:\YunGame\Race\RoadRash'
samples = []
for f in sorted(glob.glob(os.path.join(bak, '**', '*.rrs'), recursive=True)):
    b = open(f, 'rb').read()
    if len(b) == 160:
        samples.append(b)
print('样本 %d 个' % len(samples))
def u32(b, i): return struct.unpack('<I', b[i:i+4])[0]
def u16(b, i): return struct.unpack('<H', b[i:i+2])[0]

def sum32(b, s, e):  return sum(u32(b, i) for i in range(s, e, 4)) & 0xFFFFFFFF
def sum16(b, s, e):  return sum(u16(b, i) for i in range(s, e, 2)) & 0xFFFFFFFF
def sum8(b, s, e):   return sum(b[s:e]) & 0xFFFFFFFF
def xor32(b, s, e):
    x = 0
    for i in range(s, e, 4): x ^= u32(b, i)
    return x
def xor16(b, s, e):
    x = 0
    for i in range(s, e, 2): x ^= u16(b, i)
    return x
ops = {'sum32': sum32, 'sum16': sum16, 'sum8': sum8, 'xor32': xor32, 'xor16': xor16,
       'crc32': lambda b, s, e: zlib.crc32(b[s:e]) & 0xFFFFFFFF,
       'adler': lambda b, s, e: zlib.adler32(b[s:e]) & 0xFFFFFFFF}
ranges = [(4,160),(8,160),(0,4),(0,160),(4,156),(12,160),(16,160),(20,160),(4,20),(24,160)]
found = []
for name, fn in ops.items():
    for s, e in ranges:
        for mode in ('raw', 'neg', 'not', 'sub-hdr'):
            ok = 0
            for b in samples:
                v = fn(b, s, e); h = u32(b, 0)
                c = {'raw': v, 'neg': (-v) & 0xFFFFFFFF, 'not': (~v) & 0xFFFFFFFF,
                     'sub-hdr': (v - h) & 0xFFFFFFFF}[mode]
                if c == h: ok += 1
            if ok: found.append((name, s, e, mode, ok))
print('命中：', found or '常见校验算法全不匹配（可能是自定义/查表/与文件名相关）')
# 看头部 4 字节与内容的关系：两两对比，改一个字节会不会改头部
import collections
print('头部值分布（前 6）:', [b[:4].hex() for b in samples[:6]])
