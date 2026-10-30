import { useEffect, useRef, useState } from 'react';
import { ArrowUpRight, Check, ChevronRight, Maximize2, X } from 'lucide-react';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
gsap.registerPlugin(ScrollTrigger);

const media = (name: string) => `./media/asterhub-preview6-final-${name}.webp`;
export const chapters = [
  { id:'overview', label:'资源概览', eyebrow:'THE WHOLE PICTURE', title:['先看全局。','再决定关注哪里。'], text:'CPU、GPU、内存、网络与存储，放在同一个工作空间。设备忙在哪里、还有多少余量，不必在多个工具之间来回确认。', points:['点击资源卡片，在当前页面展开详情','底部速览常驻，切换页面仍能看见状态','按设备提供的能力显示数据'], image:'overview', caption:'资源概览 · 同一视野里的五类资源' },
  { id:'gpu', label:'GPU', eyebrow:'EVERY GPU, IN FOCUS', title:['每一张卡。','都值得看清。'], text:'从一张显卡到多卡设备，利用率、显存、温度与功耗一起呈现。选择经典卡片，或用趋势卡片看见变化，风格由你决定。', points:['多卡同时可见，区分忙碌与空闲','展开 GPU 详情，查看完整进程命令','结束进程前确认，并核验目标身份'], image:'eight-gpu', caption:'八卡界面示意 · 经典与趋势两种显示风格' },
  { id:'terminal', label:'终端', eyebrow:'FROM INSIGHT TO ACTION', title:['看到了。','就接着做。'], text:'发现需要处理的任务，不必离开工作台。打开本机 PowerShell 或远程 SSH 终端；切换资源页面后，会话继续保留。', points:['多个标签，让任务各自有序','字体、字号与背景随偏好调整','切换页面不会重建终端会话'], image:'terminal', caption:'终端工作空间 · 多个会话，各自继续' },
  { id:'files', label:'文件', eyebrow:'A CLEAR PATH FOR FILES', title:['两端的文件。','放在一起处理。'], text:'本机与远程目录并排，路径可以直接输入，分栏宽度可以调整。上传与下载采用复制，源文件留在原处。', points:['文件与文件夹复制，进度集中查看','同名覆盖明确确认，支持取消传输','新建且未改动的副本可有限撤销'], image:'files', caption:'双栏文件 · 路径、选择与复制传输' },
  { id:'network', label:'网络', eyebrow:'FOLLOW THE FLOW', title:['不只看速度。','也看连接。'], text:'上下行吞吐放在同一张图里，按接口切换观察。结合地址、链路与累计计数，分清数据从哪里来、向哪里去。', points:['下载与上传趋势同时比较','按网络接口查看链路和地址信息','底部保留实时上下行速率'], image:'network', caption:'网络监控 · 吞吐趋势与接口信息' },
  { id:'storage', label:'存储', eyebrow:'ROOM TO KEEP WORKING', title:['空间够不够。','磁盘忙不忙。'], text:'卷容量与物理磁盘活动分开呈现。读取、写入、活动时间和队列长度，让“空间不足”和“磁盘繁忙”有各自的线索。', points:['各存储卷的已用、可用与总容量','物理磁盘读写趋势和当前队列','设备未提供的指标明确显示状态'], image:'storage', caption:'存储监控 · 容量与 I/O 分开看' },
  { id:'history', label:'历史', eyebrow:'A MOMENT WORTH REVISITING', title:['回到那个时刻。','看看当时发生了什么。'], text:'资源变化持续归档在本机。选择一个时间点，轻量浮层带出当时的状态与进程，把偶发的峰值留作可以回看的线索。', points:['按时间范围浏览本地记录','CPU、GPU 与内存趋势切换','历史进程只读，避免误操作当前任务'], image:'history', caption:'历史记录 · 状态与进程留在同一时刻' },
];
type Viewer = {image:string; caption:string};

