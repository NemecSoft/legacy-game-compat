"""从修改者原版 reg 生成三套按键方案 reg（只改 Controls，其它设置原样保留）。

槽序：前进 刹车 左转 左倾 加速 拳打 脚踢 重击 下车 右转 右倾
每槽两个字节，低字节恒 0，高字节为游戏内部键码；特殊键码不是直觉值：
  ↑ 0x94  ↓ 0x96  ← 0x93  → 0x95  Home 0x84  PageUp 0x81  Insert 0x85
  Enter 0x0D  Space 0x20  Alt 0x92
以未修改原版目录的 reg / 游戏内“使用键盘默认设置”导出的值为唯一基准。
"""
import os

BS = chr(92)

MODDER = "00,94,00,92,00,93,00,84,00,78,00,61,00,73,00,7a,00,64,00,95,00,81"
WASD = "00,77,00,73,00,61,00,71,00,90,00,6a,00,75,00,6b,00,78,00,64,00,65"
SYSTEM = "00,94,00,96,00,93,00,84,00,6e,00,85,00,0d,00,20,00,64,00,95,00,81"


def fmt(hexstr):
    items = hexstr.split(',')
    out, line = [], []
    for it in items:
        line.append(it)
        if len(line) == 24:
            out.append(','.join(line) + ',')
            line = []
    if line:
        out.append(','.join(line))
    res = '"Controls"=hex:' + out[0]
    for l in out[1:]:
        res += BS + '\r\n  ' + l
    return res


def patch(src, dst, hexstr):
    lines = open(src, 'rb').read().decode('gbk').split('\r\n')
    idx = next(i for i, l in enumerate(lines) if l.startswith('"Controls"=hex:'))
    end = idx
    while lines[end].rstrip().endswith(BS):
        end += 1
    lines[idx:end + 1] = fmt(hexstr).split('\r\n')
    open(dst, 'wb').write('\r\n'.join(lines).encode('gbk'))
    print('wrote', dst)


if __name__ == '__main__':
    base = r'D:\AI\legacy-game-compat\Games\RoadRash\src'
    os.chdir(base)
    patch('road64.reg', 'road64.reg', MODDER)
    patch('road.reg', 'road.reg', MODDER)
    patch('road64.reg', 'road64-wasd.reg', WASD)
    patch('road.reg', 'road-wasd.reg', WASD)
    patch('road64.reg', 'road64-system.reg', SYSTEM)
    patch('road.reg', 'road-system.reg', SYSTEM)
    for old in ('road64-arrows.reg', 'road-arrows.reg'):
        if os.path.exists(old):
            os.remove(old)
            print('removed', old)
