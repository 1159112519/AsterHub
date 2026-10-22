import { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUpRight, Check, ChevronRight, Cpu, Download, Files, Gauge, Github, HardDrive, Layers3, Menu, Monitor, Server, ShieldCheck, Terminal, X } from 'lucide-react';
const installer = './releases/AQServer-Setup-0.1.0.exe';
const repository = 'https://github.com/1159112519/aq-server';

function ProductReveal() {
  const art = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = art.current;
    if (!node) return;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    const position = { x: node.clientWidth * .64, y: node.clientHeight * .5 };
    const target = { ...position };
    let frame = 0;
    let previous = 0;
    const paint = () => {
      node.style.setProperty('--spot-x', `${position.x}px`);
      node.style.setProperty('--spot-y', `${position.y}px`);
    };
    const animate = (time: number) => {
      const dt = previous ? Math.min(time - previous, 40) : 16;
      previous = time;
      const easing = 1 - Math.exp(-dt / 75);
      position.x += (target.x - position.x) * easing;
      position.y += (target.y - position.y) * easing;
      paint();
      if (Math.abs(target.x - position.x) + Math.abs(target.y - position.y) > .2) frame = requestAnimationFrame(animate);
      else { frame = 0; previous = 0; }
    };
    const move = (event: PointerEvent) => {
      if (reduced.matches) return;
      const bounds = node.getBoundingClientRect();
      target.x = (event.clientX - bounds.left) * node.clientWidth / bounds.width;
      target.y = (event.clientY - bounds.top) * node.clientHeight / bounds.height;
      if (!frame) frame = requestAnimationFrame(animate);
    };
    const reset = () => {
      target.x = node.clientWidth * .64;
      target.y = node.clientHeight * .5;
      if (reduced.matches) { position.x = target.x; position.y = target.y; paint(); } else if (!frame) frame = requestAnimationFrame(animate);
    };
    paint();
    const hero = node.closest('.hero')!;
    hero.addEventListener('pointermove', move as EventListener, { passive: true });
    hero.addEventListener('pointerleave', reset);
    const resize = new ResizeObserver(reset);
    resize.observe(node);
    reduced.addEventListener('change', reset);
    return () => { cancelAnimationFrame(frame); resize.disconnect(); reduced.removeEventListener('change', reset); hero.removeEventListener('pointermove', move as EventListener); hero.removeEventListener('pointerleave', reset); };
  }, []);
  return <div ref={art} className="product-art" aria-label="AQ Server 多 GPU 监控界面预览">
    <div className="image-stage">
      <img className="monitor-image base-image" src="./media/monitor-base.webp" alt="AQ Server 八卡资源监控界面，展示 GPU 利用率、显存、温度和功耗" width="2160" height="1500" fetchPriority="high" />
      <div className="focus-image" aria-hidden="true"><img className="monitor-image" src="./media/monitor-focus.webp" alt="" width="2160" height="1500" /></div>
    </div>
    <div className="art-caption"><span><span className="status-light" />8 GPUs. One clear view.</span><span>移动光标，聚焦细节 <ArrowUpRight size={13} /></span></div>
  </div>;
}

const features = [
  { icon: Gauge, title: '每一张卡，都看得清。', description: 'GPU 利用率、显存、温度与进程，多卡同时可见。没有显卡的设备也有完整的 CPU 和内存视图。', detail: '本机 · SSH 服务器 · 多核 CPU' },
  { icon: Terminal, title: '观察，然后开始工作。', description: '内嵌终端、双栏文件和传输队列，连接设备后继续手头的工作。分栏宽度由你决定。', detail: '终端会话 · 文件传输 · 可调整布局' },
  { icon: HardDrive, title: '过去的状态，有迹可循。', description: '已连接设备持续记录，切换页面不会停止采集。资源数据和 GPU 进程随时间归档，保存在本机。', detail: '时间回看 · 进程快照 · 本地存档' },
];

