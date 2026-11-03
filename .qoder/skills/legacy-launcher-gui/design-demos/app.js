const STYLES = [
  {
    id: "neumorph",
    name: "新拟态",
    en: "NEUMORPHISM",
    desc: "同色软面板、双向阴影，像一体成型的设备；安静、低刺激，适合长时间停留。",
    colors: ["#e6edf5", "#ffffff", "#c3cedb", "#5b7cfa"],
    extras: ""
  },
  {
    id: "glass",
    name: "玻璃拟态",
    en: "GLASSMORPHISM",
    desc: "紫青渐变、磨砂玻璃与悬浮层次；通透、现代，像一层会发光的界面。",
    colors: ["#2a145c", "#7c4dff", "#00e5ff", "#f7f8ff"],
    extras: ""
  },
  {
    id: "cyber",
    name: "暗黑霓虹",
    en: "CYBERPUNK HUD",
    desc: "深炭底、青色与洋红辉光、网格和角标；战斗 HUD 的紧张感最强。",
    colors: ["#05070a", "#00f0ff", "#ff2bd6", "#00ff9d"],
    extras: ""
  },
  {
    id: "swiss",
    name: "瑞士极简",
    en: "SWISS GRID",
    desc: "黑白、严格网格、一点朱红；没有阴影和圆角，靠秩序和字号建立高级感。",
    colors: ["#f2f1ed", "#ffffff", "#111111", "#e2231a"],
    extras: ""
  },
  {
    id: "leather",
    name: "拟物皮革",
    en: "LEATHER & BRASS",
    desc: "皮革压纹、缝线、黄铜按钮；像一台老派高级音响，厚重、有收藏感。",
    colors: ["#2a160f", "#4a2a1b", "#b98a55", "#e3c07a"],
    extras: ""
  },
  {
    id: "clay",
    name: "3D 软塑",
    en: "CLAYMORPHISM",
    desc: "奶油软塑、大圆角、柔和的体积阴影；友好、轻松，适合降低动作游戏的压迫感。",
    colors: ["#f7efe9", "#ffd9c8", "#cdebd8", "#ded7ff"],
    extras: ""
  },
  {
    id: "memphis",
    name: "孟菲斯波普",
    en: "MEMPHIS",
    desc: "高饱和几何、粗黑描边、错位阴影；80 年代玩心，最活泼、最不怕吵。",
    colors: ["#fff7df", "#ffd23f", "#ff5d8f", "#19c3b1"],
    extras: "<svg class=\"o-squiggle\" viewBox=\"0 0 120 40\" aria-hidden=\"true\"><path d=\"M4 28 C 18 4, 30 4, 42 20 S 66 36, 78 18 S 104 6, 116 24\" fill=\"none\" stroke=\"#111\" stroke-width=\"6\" stroke-linecap=\"round\"/></svg>"
  },
  {
    id: "ink",
    name: "东方水墨",
    en: "INK & PAPER",
    desc: "宣纸、墨线、朱印与大量留白；克制、安静，最贴近战国题材的文化气质。",
    colors: ["#f3eee3", "#f7f2e8", "#1f1c18", "#b23a2f"],
    extras: "<div class=\"o-vertical\">一剑入魂</div><div class=\"o-seal\">鬼</div><div class=\"o-brush\"></div>"
  },
  {
    id: "holo",
    name: "科幻全息",
    en: "HOLOGRAPHIC HUD",
    desc: "深空蓝、透明全息板、雷达与扫描线；未来感强，像在作战界面里启动游戏。",
    colors: ["#020814", "#0a2a4a", "#2f7bff", "#6fe3ff"],
    extras: "<div class=\"o-radar\"></div>"
  },
  {
    id: "pastel",
    name: "治愈渐变",
    en: "SOFT GRADIENT",
    desc: "珊瑚、蜜桃、薰衣草渐变与柔和圆角；温柔、松弛，和硬核剑斗形成反差。",
    colors: ["#ffd7c9", "#ffc7d9", "#d9c8ff", "#ff8a7a"],
    extras: ""
  }
];

const KEY_ROWS = [
  ["移动", ["W", "A", "S", "D"]],
  ["单手攻击", ["鼠标左键"]],
  ["双手攻击", ["R"]],
  ["防御 / 化劲", ["鼠标右键"]],
  ["回避", ["空格"]],
  ["锁定目标", ["鼠标中键"]],
  ["调查", ["F"]],
  ["使用道具", ["Q"]],
  ["菜单", ["Esc"]]
];

