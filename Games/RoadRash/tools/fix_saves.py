"""改 .rrs 里的字段并重算游戏自己的校验和。
校验函数从 Roadrash.exe @0x42EFC0 逆向得到（47/47 样本验证）：
  eax = (len 奇偶交替 +/- 字节) ；edi = 字节和 ；esi = sum(剩余长度 * 字节)
  返回 ((eax<<16) | (edi & 0xFFFF)) ^ esi，小端存记录头 0x00-0x03，覆盖 bytes[4:160]。
"""
import os, glob, struct, sys
d = r'D:\YunGame\Race\RoadRash'
OLD = '刘沛松'.encode('gbk'); NEW = b'YunGame'
NAME_OFF, NAME_END = 0x14, 0x20      # 名字区 0x14..0x1F（样本里名字后面全 0）

def cksum(body):
    M = 0xFFFFFFFF; eax = esi = edi = 0
    n = len(body)
    for i, byte in enumerate(body):
        rem = n - i
        esi = (esi + rem * byte) & M
        edi = (edi + byte) & M
        eax = (eax + byte if rem & 1 else eax - byte) & M
    return (((eax << 16) & M) | (edi & 0xFFFF)) ^ esi

def patch(path, name):
    b = bytearray(open(path, 'rb').read())
    if len(b) != 160: return None
    was = bytes(b)
    b[NAME_OFF:NAME_END] = name + b'\x00' * (NAME_END - NAME_OFF - len(name))
    struct.pack_into('<I', b, 0, cksum(bytes(b[4:160])))
    assert struct.unpack('<I', bytes(b[:4]))[0] == cksum(bytes(b[4:160]))
    assert bytes(b[4:8]) == b'\xad\xde\xfe\xca', '魔数被动过'
    diff = [i for i in range(160) if b[i] != was[i]]
    assert all(i < 4 or NAME_OFF <= i < NAME_END for i in diff), '改动越界 %s' % diff
    open(path, 'wb').write(bytes(b))
    return diff

if '--apply' not in sys.argv:
    n = sum(1 for f in glob.glob(os.path.join(d, '**', '*.rrs'), recursive=True)
            if open(f, 'rb').read()[NAME_OFF:NAME_OFF+6] == OLD)
    print('待改 %d 个（演练，加 --apply 生效）' % n); sys.exit(0)

cnt = 0
for f in sorted(glob.glob(os.path.join(d, '**', '*.rrs'), recursive=True)):
    b = open(f, 'rb').read()
    if len(b) != 160 or b[NAME_OFF:NAME_OFF+6] != OLD: continue
    patch(f, NEW); cnt += 1
print('已改名并重算校验和：%d 个文件' % cnt)
bad = 0
for f in glob.glob(os.path.join(d, '**', '*.rrs'), recursive=True):
    b = open(f, 'rb').read()
    if len(b) == 160 and struct.unpack('<I', b[:4])[0] != cksum(b[4:160]): bad += 1
print('全量校验复核：不匹配 %d 个' % bad)
