import { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUpRight, Check, Download, Github, Menu, Monitor, Server, ShieldCheck, Terminal, X } from 'lucide-react';
import {StoryExperience, Questions, ProductPrinciples, BrandStory, usePageMotion} from './StoryExperience';
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
      <img className="monitor-image base-image" src="./media/asterhub-preview6-final-base.webp" alt="AsterHub 八卡资源监控界面，展示 GPU 利用率、显存、温度和功耗" width="1600" height="1100" {...{fetchpriority: 'high'}} />
      <div className="focus-image" aria-hidden="true"><img className="monitor-image" src="./media/asterhub-preview6-final-eight-gpu.webp" alt="" width="1600" height="1100" /></div>
    </div>
    <div className="art-caption"><span><span className="status-light" />多卡，同时看清。</span><span>当前预览界面 · 示例数据 <ArrowUpRight size={13} /></span></div>
  </div>;
}

export default function App() {
  const [mobileOpen,setMobileOpen]=useState(false);
  usePageMotion();
  useEffect(()=>{if(!mobileOpen)return;const close=(e:KeyboardEvent)=>{if(e.key==='Escape')setMobileOpen(false);};document.addEventListener('keydown',close);return()=>document.removeEventListener('keydown',close);},[mobileOpen]);
  return <>
    <a className="skip-link" href="#experience">跳至产品介绍</a>
    <header className="site-header">
      <a className="brand" href="#home" aria-label="AsterHub 首页"><img src="./asterhub-brand.svg" alt="" width="29" height="29"/><span>AsterHub</span></a>
      <nav className="desktop-nav" aria-label="主导航"><a href="#experience">产品体验</a><a href="#download">下载</a><a href="#questions">常见问题</a><a href="#updates">版本更新</a></nav>
      <a className="header-download" href={installer} download><Download size={15}/><span>下载 Windows</span></a>
      <button className="mobile-menu-button" aria-label={mobileOpen?'关闭导航':'打开导航'} aria-expanded={mobileOpen} onClick={()=>setMobileOpen(v=>!v)}>{mobileOpen?<X size={23}/>:<Menu size={23}/>}</button>
      {mobileOpen&&<nav className="mobile-nav" aria-label="移动导航">{[['#experience','产品体验'],['#download','下载'],['#questions','常见问题'],['#updates','版本更新']].map(([href,label])=><a href={href} key={href} onClick={()=>setMobileOpen(false)}>{label}<ArrowUpRight size={16}/></a>)}</nav>}
    </header>
    <main>
      <section className="hero" id="home">
        <div className="hero-topline"><span className="hero-category"><span className="status-light"/>YOUR DEVICES. ONE WORKSPACE.</span><span className="platform-label">WINDOWS PREVIEW · UBUNTU</span></div>
        <div className="hero-content">
          <a className="release-note" href="#updates"><span>{windowsVersion}</span> 全新 Windows 工作台<ArrowUpRight size={13}/></a>
          <h1 className="hero-title">连接设备，<br/><span>看见运行。</span></h1>
          <p className="hero-english">Your server, <em>in focus.</em></p>
          <p className="hero-description">从本机到远程服务器，<br/>监控、终端与文件，留在同一个工作空间。</p>
          <div className="hero-actions"><div className="hero-platform"><a className="download-button" href={installer} download><Download size={18}/><span>下载 Windows 预览版</span><ArrowUpRight size={18}/></a><p className="compatibility">Windows 10 / 11 · x64<br/>v{windowsVersion} · {windowsDate}</p></div><a className="explore-link" href="#experience">探索工作台<ArrowDown size={16}/></a></div>
        </div>
        <ProductReveal/>
        <div className="hero-bottom"><span><Server size={14}/>本机与远程<span className="bottom-divider"/>CPU / GPU / MEMORY / NETWORK / STORAGE</span><a href="#experience">往下，看见更多<ArrowDown size={14}/></a></div>
      </section>
      <section className="product-intro section-shell" data-reveal><p className="section-kicker">A LITTLE MORE CLARITY.</p><h2>一台电脑，或一组服务器。<br/>让状态清楚，<span>让工作连贯。</span></h2><p>先看资源，再找线索，接着处理任务。<br/>需要的信息和工具，沿着你的工作展开。</p></section>
      <StoryExperience/>
      <ProductPrinciples/>
      <section className="downloads section-shell" id="download"><div className="downloads-heading"><span className="section-kicker">GET ASTERHUB</span><h2>选择你的工作环境。</h2><p>Windows v{windowsVersion} 预览 · Ubuntu v{ubuntuVersion}，按平台独立发布。</p></div><div className="download-grid"><article className="platform-card"><Monitor size={26} strokeWidth={1.4} /><h3>Windows <span className="platform-version">v{windowsVersion}</span></h3><div className="platform-date">{windowsDate} 更新 · Windows 新工作台预览</div><p>Windows 10 / 11 · x64</p><a className="platform-download" href={installer} download>下载 EXE 安装包<Download size={16} /></a><div className="platform-detail">支持资源监测、终端与文件传输。预览版与原稳定版的数据独立保存，不会自动迁移已保存的凭据。需要 WebView2 Runtime。</div><a className="stable-download" href={`./releases/AQServer-Setup-${stableWindowsVersion}.exe`} download>下载稳定版 v{stableWindowsVersion} · {stableWindowsDate}</a></article><article className="platform-card"><Terminal size={26} strokeWidth={1.4} /><h3>Ubuntu <span className="platform-version">v{ubuntuVersion}</span></h3><div className="platform-date">{ubuntuDate} 更新 · 独立预览版</div><p>Ubuntu 22.04 amd64 · Debian 安装包</p><a className="platform-download" href={ubuntuInstaller} download>下载 DEB 安装包<Download size={16} /></a><code>sudo apt install ./AQServer-{ubuntuVersion}-ubuntu-all.deb</code><div className="platform-detail">安装后从应用菜单打开，或运行 <strong>aq-server --desktop</strong>。首次安装会自动获取所需依赖。建议在 Ubuntu 22.04 桌面环境使用。</div></article></div><div className="download-footnote"><span>Windows 与 Ubuntu 的功能有所不同。Ubuntu v{ubuntuVersion} 支持 Ubuntu 22.04 amd64；如需托盘或应用内自动更新，请选择 Windows 版。Ubuntu 24.04 暂不列入支持范围。</span><a href="./releases/SHA256SUMS.txt">稳定版与 Ubuntu SHA-256<ArrowUpRight size={13} /></a></div></section>
      <Questions/>
      <section className="updates section-shell" id="updates"><div className="section-heading"><span className="section-kicker">02 / RELEASE NOTES</span><h2>持续打磨，<br /><span>把细节做好。</span></h2><p>这是早期版本。持续完善体验，<br />逐步走向稳定发布。</p></div><div className="release-stack"><article className="release-card"><div className="release-card-heading"><h3>Windows v{windowsVersion}</h3><span>{windowsDate} · 公开预览</span></div><ul><li><Check size={15} />连接中断时保留设备与读数，恢复后继续更新</li><li><Check size={15} />打开程序即可查看资源卡片，未获取的读数清晰标示</li><li><Check size={15} />实时曲线向左推进，缺失时段不再连成曲线</li><li><Check size={15} />曲线显示纵轴单位，支持自动范围或固定百分比范围</li><li><Check size={15} />关于与更新集中在同一页面</li><li><Check size={15} />资源速览更紧凑，详情浮窗更清晰</li><li><Check size={15} />改善远程采样连接，CPU 核心活动显示更平滑</li></ul><div className="release-card-footer"><a href={installer} download>下载安装包<Download size={15} /></a><a href="./releases/PREVIEW-SHA256SUMS.txt">SHA-256<ArrowUpRight size={13} /></a></div></article><article className="release-card ubuntu-release"><div className="release-card-heading"><h3>Ubuntu v{ubuntuVersion}</h3><span>{ubuntuDate} · 独立预览</span></div><p>查看 CPU、GPU、内存、网络上下行与存储容量，支持设备连接和历史记录。适用于 Ubuntu 22.04 amd64；通过 DEB 安装包安装或升级。</p><div className="release-card-footer"><a href={ubuntuInstaller} download>下载 Ubuntu v{ubuntuVersion}<Download size={15} /></a><a href="./releases/SHA256SUMS.txt">SHA-256<ArrowUpRight size={13} /></a></div></article></div></section>
      <section className="install-note section-shell"><ShieldCheck size={23} strokeWidth={1.5} /><div><h3>同一通道升级，保留配置与历史。</h3><p>Windows 检查到新版本后，先下载更新，再点击“安装并重新启动”。安装完成后会自动打开 AsterHub。同一通道内配置和历史保留，终端会话会结束。Windows 预览版与原稳定版使用独立数据，不自动迁移已保存的凭据。Ubuntu 通过 apt 手动安装或升级。</p><p className="small-note">早期版本尚未使用 Windows 发布者代码签名证书，安装时可能显示未知发布者。</p></div></section>
      <BrandStory/>
    </main>
    <footer className="site-footer section-shell"><a className="footer-brand" href="#home">AsterHub<span>连接设备，看见运行。</span></a><div><span>Windows v{windowsVersion} · Ubuntu v{ubuntuVersion}</span><a href={repository} target="_blank" rel="noreferrer"><Github size={15}/>版本更新与发行说明<ArrowUpRight size={12}/></a></div></footer>
  </>;
}
