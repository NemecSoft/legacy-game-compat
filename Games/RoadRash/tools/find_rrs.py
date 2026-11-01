import re, sys
p = r'D:\YunGame\Race\RoadRash\Roadrash.exe'
b = open(p,'rb').read()
print('exe size', len(b))
# ASCII strings >=4
pat = re.compile(rb'[\x20-\x7e]{4,}')
hits = [m for m in pat.finditer(b) if re.search(rb'(?i)(rrs|\.dat|save|load|DATA\|%s)', m.group())]
seen=set()
for m in hits:
    s = m.group().decode('latin1')
    if s in seen: continue
    seen.add(s)
    if re.search(r'(?i)(rrs|save|load|\.dat|DATA)', s):
        print('%08X  %s' % (m.start(), s))
