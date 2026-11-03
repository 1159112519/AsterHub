import test from 'node:test';
import assert from 'node:assert/strict';
import {normalize} from '../native/telemetry.js';

const snapshot=names=>normalize({processes:names.map((Name,i)=>({Gpu:'0',Pid:String(700+i),Name,Memory:'1024 MiB',User:'alice',IdentityToken:'unchanged'}))});
test('GPU identity keeps full executable paths with spaces and Unicode across snapshot reloads',()=>{
 const paths=['/home/alice/模型 one/venv/bin/python','/srv/task two/venv/bin/python',String.raw`C:\Program Files\任务一\python.exe`,String.raw`D:\实验二\python.exe`];
 const rows=snapshot(paths).processes;
 assert.deepEqual(rows.map(p=>p.name),paths);
 assert.deepEqual(rows.map(p=>p.path),paths);
 assert.deepEqual(rows.map(p=>p.pid),['700','701','702','703']);
 assert.equal(rows[0].memory,1);
 const restored=normalize(JSON.parse(JSON.stringify({processes:paths.map((Name,i)=>({Gpu:'0',Pid:String(700+i),Name}))})));
 assert.deepEqual(restored.processes.map(p=>p.name),paths);
});
test('short known names remain usable when an executable path is unavailable',()=>{
 assert.deepEqual(snapshot(['python','python.exe','[Not Found]','其他进程']).processes.map(p=>p.name),['python','python.exe']);
});
