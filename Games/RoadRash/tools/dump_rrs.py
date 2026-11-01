import os, sys, glob
d = r'D:\YunGame\Race\RoadRash'
pats = ['*.RRS','*.rrs']
files = []
for p in pats:
    files += glob.glob(os.path.join(d, p))
files += glob.glob(os.path.join(d, 'DATA', '*.RRS'))
def hexdump(b, base=0):
    out=[]
    for i in range(0, len(b), 16):
        ch=b[i:i+16]
        a=''.join(chr(c) if 32<=c<127 else '.' for c in ch)
        g=''
        j=0
        while j < len(ch):
            if ch[j]>=0x81 and j+1<len(ch) and ch[j+1]>=0x40:
                try: g+=bytes(ch[j:j+2]).decode('gbk',errors='replace')
                except: g+='??'
                j+=2
            else:
                g+=chr(ch[j]) if 32<=ch[j]<127 else '.'
                j+=1
        out.append('%04X  %-48s |%-16s| %s' % (i+base, ' '.join('%02X'%c for c in ch), a, g))
    return '\n'.join(out)
for f in sorted(files):
    b=open(f,'rb').read()
    print('='*100)
    print(os.path.basename(f), len(b), 'bytes')
    print(hexdump(b))
