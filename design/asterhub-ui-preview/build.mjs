import fs from 'node:fs/promises';
import {english} from './english.mjs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../..');
const app=path.resolve(process.argv[2]||'desktop-preview'),src=path.join(app,'web/src'),web=path.join(path.resolve(process.env.ASTERHUB_PREVIEW_OUT||path.join(root,'outputs/AsterHub-毛玻璃UI演示')),'ui');
const release=path.resolve(process.env.ASTERHUB_PREVIEW_RELEASE||path.join(root,'work/release-0.4.3.2-20261110/AsterHub-0.4.3.2-final'));
const published=path.join(release,'resources/workbench');
const {build}=await import(pathToFileURL(path.join(app,'web/node_modules/vite/dist/node/index.js')).href);
const inject=f=>JSON.stringify(path.join(dir,f).replaceAll('\\','/'));
const replace=(s,a,b)=>{if(!s.includes(a))throw Error('Missing preview hook: '+a);return s.replace(a,()=>b)};
const hooks=new Set();
for(const en of [false,true]){
await build({configFile:false,root:path.join(app,'web'),publicDir:false,resolve:{alias:{'@aster':src,react:path.join(app,'web/node_modules/react')}},esbuild:{jsx:'automatic'},build:{outDir:web,emptyOutDir:!en,assetsInlineLimit:0},plugins:[{name:'asterhub-design-preview',enforce:'pre',transform(s,id){
 const file=id.replaceAll('\\','/');
 if(file.endsWith('/native/bridge.js')){hooks.add('bridge');return `import {fixtureTransport} from ${inject('fixture.js')};\n`+replace(s,'createClient(globalThis.chrome?.webview)','createClient(fixtureTransport)')}
 if(file.endsWith('/native/NativeApp.jsx')){hooks.add('shell');return `import ${inject('study.css')};\n`+replace(replace(s,"'navigation',()=>({page:'overview'})","'navigation',()=>({page:'cpu'})"),'<span className="topbar-separator"/>','<span className="study-mode">UI 演示</span><span className="topbar-separator"/>')}
 if(file.endsWith('/appearance/resolve-appearance.js')){hooks.add('palette');return replace(s,"export const validTheme=id=>legacyIds.includes(id)||packs.some(p=>p.id===id)?id:'graphite';","export const validTheme=id=>id==='study-glass'?id:'graphite';")}
 if(file.endsWith('/native/AppearanceChoices.jsx')){hooks.add('swatches');return replace(replace(s,/export const skins=.+?;\r?\n/.exec(s)[0],"export const skins=[{id:'graphite',mode:'dark'},{id:'study-glass',mode:'dark'}];\n"),"aria-label={'皮肤 '+String(index+1).padStart(2,'0')}",`aria-label={index?'雾面玻璃':'经典深色'}`)}
 if(file.endsWith('/native/Preferences.jsx')){hooks.add('preferences');const a=s.indexOf('<SkinChoices '),b=s.indexOf('<RestartNotice',a),picker=s.slice(a,b);if(a<0||b<0)throw Error('Appearance controls missing');return replace(s.slice(0,a)+s.slice(b),'<h4>GPU 显示风格</h4>',`<div className="native-preferences-fields"><label className="field">界面语言<ThemedSelect aria-label="界面语言" value={document.documentElement.lang==='en'?'en':'zh'} onChange={e=>location.assign(e.target.value==='en'?'/english.html':'/index.html')}><option value="zh">简体中文</option><option value="en">English</option></ThemedSelect></label></div><h4>GPU 显示风格</h4>`).replace('窗口毛玻璃</label></>}', '窗口毛玻璃</label><div className="study-appearance-library">'+picker+'</div></>}')}
 if(file.endsWith('/native/TaskCenter.jsx')){hooks.add('task');return replace(replace(s,`<h2 id={triggerId+'-title'}>任务</h2>`,`<div className="study-task-heading"><span className="study-medallion"><ListChecks size={15} aria-hidden="true"/></span><h2 id={triggerId+'-title'}>任务</h2></div>`),"{state==='success'&&<Check size={13} aria-hidden=\"true\"/>}",`<i className="study-task-state" aria-hidden="true">{state==='success'?<Check size={10}/>:state==='error'?<X size={10}/>:null}</i>`)}
 if(file.endsWith('/analysis/ResourceAnalysis.jsx')){hooks.add('analysis');return replace(replace(replace(replace(replace(s,'import {ChevronDown}', 'import {ChevronDown,Activity,ListOrdered}'),"`${metric}-resource-tools`,()=>({tab:'monitor'})","`${metric}-resource-tools`,()=>({tab:'analysis'})"),'<h2>{title}</h2>',`<h2 className="study-section-heading"><span className="study-medallion"><Activity size={16} aria-hidden="true"/></span>{title}</h2>`),'<h3>{rankTitle}</h3>',`<h3 className="study-section-heading"><span className="study-medallion"><ListOrdered size={14} aria-hidden="true"/></span>{rankTitle}</h3>`),'<p data-selection>{timeLabel(prior,point)}</p>',`{markers.length?<div className="study-range" aria-label="对比时段"><div className="study-range-keys">{markers.map(marker=><span key={marker.key} data-endpoint={marker.key} style={{'--study-key':marker.color}}><i aria-hidden="true"/>{marker.label}<time>{clock(marker.time)}</time></span>)}</div><div className="study-rail" aria-hidden="true" style={{'--study-a':(prior.time-snapshot.from)/Math.max(1,snapshot.to-snapshot.from)*100+'%','--study-b':(point.time-snapshot.from)/Math.max(1,snapshot.to-snapshot.from)*100+'%'}}><i/><b/><b/></div></div>:<p data-selection>{timeLabel(prior,point)}{current?'（当前）':''}</p>}`).replace("color:'var(--amber)'},{key:'end'","color:'var(--study-start)'},{key:'end'").replace("color:'var(--blue)'}]:[]","color:'var(--study-end)'}]:[]")}
}},{name:'asterhub-preview-english',enforce:'pre',transform(s,id){if(en&&/\.(jsx|js|mjs)$/.test(id)&&!id.includes('node_modules')&&(id.includes('/src/')||id.replaceAll('\\','/').endsWith('/fixture.js')))return english(s)}}]});
if(hooks.size!==7)throw Error('Incomplete UI injection');
const index=path.join(web,'index.html');
const html=(await fs.readFile(index,'utf8')).replace(/<title>.*?<\/title>/,en?'<title>AsterHub · UI Preview</title>':'<title>AsterHub · 毛玻璃 UI 演示</title>');
await fs.writeFile(path.join(web,en?'english.html':'chinese.html'),en?html.replace('lang="zh-CN"','lang="en"'):html);
}
await fs.rename(path.join(web,'chinese.html'),path.join(web,'index.html'));
for(const name of ['fonts','brand-icons','asterhub.svg','startup.js','startup.css'])await fs.cp(path.join(published,name),path.join(web,name),{recursive:true});
console.log(web);

