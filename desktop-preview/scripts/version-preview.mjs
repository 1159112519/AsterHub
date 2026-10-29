import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
let current;try{current=(await fs.readFile(path.join(root,'VERSION'),'utf8')).trim()}catch{current='0.3.0-preview.3'}
const m=current.match(/^(\d+)\.(\d+)\.(\d+)-preview\.(\d+)$/);if(!m)throw Error('Invalid preview version');const next=`${m[1]}.${m[2]}.${m[3]}-preview.${Number(m[4])+1}`;
for(const file of ['installer/notice.txt','host/AssemblyInfo.cs','host/WorkbenchHost.cs','web/src/native/NativeApp.jsx','web/src/native/Preferences.jsx','scripts/package-preview.mjs','installer/build.ps1','installer/AsterHubPreview.iss','installer/verify.ps1','installer/verify-installed.mjs']){
 const p=path.join(root,file);let s=await fs.readFile(p,'utf8');s=s.replaceAll(current,next).replaceAll(`Windows Preview ${m[4]}`,`Windows Preview ${Number(m[4])+1}`);if(file==='host/AssemblyInfo.cs')s=s.replace(/AssemblyInformationalVersion\("[^"]+"\)/,`AssemblyInformationalVersion("${next}")`).replace(/AssemblyFileVersion\("[^"]+"\)/,`AssemblyFileVersion("${m[1]}.${m[2]}.${m[3]}.${Number(m[4])+1}")`);if(file.endsWith('.iss'))s=s.replaceAll(`VersionInfoVersion=${m[1]}.${m[2]}.${m[3]}.${m[4]}`,`VersionInfoVersion=${m[1]}.${m[2]}.${m[3]}.${Number(m[4])+1}`).replaceAll(`VersionInfoProductVersion=${m[1]}.${m[2]}.${m[3]}.${m[4]}`,`VersionInfoProductVersion=${m[1]}.${m[2]}.${m[3]}.${Number(m[4])+1}`);await fs.writeFile(p,s)
}
await fs.writeFile(path.join(root,'VERSION'),next+'\n');console.log('PREVIEW_VERSION='+next);
