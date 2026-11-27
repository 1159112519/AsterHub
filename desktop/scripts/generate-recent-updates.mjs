import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';
export function recentUpdates(changelog,version,{localOnly=false}={}){
 const headings=[...changelog.matchAll(/^## ([^\s]+) · (\d{4}-\d{2}-\d{2})\s*$/gm)];
 if(headings[0]?.[1]!==version){
  // A local verification package can be newer than the published history it displays.
  const parse=value=>/^\d+(?:\.\d+){2,3}$/.test(value||'')?value.split('.').map(Number):[];
  const latest=parse(headings[0]?.[1]),current=parse(version);
  const part=[0,1,2,3].find(index=>(current[index]||0)!==(latest[index]||0));
  if(!localOnly||!latest.length||!current.length||part===undefined||(current[part]||0)<=(latest[part]||0))throw new Error('The newest changelog must match the package version.');
 }
 return headings.slice(0,3).map((heading,index)=>{
  const body=changelog.slice(heading.index+heading[0].length,headings[index+1]?.index??changelog.length),entry={version:heading[1],date:heading[2],added:[],improved:[],fixed:[]};
  for(const match of body.matchAll(/^- (.+)$/gm)){const text=match[1].trim();entry[/^(新增|支持)/.test(text)?'added':/^(修复|解决)/.test(text)?'fixed':'improved'].push(text)}
  if(!entry.added.length&&!entry.improved.length&&!entry.fixed.length)throw new Error('A release changelog must contain user-facing changes.');return entry;
 });
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),version=(await fs.readFile(path.join(root,'VERSION'),'utf8')).trim(),releases=recentUpdates(await fs.readFile(path.join(root,'CHANGELOG.md'),'utf8'),version,{localOnly:process.env.ASTERHUB_LOCAL_BUILD==='1'});
 await fs.writeFile(path.join(root,'web/src/native/about-releases.json'),JSON.stringify(releases,null,2)+'\n');console.log('RECENT_UPDATES='+version);
}
