# -*- coding: utf-8 -*-
# 从 CoD2 的 iwd（zip 包）里抽出默认按键、画面选项与安全模式配置。
# 用途：启动器右栏按键表要照游戏真实默认键位写；选层/分辨率要先看清引擎自己支持哪些 cvar。
# 用法：python dump-cfg.py [输出文件]
import zipfile, os, sys, io

GAME = r'D:\YunGame\CoD\CoD2\main'
WANT = [('iw_00.iwd', ['default.cfg', 'safemode.cfg', 'configure.cfg']),
        ('iw_06.iwd', ['ui/options_graphics.cfg', 'ui/options_graphics_set.cfg'])]

out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), '..', 'evidence', 'cod2-cfg.txt')
out = io.open(out_path, 'w', encoding='utf-8', newline='\r\n')
for fn, names in WANT:
    p = os.path.join(GAME, fn)
    if not os.path.exists(p):
        out.write('===== %s 不存在\n' % fn)
        continue
    z = zipfile.ZipFile(p)
    for n in names:
        try:
            t = z.read(n).decode('latin-1')
        except KeyError:
            out.write('===== %s 不在 %s 里\n' % (n, fn))
            continue
        out.write('===== %s (%s, %d bytes)\n%s\n' % (n, fn, len(t), t))
out.close()
print('written:', out_path)
