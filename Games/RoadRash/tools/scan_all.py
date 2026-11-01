import os, re, glob
root = r'D:\YunGame\Race\RoadRash'
pat = re.compile(rb'(?i)\.rrs|rrs\b|ROADSAVE|\.SAV')
for f in glob.glob(os.path.join(root,'*')) + glob.glob(os.path.join(root,'DATA','*')):
    if not os.path.isfile(f): continue
    if os.path.getsize(f) > 60*1024*1024: continue
    try: b=open(f,'rb').read()
    except: continue
    ms=[m for m in pat.finditer(b)]
    if ms:
        print('%-46s %8d  hits=%d' % (os.path.relpath(f, root), os.path.getsize(f), len(ms)))
        for m in ms[:6]:
            ctx=b[max(0,m.start()-40):m.start()+40]
            print('    %08X  %s' % (m.start(), re.sub(r'[^ -~]','.',ctx.decode('latin1')).replace('\r','?').replace('\n','?')))