export function usePageMotion() {
  useEffect(() => {
    const mm = gsap.matchMedia();
    mm.add('(prefers-reduced-motion: no-preference)', () => {
      const context = gsap.context(() => {
        document.querySelectorAll<HTMLElement>('[data-reveal]').forEach(node => {
          gsap.from(node, { y:24, autoAlpha:0, duration:.7, ease:'power2.out', scrollTrigger:{trigger:node,start:'top 90%',once:true} });
        });
      });
      return () => context.revert();
    });
    // Inline tablet/phone chapters reveal independently, once, with soft deceleration.
    // GSAP restores styles when the viewport or reduced-motion setting changes.
    mm.add('(max-width: 1099px) and (prefers-reduced-motion: no-preference)', () => {
      const context = gsap.context(() => {
        document.querySelectorAll<HTMLElement>('[data-story-reveal]').forEach(node => {
          const isImage = node.dataset.storyReveal === 'image';
          const chapter = node.closest<HTMLElement>('.story-step');
          const direction = Array.from(chapter?.parentElement?.children || []).indexOf(chapter!) % 2 ? -1 : 1;
          gsap.from(node, {
            autoAlpha: 0, y: isImage ? 28 : 18, x: isImage ? 0 : direction * 8,
            scale: isImage ? .975 : 1, rotation: isImage ? direction * .35 : 0,
            transformOrigin: '50% 70%', duration: isImage ? .95 : .75,
            ease: 'power3.out', clearProps: 'transform,opacity,visibility,transformOrigin',
            scrollTrigger: { trigger: node, start: 'top 94%', once: true },
          });
        });
      });
      return () => context.revert();
    });
    let frame = 0;
    const progress = () => { cancelAnimationFrame(frame); frame=requestAnimationFrame(()=>{ const distance=document.documentElement.scrollHeight-innerHeight;document.documentElement.style.setProperty('--page-progress',String(distance?scrollY/distance:0)); }); };
    addEventListener('scroll',progress,{passive:true});addEventListener('resize',progress);progress();
    return () => {mm.revert();cancelAnimationFrame(frame);removeEventListener('scroll',progress);removeEventListener('resize',progress);};
  },[]);
}

