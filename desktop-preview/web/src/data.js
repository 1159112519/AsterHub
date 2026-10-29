export const devices = [
 {id:'aq',name:'aq-server',address:'117.50.162.34',user:'baiyang',group:'训练集群',os:'Ubuntu 22.04 LTS',gpuCount:8,cores:124,ram:925,latency:38,kind:'server',cpu:'Intel Xeon Gold 6338 × 2'},
 {id:'local',name:'本机',address:'Windows 11 · 此电脑',user:'lenovo',group:'我的设备',os:'Windows 11 Pro',gpuCount:1,cores:16,ram:32,latency:0,kind:'local',cpu:'AMD Ryzen 7 7840HS'},
 {id:'web',name:'web-node',address:'192.0.2.24',user:'developer',group:'开发环境',os:'Ubuntu 24.04 LTS',gpuCount:0,cores:8,ram:16,latency:62,kind:'server',cpu:'AMD EPYC · 8 vCPU'},
];
export const colors={cpu:'var(--blue)',gpu:'var(--green)',memory:'var(--purple)',network:'var(--amber)',storage:'var(--muted)'};
export function wave(seed=1,n=66,base=40,amp=16){return Array.from({length:n},(_,i)=>Math.max(2,Math.min(99,base+Math.sin(i*.33+seed)*amp*.45+Math.sin(i*.89+seed*3)*amp*.22+Math.cos(i*1.77+seed)*amp*.16+Math.sin(i*.1)*amp*.25)));}
export const baseSeries=[{name:'CPU',color:colors.cpu,values:wave(2,66,37,24)},{name:'GPU',color:colors.gpu,values:wave(6,66,51,32)},{name:'内存',color:colors.memory,values:wave(8,66,34,3)}];
export const gpuFixture=Array.from({length:8},(_,i)=>({id:i,name:'GeForce RTX 4090',util:[98,73,99,2,0,0,0,0][i],used:[21.03,21.03,22.52,.12,.02,.02,.02,.02][i],total:48,temp:[78,68,66,34,33,35,36,32][i],power:[309,291,303,24,21,23,35,20][i],fan:[100,100,100,64,64,64,64,64][i],series:wave(i,38,i<3?[86,61,91][i]:3,i<3?20:3)}));
export const processFixture=[
 {pid:'2499656',gpu:0,user:'zeyu',name:'python · train.py',path:'/home/zeyu/miniforge3/envs/G_DINO/bin/python train.py',memory:18.42,type:'C'},
 {pid:'2499657',gpu:1,user:'zeyu',name:'python · train.py',path:'/home/zeyu/miniforge3/envs/G_DINO/bin/python train.py',memory:18.42,type:'C'},
 {pid:'2499658',gpu:2,user:'zeyu',name:'python · train.py',path:'/home/zeyu/miniforge3/envs/G_DINO/bin/python train.py',memory:20.14,type:'C'},
 {pid:'2351443',gpu:0,user:'—',name:'其他进程',path:'其他进程',memory:2.61,type:'C'},
 {pid:'1508337',gpu:1,user:'—',name:'其他进程',path:'其他进程',memory:2.61,type:'C'},
];
export const remoteEntries={
 '/home/baiyang':[{name:'projects',kind:'folder',modified:'今天 08:42'},{name:'datasets',kind:'folder',modified:'昨天 16:30'},{name:'checkpoints',kind:'folder',modified:'今天 09:02'},{name:'logs',kind:'folder',modified:'今天 09:14'},{name:'train.py',kind:'python',size:'8.4 KB',modified:'今天 08:36'},{name:'environment.yml',kind:'file',size:'1.8 KB',modified:'10月26日'},{name:'README.md',kind:'file',size:'2.3 KB',modified:'10月25日'}],
 '/home/baiyang/projects':[{name:'grounding-dino',kind:'folder',modified:'今天 08:42'},{name:'configs',kind:'folder',modified:'昨天 11:23'},{name:'train.py',kind:'python',size:'12.6 KB',modified:'今天 08:36'}],
 '/home/baiyang/datasets':[{name:'inspection-v3',kind:'folder',modified:'昨天 16:30'},{name:'dataset.yaml',kind:'file',size:'428 B',modified:'昨天 16:30'}],
 '/home/baiyang/logs':[{name:'train-20261028.log',kind:'file',size:'284 KB',modified:'今天 09:14'}],
};
export const localEntries=[{name:'AsterHub',kind:'folder',modified:'今天 08:10'},{name:'模型训练',kind:'folder',modified:'昨天 18:24'},{name:'experiments',kind:'folder',modified:'今天 08:20'},{name:'config.yaml',kind:'file',size:'2.4 KB',modified:'今天 08:35'},{name:'train.py',kind:'python',size:'8.4 KB',modified:'今天 08:36'},{name:'notes.md',kind:'file',size:'1.2 KB',modified:'昨天 17:00'}];
export function minutesLabel(index,total=66,start=9*60){const value=start+Math.round(index/(total-1)*15);return `${String(Math.floor(value/60)).padStart(2,'0')}:${String(value%60).padStart(2,'0')}`;}
