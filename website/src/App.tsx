import { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUpRight, Check, ChevronRight, Cpu, Download, Files, Gauge, Github, HardDrive, Layers3, Menu, Monitor, Server, ShieldCheck, Terminal, X } from 'lucide-react';
import releaseVersions from './release-versions.json';
const stableWindowsVersion = releaseVersions.windows;
const windowsVersion = releaseVersions.windowsPreview;
const ubuntuVersion = releaseVersions.ubuntu;
const windowsDate = releaseVersions.windowsPreviewDate.replace(/-/g, '.');
const stableWindowsDate = releaseVersions.windowsDate.replace(/-/g, '.');
const ubuntuDate = releaseVersions.ubuntuDate.replace(/-/g, '.');
const installer = `./releases/AsterHub-Setup-${windowsVersion}.exe`;
const ubuntuInstaller = `./releases/AQServer-${ubuntuVersion}-ubuntu-all.deb`;
const repository = 'https://github.com/1159112519/aq-server/releases';

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
  return <div ref={art} className="product-art" aria-label="AsterHub 多 GPU 监控界面预览">
    <div className="image-stage">
      <img className="monitor-image base-image" src="./media/monitor-base.webp" alt="AsterHub 八卡资源监控界面，展示 GPU 利用率、显存、温度和功耗" width="2160" height="1500" {...{fetchpriority: 'high'}} />
      <div className="focus-image" aria-hidden="true"><img className="monitor-image" src="./media/monitor-focus.webp" alt="" width="2160" height="1500" /></div>
    </div>
    <div className="art-caption"><span><span className="status-light" />8 GPUs. One clear view.</span><span>模拟界面 · 移动光标聚焦细节 <ArrowUpRight size={13} /></span></div>
  </div>;
}

