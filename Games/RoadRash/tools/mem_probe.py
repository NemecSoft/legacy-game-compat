import ctypes, ctypes.wintypes as wt, re, sys
k32=ctypes.windll.kernel32
PROCESS_VM_READ=0x0010; PROCESS_QUERY_INFORMATION=0x0400
class MBI(ctypes.Structure):
    _fields_=[("BaseAddress",wt.LPVOID),("AllocationBase",wt.LPVOID),
              ("AllocationProtect",wt.DWORD),("RegionSize",ctypes.c_size_t),
              ("State",wt.DWORD),("Protect",wt.DWORD),("Type",wt.DWORD)]
pid=int(sys.argv[1])
h=k32.OpenProcess(PROCESS_VM_READ|PROCESS_QUERY_INFORMATION, False, pid)
if not h: sys.exit('OpenProcess failed %d' % k32.GetLastError())
addr=0; buf=(ctypes.c_char*0x10000)(); got=ctypes.c_size_t()
chunks=[]; mbi=MBI()
MEM_PRIVATE=0x20000
while addr < 0x7FFF0000:
    if k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
        size=min(mbi.RegionSize, 0x100000)
        if mbi.State==0x1000 and mbi.Protect in (0x04,0x20,0x40,0x08,0x80):
            ok=k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(got))
            if ok and got.value: chunks.append((addr, bytes(buf[:got.value])))
        addr += size
    else: addr += 0x10000
blob=b''.join(c for _,c in chunks)
print('read %d bytes across %d regions' % (len(blob), len(chunks)))
for n in [b'Usage:', b'savefile', b'ignored', b'.RRS', b'.rrs', b'%s : %s']:
    hits=[m.start() for m in re.finditer(re.escape(n), blob)]
    print('%-12s %d hits' % (n.decode(errors='replace'), len(hits)))
    for o in hits[:3]:
        print('    ', re.sub(r'[^ -~]','.',blob[max(0,o-90):o+130].decode('latin1')))
