"""从当前注册表导出 Controls，生成干净的按键 reg。"""
import subprocess

KEY = r"HKLM\SOFTWARE\WOW6432Node\Electronic Arts\RoadRash 95"


def current_controls():
    out = subprocess.run(["reg", "query", KEY, "/v", "Controls"],
                         capture_output=True, text=True, encoding="mbcs").stdout
    line = [l for l in out.splitlines() if "Controls" in l][0]
    return line.split()[-1].strip()


def split_words(hexstr):
    b = bytes.fromhex(hexstr)
    vals = [b[i] for i in range(0, len(b), 2)]  # 每槽取第 1 字节（显示码在高字节前的 0）
    vals = [b[i + 1] for i in range(0, len(b), 2)]
    return vals


if __name__ == "__main__":
    h = current_controls()
    print("raw:", h)
    print("codes:", ", ".join(f"{v:02x}" for v in split_words(h)))
