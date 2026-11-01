import os, shutil, glob, sys, time
d = r'D:\YunGame\Race\RoadRash'
bak = r'D:\AI\legacy-game-compat\Games\RoadRash\backup\saves-20261031-刘沛松'
OLD = '刘沛松'.encode('gbk')            # C1 F5 C5 E6 CB C9
NEW = b'YunGame'                        # 7 ASCII bytes
apply = '--apply' in sys.argv

files = sorted(set(glob.glob(os.path.join(d, '**', '*.rrs'), recursive=True) +
                   glob.glob(os.path.join(d, '**', '*.RRS'), recursive=True)))
targets = []
for f in files:
    b = open(f, 'rb').read()
    if len(b) == 160 and b[0x14:0x1A] == OLD:
        targets.append(f)
print('扫描 %d 个 .rrs，其中 0x14 处是「刘沛松」的：%d 个' % (len(files), len(targets)))
if not apply:
    print('（演练模式，加 --apply 才真改）')
    for t in targets[:5]: print('   ', os.path.relpath(t, d))
    sys.exit(0)

# 1) 备份（整树原样复制，保留相对路径与时间戳）
n = 0
for t in targets:
    rel = os.path.relpath(t, d)
    dst = os.path.join(bak, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if not os.path.exists(dst):
        shutil.copy2(t, dst); n += 1
print('已备份 %d 个文件 -> %s' % (n, bak))

# 2) 改写：0x14 起写 YunGame + \0，占 8 字节（原来 6 字节名 + 2 个 0），不挪动后面任何字段
changed = 0
for t in targets:
    b = bytearray(open(t, 'rb').read())
    if bytes(b[0x14:0x14+len(NEW)]) == NEW:      # 已改过
        continue
    b[0x14:0x1C] = NEW + b'\x00' * (8 - len(NEW))
    assert bytes(b[0x1C:0x20]) == b'\x00\x00\x00\x00', '名字区越界'
    open(t, 'wb').write(bytes(b)); changed += 1
print('已改写 %d 个文件' % changed)

# 3) 自检：名字区、总长度、其余字段未动
bad = []
for t in targets:
    now = open(t, 'rb').read()
    was = open(os.path.join(bak, os.path.relpath(t, d)), 'rb').read()
    diff = [i for i in range(160) if now[i] != was[i]]
    if len(now) != 160 or now[0x14:0x1B] != NEW or any(not (0x14 <= i <= 0x1B) for i in diff):
        bad.append((os.path.relpath(t, d), diff))
print('自检：改动只落在 0x14-0x1B 的文件 %d/%d；异常 %d' % (len(targets)-len(bad), len(targets), len(bad)))
for r in bad[:5]: print('   !', r)