const features = [
  { icon: Gauge, title: '每一张卡，都看得清。', description: 'GPU 利用率、显存、温度与进程，多卡同时可见。没有显卡的设备也有完整的 CPU、内存、网络与存储视图。', detail: '本机 · SSH 服务器 · 多核 CPU' },
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
      <a className="brand" href="#home" aria-label="AsterHub 首页"><img src="./favicon.ico" alt="" width="29" height="29" /><span>AsterHub</span></a>
      <nav className="desktop-nav" aria-label="主导航"><a href="#experience">产品体验</a><a href="#updates">版本更新</a><a href={repository} target="_blank" rel="noreferrer" title="公开版本更新与发行说明">GitHub <ArrowUpRight size={13} /></a></nav>
      <a className="header-download" href="#download"><Download size={14} />下载 <span>v{windowsVersion}</span></a>
      <button className="mobile-menu-button" type="button" aria-label={mobileOpen ? '关闭导航' : '打开导航'} aria-expanded={mobileOpen} aria-controls="mobile-navigation" onClick={() => setMobileOpen(!mobileOpen)}>{mobileOpen ? <X size={21} /> : <Menu size={21} />}</button>
      {mobileOpen && <nav id="mobile-navigation" className="mobile-nav" aria-label="移动导航"><a href="#experience" onClick={() => setMobileOpen(false)}>产品体验<ChevronRight size={16} /></a><a href="#updates" onClick={() => setMobileOpen(false)}>版本更新<ChevronRight size={16} /></a><a href={repository} target="_blank" rel="noreferrer" onClick={() => setMobileOpen(false)}>更新日志<ArrowUpRight size={16} /></a></nav>}
    </header>
    <main>
      <section className="hero" id="home">
        <div className="hero-topline"><span className="hero-category"><span className="status-light" />A QUIETER WAY TO MONITOR</span><span className="platform-label"><Monitor size={13} /> WINDOWS & UBUNTU</span></div>
        <div className="hero-content">
          <div className="release-note"><span>{windowsVersion}</span> 全新工作台 · 网络与存储监控 <ArrowUpRight size={13} /></div>
          <h1>Your server,<br /><em>in focus.</em></h1>
          <p className="hero-description">让复杂的运行状态，变得一目了然。<br />从本机到远程服务器，监控、终端与文件，<br className="desktop-break" />留在同一个安静的工作空间。</p>
          <div className="hero-actions">
            <div className="hero-platform"><a className="download-button" href={installer} download><Download size={18} /><span>下载 Windows 预览版</span><ArrowUpRight size={18} /></a><p className="compatibility">Windows 10 / 11 · x64<br />v{windowsVersion} · {windowsDate}</p></div>
            <div className="hero-platform"><a className="download-button ubuntu-button" href={ubuntuInstaller} download><Terminal size={18} /><span>下载 Ubuntu 版</span><ArrowUpRight size={18} /></a><p className="compatibility">Ubuntu 22.04 · .deb<br />v{ubuntuVersion} · {ubuntuDate}</p></div>
          </div>
        </div>
        <ProductReveal />
        <div className="hero-bottom"><span><Server size={14} /> LOCAL & REMOTE<span className="bottom-divider" />CPU / GPU / MEMORY / NETWORK / STORAGE</span><a href="#experience">SCROLL TO EXPLORE<ArrowDown size={13} /></a></div>
      </section>
      <section className="downloads section-shell" id="download"><div className="downloads-heading"><span className="section-kicker">GET ASTERHUB</span><h2>选择你的工作环境。</h2><p>Windows v{windowsVersion} 预览 · Ubuntu v{ubuntuVersion}，按平台独立发布。</p></div><div className="download-grid"><article className="platform-card"><Monitor size={26} strokeWidth={1.4} /><h3>Windows <span className="platform-version">v{windowsVersion}</span></h3><div className="platform-date">{windowsDate} 更新 · Windows 新工作台预览</div><p>Windows 10 / 11 · x64</p><a className="platform-download" href={installer} download>下载 EXE 安装包<Download size={16} /></a><div className="platform-detail">模块化目录安装，终端与文件传输已接回。预览版与原稳定版的数据独立保存，不会自动迁移已保存的凭据。需要 WebView2 Runtime。</div><a className="stable-download" href={`./releases/AQServer-Setup-${stableWindowsVersion}.exe`} download>下载稳定版 v{stableWindowsVersion} · {stableWindowsDate}</a></article><article className="platform-card"><Terminal size={26} strokeWidth={1.4} /><h3>Ubuntu <span className="platform-version">v{ubuntuVersion}</span></h3><div className="platform-date">{ubuntuDate} 更新 · 独立预览版</div><p>Ubuntu 22.04 amd64 · Debian 安装包</p><a className="platform-download" href={ubuntuInstaller} download>下载 DEB 安装包<Download size={16} /></a><code>sudo apt install ./AQServer-{ubuntuVersion}-ubuntu-all.deb</code><div className="platform-detail">安装后从应用菜单打开，或运行 <strong>aq-server --desktop</strong>。首次安装由 apt 获取 GTK/WebKit 和 Python 运行库。无桌面服务器可运行本地服务；工作台需要本机访问密钥。</div></article></div><div className="download-footnote"><span>两个平台独立迭代，版本与功能进度可能不同。Ubuntu v{ubuntuVersion} 为保留的预览版，后续不随 Windows 同步更新；Ubuntu 24.04、原生托盘和自动升级尚未支持或验证。</span><a href="./releases/SHA256SUMS.txt">稳定版与 Ubuntu SHA-256<ArrowUpRight size={13} /></a></div></section>
      <section className="experience section-shell" id="experience">
        <div className="section-heading"><span className="section-kicker">01 / THE EXPERIENCE</span><h2>信息完整，<br /><span>工作空间保持轻盈。</span></h2><p>需要时展开，忙碌时安静常驻。<br />让监控融入工作，而不是占满你的注意力。</p></div>
        <div className="feature-grid">{features.map(({ icon: Icon, title, description, detail }) => <article className="feature" key={title}><Icon size={23} strokeWidth={1.5} /><h3>{title}</h3><p>{description}</p><div>{detail}</div></article>)}</div>
        <div className="principle-strip"><span><Layers3 size={16} />多主题，保持一致的界面语言</span><span><Files size={16} />上传下载采用复制，保留原文件</span><span><Cpu size={16} />根据设备能力显示资源</span></div>
      </section>
      <section className="updates section-shell" id="updates"><div className="section-heading"><span className="section-kicker">02 / RELEASE NOTES</span><h2>持续打磨，<br /><span>把细节做好。</span></h2><p>这是早期版本。持续完善体验，<br />逐步走向稳定发布。</p></div><div className="release-stack"><article className="release-card"><div className="release-card-heading"><h3>Windows v{windowsVersion}</h3><span>{windowsDate} · 公开预览</span></div><ul><li><Check size={15} />修复历史记录黑屏、启动白屏和设置切页跳动</li><li><Check size={15} />GPU 进程可展开完整命令，结束操作需确认并核验身份</li><li><Check size={15} />网络接口、吞吐趋势与链路详情；存储 I/O、队列与卷容量</li><li><Check size={15} />多标签终端、文件复制传输和可调整分栏</li><li><Check size={15} />恢复检查更新与反馈建议，保留细腻的曲线和数字动画</li><li><Check size={15} />优化连接测试居中、底栏留白与负载配色</li></ul><div className="release-card-footer"><a href={installer} download>下载安装包<Download size={15} /></a><a href="./releases/PREVIEW-SHA256SUMS.txt">SHA-256<ArrowUpRight size={13} /></a></div></article><article className="release-card ubuntu-release"><div className="release-card-heading"><h3>Ubuntu v{ubuntuVersion}</h3><span>{ubuntuDate} · 独立预览</span></div><p>保留本次已验证的 GTK/WebKit 工作台，包含网络上下行、分区容量和五项常驻资源。Ubuntu 后续按需维护，不随 Windows 同步发版。</p><div className="release-card-footer"><a href={ubuntuInstaller} download>下载 Ubuntu v{ubuntuVersion}<Download size={15} /></a><a href="./releases/SHA256SUMS.txt">SHA-256<ArrowUpRight size={13} /></a></div></article></div></section>
      <section className="install-note section-shell"><ShieldCheck size={23} strokeWidth={1.5} /><div><h3>同一通道升级，保留配置与历史。</h3><p>Windows 检查新版本后点击“安装并重新启动”，确认后自动下载、校验、退出、安装并重新打开。同一通道内配置和历史保留，终端会话会结束。Windows 预览版与原稳定版使用独立数据，不自动迁移已保存的凭据。Ubuntu 通过 apt 手动安装或升级。</p><p className="small-note">早期版本尚未使用 Windows 发布者代码签名证书，安装时可能显示未知发布者。</p></div></section>
    </main>
    <footer className="site-footer section-shell"><a className="footer-brand" href="#home">AsterHub<span>Your server, in focus.</span></a><div><span>Windows v{windowsVersion} · Ubuntu v{ubuntuVersion}</span><a href={repository} target="_blank" rel="noreferrer"><Github size={15} />版本更新与发行说明<ArrowUpRight size={12} /></a></div></footer>
  </>;
}