export function StoryExperience() {
  const root=useRef<HTMLElement>(null), stage=useRef<HTMLDivElement>(null);
  const [active,setActive]=useState(0),[gpuStyle,setGpuStyle]=useState('trend'),[viewer,setViewer]=useState<Viewer|null>(null);
  const dialog=useRef<HTMLDialogElement>(null),[zoom,setZoom]=useState(false);
  const image=(i:number)=>media(i===1&&gpuStyle==='classic'?'classic':chapters[i].image);
  useEffect(()=>{
    let frame=0;
    const update=()=>{cancelAnimationFrame(frame);frame=requestAnimationFrame(()=>{const nodes=root.current?.querySelectorAll<HTMLElement>('.story-step');if(!nodes?.length)return;const center=innerHeight*.48;let nearest=0,distance=Infinity;nodes.forEach((node,i)=>{const r=node.getBoundingClientRect(),d=Math.abs(r.top+r.height/2-center);if(d<distance){distance=d;nearest=i;}});setActive(nearest);});};
    addEventListener('scroll',update,{passive:true});addEventListener('resize',update);update();return()=>{cancelAnimationFrame(frame);removeEventListener('scroll',update);removeEventListener('resize',update);};
  },[]);
  useEffect(()=>{
    if(!stage.current)return;
    const reduce=matchMedia('(prefers-reduced-motion: reduce)').matches;
    const frames=stage.current.querySelectorAll('.story-shot');
    gsap.to(frames,{autoAlpha:i=>i===active?1:0,scale:i=>i===active?1:1.025,duration:reduce?0:.48,ease:'power2.out',overwrite:true});
    return()=>{gsap.killTweensOf(frames);};
  },[active]);
  useEffect(()=>{
    if(!viewer)return;
    const previous=document.activeElement as HTMLElement|null,overflow=document.body.style.overflow;
    setZoom(false);dialog.current?.showModal();document.body.style.overflow='hidden';
    return()=>{dialog.current?.close();document.body.style.overflow=overflow;previous?.focus({preventScroll:true});};
  },[viewer]);
  const choose=(i:number)=>{setActive(i);document.getElementById('story-'+chapters[i].id)?.scrollIntoView({behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth',block:'center'});};
  const styleControls=(suffix:string)=><div className="story-style-switch" role="group" aria-label={'GPU 卡片风格'+suffix}>{[['trend','趋势卡片'],['classic','经典卡片']].map(([value,label])=><button key={value} onClick={()=>setGpuStyle(value)} aria-pressed={gpuStyle===value}>{label}</button>)}</div>;
  return <section className="story-experience section-shell" id="experience" ref={root}>
    <div className="story-heading" data-reveal><p className="section-kicker">MEET YOUR WORKSPACE</p><h2>从看见状态，<br/><span>到接着工作。</span></h2><p>沿着一次真实的工作，认识 AsterHub。<br/>往下滚动，界面随章节展开。</p></div>
    <div className="story-layout">
      <div className="story-visual">
        <div className="story-frame" ref={stage} data-active-scene={chapters[active].id}>
          {chapters.map((chapter,i)=><img key={chapter.id} className={'story-shot'+(active===i?' active':'')} src={image(i)} width="1600" height="1100" alt={'AsterHub '+chapter.label+'，示例数据'} aria-hidden={active!==i} loading="lazy" decoding="async"/>)}
          <button className="expand-image" aria-label="放大当前界面" onClick={()=>setViewer({image:image(active),caption:chapters[active].caption})}><Maximize2 size={17}/></button>
        </div>
        <div className="story-frame-caption"><span>{chapters[active].caption}</span><span>Windows 预览 · 示例数据</span></div>
        <div className="chapter-progress" aria-hidden="true"><span style={{width:`${(active+1)/chapters.length*100}%`}}/></div>
        <nav className="chapter-nav" aria-label="产品演示章节">{chapters.map((chapter,i)=><button key={chapter.id} onClick={()=>choose(i)} aria-current={active===i?'step':undefined}>{chapter.label}</button>)}</nav>
        {active===1&&styleControls('桌面')}
      </div>
      <div className="story-copy">{chapters.map((chapter,i)=><article className={'story-step'+(active===i?' is-current':'')} id={'story-'+chapter.id} key={chapter.id} data-scene={chapter.id}>
        <div className="story-inline-image" data-story-reveal="image"><button onClick={()=>setViewer({image:image(i),caption:chapter.caption})} aria-label={'放大'+chapter.label+'界面'}><img src={image(i)} width="1600" height="1100" alt={'AsterHub '+chapter.label+'，示例数据'} loading="lazy" decoding="async"/><span><Maximize2 size={14}/>查看界面</span></button>{i===1&&styleControls('移动端')}<p>Windows 预览界面 · 示例数据</p></div>
        <div className="story-step-heading" data-story-reveal="copy"><span className="story-number">0{i+1}</span><span>{chapter.eyebrow}</span></div>
        <h3 data-story-reveal="copy">{chapter.title[0]}<br/><span>{chapter.title[1]}</span></h3><p className="story-description" data-story-reveal="copy">{chapter.text}</p>
        <ul data-story-reveal="copy">{chapter.points.map(point=><li key={point}><Check size={15}/>{point}</li>)}</ul>
        {i===3&&<p className="story-boundary" data-story-reveal="copy">外部文件可通过系统选择器上传；撤销不还原已经覆盖的旧文件。</p>}
      </article>)}</div>
    </div>
    <dialog ref={dialog} className="screenshot-viewer" aria-label="AsterHub 界面大图" onClose={()=>setViewer(null)} onClick={e=>{if(e.target===e.currentTarget)setViewer(null);}}>{viewer&&<div className="viewer-panel"><header><div><strong>{viewer.caption}</strong><span>Windows 预览界面 · 示例数据</span></div><button onClick={()=>setZoom(v=>!v)} aria-pressed={zoom}>{zoom?'适应窗口':'放大细节'}</button><button className="viewer-close" aria-label="关闭界面大图" onClick={()=>setViewer(null)}><X size={22}/></button></header><div className={'viewer-image'+(zoom?' is-zoomed':'')}><img src={viewer.image} alt={viewer.caption}/></div></div>}</dialog>
  </section>;
}

const questions=[
 ['哪些设备可以使用？','当前重点是 Windows 桌面客户端，可以监控本机，并通过 SSH 连接远程服务器。没有独立显卡的设备，也可以查看 CPU、内存、网络与存储。不同系统能够提供的具体指标可能不同。'],
 ['Windows 与 Ubuntu 的功能一样吗？','有所不同。此页面展示 Windows 0.3.0 预览版。Ubuntu 0.1.3 支持资源监测与历史记录，适用于 Ubuntu 22.04 amd64；请在下载区域查看各平台的版本及支持范围。'],
 ['监控记录存在哪里？','资源历史保存在本机。连接远程设备时通过 SSH 获取状态。反馈功能提交你主动填写的内容，不会自动附带终端记录或密码。'],
 ['预览版会覆盖原来的稳定版吗？','Windows 预览版采用独立的安装通道和数据目录，原稳定版仍可下载。两者不自动迁移已保存的凭据；同一通道内升级保留配置与历史。'],
 ['更新需要重新手动安装吗？','预览版中打开“偏好设置 → 检查更新”。发现新版本后，可以下载安装包并校验发布签名、文件大小与散列；确认后程序退出、安装并重新启动。活动终端会话会结束，文件传输进行时会阻止安装。'],
 ['怎样反馈问题或建议？','打开“偏好设置 → 反馈与建议”，填写问题和复现步骤后提交。收到回执才表示保存成功；如果失败，表单内容会保留，方便稍后重试。'],
];
export function Questions(){return <section className="questions section-shell" id="questions"><div data-reveal><p className="section-kicker">A FEW THINGS TO KNOW</p><h2>开始之前，<br/><span>你可能想知道。</span></h2><p>能力说清楚，选择更轻松。</p></div><div className="question-list">{questions.map(([question,answer])=><details key={question}><summary>{question}<ChevronRight size={20}/></summary><p>{answer}</p></details>)}</div></section>;}
export function ProductPrinciples(){return <section className="product-principles section-shell"><div className="principles-heading" data-reveal><p className="section-kicker">MADE TO FEEL FAMILIAR</p><h2>功能在场。<br/><span>打扰，少一点。</span></h2><p>不是让每一个数字争夺注意力。<br/>是让需要的信息，在需要时出现。</p></div><div className="principle-rows"><article data-reveal><span>01</span><h3>外观，由你选择。</h3><p>经典与趋势卡片保持同一套配色。主题、界面密度、终端字体和动态效果，按使用习惯调整。</p></article><article data-reveal><span>02</span><h3>细节，有来有回。</h3><p>从卡片展开的浮层，沿着来源出现。数字与曲线平滑变化；不需要动态时，也可以减少动画。</p></article><article data-reveal><span>03</span><h3>重要操作，明确一点。</h3><p>结束进程要确认，覆盖文件要确认。无法提供的指标明确显示，不用无意义的数字掩盖未知。</p></article></div></section>;}
export function BrandStory(){return <section className="brand-story section-shell" data-reveal><p className="section-kicker">A STAR. A CONNECTION. A CLEARER VIEW.</p><h2>AsterHub</h2><p className="brand-tagline">连接设备，看见运行。</p><p>Aster 代表星，Hub 代表连接的中心。<br/>每台设备独立运行，AsterHub 将它们连接，让状态清晰可见。</p><a href="#download">找到适合你的版本<ArrowUpRight size={17}/></a></section>;}
