// 解析 field-watch.bin（帧=tick8+obj4+0x200字节），找 respawn 无敌计时器候选：突增后单调递减归零的 offset。
const fs = require('fs');
const b = fs.readFileSync(process.argv[2] || 'D:/Temp/field-watch.bin');
const FRAME = 8 + 4 + 0x400;
const n = Math.floor(b.length / FRAME);
if (n < 10) { console.log('帧数不足', n); process.exit(1); }
const ticks = [], objs = [];
const series = [];
for (let off = 0; off < 0x400; off += 4) series.push(new Int32Array(n));
for (let i = 0; i < n; i++) {
  const base = i * FRAME;
  ticks.push(Number(b.readBigInt64LE(base)));
  objs.push(b.readUInt32LE(base + 8));
  for (let off = 0; off < 0x400; off += 4) series[off / 4][i] = b.readInt32LE(base + 12 + off);
}
console.log('frames=' + n, 'obj=' + objs[0].toString(16), 'span_ms=' + (ticks[n - 1] - ticks[0]));
const cands = [];
for (let k = 0; k < series.length; k++) {
  const s = series[k];
  let jumps = 0, decs = 0, incs = 0, max = -2147483648, min = 2147483647, zeroRun = 0, maxZeroRun = 0;
  for (let i = 0; i < n; i++) {
    if (s[i] > max) max = s[i];
    if (s[i] < min) min = s[i];
    if (s[i] === 0) { zeroRun++; if (zeroRun > maxZeroRun) maxZeroRun = zeroRun; } else zeroRun = 0;
    if (i > 0) {
      const d = s[i] - s[i - 1];
      if (d > 10) jumps++;
      else if (d < 0 && d >= -10) decs++;
      else if (d > 0 && d <= 10) incs++;
    }
  }
  // 倒计时候选：有突增、有逐步递减、最小值为0、有连续归零段
  if (jumps >= 1 && decs >= 5 && min === 0 && maxZeroRun >= 3 && max > 20 && max < 100000)
    cands.push({ off: k * 4, jumps, decs, incs, max, maxZeroRun });
}
cands.sort((a, b) => (b.decs + b.jumps * 5) - (a.decs + a.jumps * 5));
console.log('candidates=' + cands.length);
for (const c of cands.slice(0, 15)) {
  const s = series[c.off / 4];
  let sample = [];
  for (let i = 0; i < n; i += Math.max(1, Math.floor(n / 40))) sample.push(s[i]);
  console.log('off=0x' + c.off.toString(16), 'jumps=' + c.jumps, 'decs=' + c.decs, 'max=' + c.max, 'zeroRun=' + c.maxZeroRun);
  console.log('  series:', sample.join(','));
}