function mockupHTML(style) {
  const keys = KEY_ROWS.map(([label, caps]) => `
    <div class="keyrow">
      <span>${label}</span>
      <span class="keys">${caps.map((cap) => `<kbd>${cap}</kbd>`).join("")}</span>
    </div>`).join("");

  return `
  <div class="mockup m-${style.id}">
    <div class="app">
      <div class="winbar">
        <div class="win-dots"><i></i><i></i><i></i></div>
        <div class="win-title">游戏启动器</div>
        <div class="win-meta">启动器</div>
      </div>
      <div class="app-head">
        <div class="app-title">游戏启动器</div>
        <div class="app-sub">YunGame Launcher · GUI Sample</div>
      </div>
      <div class="app-body">
        <aside class="pane pane-left">
          <div class="kicker">关于这款游戏</div>
          <p class="intro">这是一个游戏启动器的三栏界面样张：左边介绍游戏，中间选择启动方式，右边是操作说明。</p>
          <p class="intro">同一套内容只改变材质、光线、字体与秩序，用来比较不同 GUI 风格。</p>
          <div class="left-foot">
            <div class="kicker">玩不了或报错</div>
            <p>点「打开运行日志」<br>把内容发给管理员</p>
          </div>
        </aside>

        <section class="pane pane-center">
          <div class="kicker">启动说明</div>
          <p class="intro">游戏会自动检测画面。</p>
          <div class="field-label">启动选项</div>
          <div class="select">游戏自动检测（推荐）<span class="chev">⌄</span></div>
          <span class="btn btn-primary">启动游戏</span>
          <div class="btn-row">
            <span class="btn">修复分辨率</span>
            <span class="btn">打开运行日志</span>
            <span class="btn">退出</span>
          </div>
        </section>

        <aside class="pane pane-right">
          <div class="kicker">操作说明</div>
          ${keys}
          <p class="small core-tip">核心：敌人攻击瞬间按右键化劲；空格回避；不要贪刀。</p>
        </aside>
      </div>
      <div class="app-foot">
        <span class="status"><i></i>已就绪</span>
        <span class="brand">YunGame</span>
        <span class="hint">游戏退出后本窗口自动关闭</span>
      </div>
    </div>
    ${style.extras}
  </div>`;
}

function render() {
  const gallery = document.getElementById("gallery");
  const index = document.getElementById("style-index");
  const only = new URLSearchParams(location.search).get("style");
  const entries = STYLES
    .map((style, i) => ({ style, no: String(i + 1).padStart(2, "0") }))
    .filter((entry) => !only || entry.style.id === only);

  if (only) document.body.classList.add("single-style");

  index.innerHTML = entries.map(({ style, no }) => {
    return `<a href="#s-${style.id}"><b>${no}</b>${style.name}</a>`;
  }).join("");

  gallery.innerHTML = entries.map(({ style, no }) => {
    const swatches = style.colors.map((color) => `<i style="--c:${color}"></i>`).join("");
    return `
      <section class="style-block" id="s-${style.id}">
        <div class="style-meta">
          <span class="style-no">${no}</span>
          <div class="style-name">
            <h2>${style.name}</h2>
            <span>${style.en}</span>
          </div>
          <p class="style-desc">${style.desc}</p>
          <div class="swatches" aria-hidden="true">${swatches}</div>
        </div>
        <div class="mockup-frame">${mockupHTML(style)}</div>
      </section>`;
  }).join("");
}

function fitMockups() {
  const mobile = window.innerWidth < 900;
  document.querySelectorAll(".mockup-frame").forEach((frame) => {
    const mockup = frame.querySelector(".mockup");
    if (!mockup) return;
    if (mobile) {
      frame.classList.add("is-scroll");
      frame.style.height = "720px";
      mockup.style.transform = "none";
    } else {
      frame.classList.remove("is-scroll");
      const scale = Math.min(1, frame.clientWidth / 1080);
      mockup.style.transform = `scale(${scale})`;
      frame.style.height = `${Math.round(720 * scale)}px`;
    }
  });
}

function watchIndex() {
  const links = Array.from(document.querySelectorAll(".style-index a"));
  if (!("IntersectionObserver" in window)) return;
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (!entry.isIntersecting) return;
      links.forEach((link) => {
        link.classList.toggle("is-active", link.getAttribute("href") === `#${entry.target.id}`);
      });
    });
  }, { rootMargin: "-18% 0px -70% 0px" });
  document.querySelectorAll(".style-block").forEach((section) => observer.observe(section));
}

render();
requestAnimationFrame(() => {
  fitMockups();
  watchIndex();
  if (!document.body.classList.contains("single-style") && location.hash) {
    const target = document.querySelector(location.hash);
    if (target) target.scrollIntoView({ block: "start" });
  }
});
window.addEventListener("resize", () => requestAnimationFrame(fitMockups));