export default function App() {
  const [mobileOpen, setMobileOpen] = useState(false);
  useEffect(() => {
    if (!mobileOpen) return;
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setMobileOpen(false); };
    document.addEventListener('keydown', close);
    return () => document.removeEventListener('keydown', close);
  }, [mobileOpen]);
  return <>
    <header className="site-header">
      <a className="brand" href="#home" aria-label="AQ Server 首页"><img src="./favicon.ico" alt="" width="29" height="29" /><span>AQ<span className="brand-space">Server</span></span></a>
      <nav className="desktop-nav" aria-label="主导航"><a href="#experience">产品体验</a><a href="#updates">版本更新</a><a href={repository} target="_blank" rel="noreferrer" title="项目仓库，需授权访问">GitHub <ArrowUpRight size={13} /></a></nav>
      <a className="header-download" href={installer} download><Download size={14} />下载 <span>v0.1.0</span></a>
      <button className="mobile-menu-button" type="button" aria-label={mobileOpen ? '关闭导航' : '打开导航'} aria-expanded={mobileOpen} aria-controls="mobile-navigation" onClick={() => setMobileOpen(!mobileOpen)}>{mobileOpen ? <X size={21} /> : <Menu size={21} />}</button>
      {mobileOpen && <nav id="mobile-navigation" className="mobile-nav" aria-label="移动导航"><a href="#experience" onClick={() => setMobileOpen(false)}>产品体验<ChevronRight size={16} /></a><a href="#updates" onClick={() => setMobileOpen(false)}>版本更新<ChevronRight size={16} /></a><a href={repository} target="_blank" rel="noreferrer" onClick={() => setMobileOpen(false)}>项目仓库<ArrowUpRight size={16} /></a></nav>}
    </header>
    <main>
      <section className="hero" id="home">
        <div className="hero-topline"><span className="hero-category"><span className="status-light" />A QUIETER WAY TO MONITOR</span><span className="platform-label"><Monitor size={13} /> BUILT FOR WINDOWS</span></div>
        <div className="hero-content">
          <div className="release-note"><span>0.1.0</span> 首个公开预览版本 <ArrowUpRight size={13} /></div>
          <h1>Your server,<br /><em>in focus.</em></h1>
          <p className="hero-description">让复杂的运行状态，变得一目了然。<br />从本机到远程服务器，监控、终端与文件，<br className="desktop-break" />留在同一个安静的工作空间。</p>
          <div className="hero-actions"><a className="download-button" href={installer} download><Download size={18} /><span>下载 Windows 版</span><ArrowUpRight size={18} /></a><a className="explore-link" href="#experience">探索产品<ArrowDown size={15} /></a></div>
          <p className="compatibility">Windows 10 / 11 · x64 <span>免费使用</span></p>
        </div>
        <ProductReveal />
        <div className="hero-bottom"><span><Server size={14} /> LOCAL & REMOTE<span className="bottom-divider" />CPU / GPU / MEMORY</span><a href="#experience">SCROLL TO EXPLORE<ArrowDown size={13} /></a></div>
      </section>
      <section className="experience section-shell" id="experience">
        <div className="section-heading"><span className="section-kicker">01 / THE EXPERIENCE</span><h2>信息完整，<br /><span>工作空间保持轻盈。</span></h2><p>需要时展开，忙碌时安静常驻。<br />让监控融入工作，而不是占满你的注意力。</p></div>
        <div className="feature-grid">{features.map(({ icon: Icon, title, description, detail }) => <article className="feature" key={title}><Icon size={23} strokeWidth={1.5} /><h3>{title}</h3><p>{description}</p><div>{detail}</div></article>)}</div>
        <div className="principle-strip"><span><Layers3 size={16} />多主题，保持一致的界面语言</span><span><Files size={16} />上传下载采用复制，保留原文件</span><span><Cpu size={16} />根据设备能力显示资源</span></div>
      </section>
      <section className="updates section-shell" id="updates"><div className="section-heading"><span className="section-kicker">02 / RELEASE NOTES</span><h2>从 0.1.0 开始，<br /><span>把细节做好。</span></h2><p>这是早期版本。持续完善体验，<br />逐步走向稳定发布。</p></div><article className="release-card"><div className="release-card-heading"><h3>v0.1.0</h3><span>公开预览</span></div><ul><li><Check size={15} />本机与多服务器资源监控、终端和文件工作台</li><li><Check size={15} />资源历史与 GPU 进程快照保存在本机</li><li><Check size={15} />与主题一致的托盘菜单和迷你监控</li><li><Check size={15} />偏好设置支持应用，分栏与列宽可调整</li><li><Check size={15} />完整目录安装包、检查更新与安装前校验</li></ul><div className="release-card-footer"><a href={installer} download>下载安装包<Download size={15} /></a><a href="./releases/SHA256SUMS.txt">SHA-256<ArrowUpRight size={13} /></a></div></article></section>
      <section className="install-note section-shell"><ShieldCheck size={23} strokeWidth={1.5} /><div><h3>升级，保留你的配置与历史。</h3><p>客户端校验发布签名和安装包；下载后，由你确认安装。更新会关闭终端会话，请先保存工作。</p><p className="small-note">早期版本尚未使用 Windows 发布者代码签名证书，安装时可能显示未知发布者。</p></div></section>
    </main>
    <footer className="site-footer section-shell"><a className="footer-brand" href="#home">AQ Server<span>Your server, in focus.</span></a><div><span>v0.1.0 · Windows x64</span><a href={repository} target="_blank" rel="noreferrer"><Github size={15} />项目仓库（需授权）<ArrowUpRight size={12} /></a></div></footer>
  </>;
}
