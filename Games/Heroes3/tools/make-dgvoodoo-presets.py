# -*- coding: utf-8 -*-
# 从仓库自带的 dgVoodoo2 原版 conf 生成两份预设：全屏 / 窗口
# 只改必要行，键名一律照抄官方模板，不自己发明
import io
import os

SRC = r'D:\AI\legacy-game-compat\tools\dgvoodoo\x86\dgVoodoo.conf'
OUT = r'D:\AI\legacy-game-compat\Games\Heroes3\src'

lines = io.open(SRC, encoding='utf-8', errors='replace').read().splitlines()


def patch(lines, section, key, value):
    """在指定小节里把 key 的值改掉；找不到就报错，绝不静默跳过"""
    want = section.strip('[]')
    cur = None
    for i, raw in enumerate(lines):
        s = raw.strip()
        if s.startswith('[') and s.endswith(']'):
            cur = s[1:-1]
            continue
        if cur != want:
            continue
        if '=' not in raw:
            continue
        name = raw.split('=')[0].strip()
        if name == key:
            indent = raw[:len(raw) - len(raw.lstrip())]
            lines[i] = '%s%s%s= %s' % (indent, name, ' ' * max(1, 35 - len(name)), value)
            return True
    raise SystemExit('找不到键 %s/%s，模板变了，别硬写' % (section, key))


edits = {
    # 两条通道都关掉水印、限 60 帧、开垂直同步（1999 年的帧逻辑跟刷新率绑死）
    'common': [('[DirectX]', 'dgVoodooWatermark', 'true'),  # 水印保留：一眼能看出走的是哪条通道
               ('[GeneralExt]', 'FPSLimit', '60'),
               ('[DirectX]', 'ForceVerticalSync', 'true'),
               ('[General]', 'CaptureMouse', 'true')],
    'full': [('[General]', 'FullScreenMode', 'true'),
             ('[General]', 'ScalingMode', 'centered'),
             ('[General]', 'KeepWindowAspectRatio', 'true'),
             ('[General]', 'CenterAppWindow', 'true')],
    'window': [('[General]', 'FullScreenMode', 'false'),
               ('[General]', 'ScalingMode', 'unspecified'),
               ('[General]', 'KeepWindowAspectRatio', 'true'),
               ('[General]', 'CenterAppWindow', 'true')],
}

for name in ('dgVoodoo-window.conf',):   # 全屏那条实测让游戏报 DDERR_INVALIDRECT，已撤下
    l = list(lines)
    for sec, key, val in edits['common']:
        patch(l, sec, key, val)
    for sec, key, val in edits['full' if 'full' in name else 'window']:
        patch(l, sec, key, val)
    head = ('; 英雄无敌3：死亡阴影 —— dgVoodoo2 预设（%s）\r\n'
            '; 由 tools/make-dgvoodoo-presets.py 从官方模板生成，只改列出的几行\r\n'
            % ('全屏' if 'full' in name else '窗口'))
    data = head + '\r\n'.join(l) + '\r\n'
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    io.open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='').write(data)
    print('已生成', name)

for f in sorted(os.listdir(OUT)):
    if f.startswith('dgVoodoo'):
        print(' ', f, os.path.getsize(os.path.join(OUT, f)), '字节')
