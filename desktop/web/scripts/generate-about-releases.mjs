// Regenerate after editing CHANGELOG.md: node web/scripts/generate-about-releases.mjs
// This copies published entries verbatim; no unreleased or inferred features.
import fs from 'node:fs/promises';
const changelog=new URL('../../CHANGELOG.md',import.meta.url);
const sections=(await fs.readFile(changelog,'utf8')).split(/^##\s+/m).slice(1,4);
const releases=sections.map(section=>{
 const [heading,...lines]=section.split(/\r?\n/);
 const match=heading.match(/^(\S+)\s*·\s*(\d{4}-\d{2}-\d{2})/);
 if(!match)throw new Error('Unrecognized release heading: '+heading);
 const release={version:match[1],date:match[2],added:[],improved:[],fixed:[]};
 for(const line of lines){if(!line.startsWith('- '))continue;const item=line.slice(2);const group=/^修复/.test(item)?'fixed':/^新增|^正式采用/.test(item)?'added':'improved';release[group].push(item)}
 return release;
});
if(releases.length!==3)throw new Error('Expected three documented releases');
await fs.writeFile(new URL('../src/native/about-releases.json',import.meta.url),JSON.stringify(releases,null,2)+'\n','utf8');
console.log('Generated recent updates: '+releases.map(release=>release.version).join(', '));
