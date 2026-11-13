import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';
export function recentUpdates(changelog,version){
 const headings=[...changelog.matchAll(/^## ([^\s]+) · (\d{4}-\d{2}-\d{2})\s*$/gm)];
 if(headings[0]?.[1]!==version)throw new Error('The newest changelog must match the package version.');
 return headings.slice(0,3).map((heading,index)=>{
  const body=changelog.slice(heading.index+heading[0].length,headings[index+1]?.index??changelog.length),entry={version:heading[1],date:heading[2],added:[],improved:[],fixed:[]};
  for(const match of body.matchAll(/^- (.+)$/gm)){const text=match[1].trim();entry[/^(新增|支持)/.test(text)?'added':/^(修复|解决)/.test(text)?'fixed':'improved'].push(text)}
  if(!entry.added.length&&!entry.improved.length&&!entry.fixed.length)throw new Error('A release changelog must contain user-facing changes.');return entry;
 });
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),version=(await fs.readFile(path.join(root,'VERSION'),'utf8')).trim(),releases=recentUpdates(await fs.readFile(path.join(root,'CHANGELOG.md'),'utf8'),version);
 await fs.writeFile(path.join(root,'web/src/native/about-releases.json'),JSON.stringify(releases,null,2)+'\n');console.log('RECENT_UPDATES='+version);
}
