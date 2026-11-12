const minute=60000;
export const anchor=Math.floor(Date.now()/minute)*minute;
export const step=30000;
export const count=28;
export const trendStart=anchor-(count-1)*step;
export const hhmm=t=>new Date(t).toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit',hour12:false});
const shape=[8,9,7,13,11,14,16,12,16,20,19,27,42,66,82,64,49,35,28,24,18,23,22,19,17,14,12,13];
const memCurve=[0,.1,0,.1,.2,.2,.3,.3,.4,.6,.8,1.1,1.6,2.4,3.6,5.0,5.9,6.5,6.9,7.3,7.7,8,8.3,8.5,8.7,8.8,8.9,9];
export const historyValues=(i,cpu,memory)=>({cpu:Math.min(98,cpu+shape[i]-12),memory:Math.max(5,memory-9+memCurve[i]),interval:30});
export function processEvidence(time,metric,id){
 const i=(time-trendStart)/step;
 if(!Number.isInteger(i)||i<0||i>=count||[3,18].includes(i))return null;
 const app=id==='local'?['renderer.exe','Code.exe','msedge.exe']:['python','node','ffmpeg'];
 if(metric==='cpu'){
  const load=shape[i],rows=[[app[0],Math.round(load*.63)],[app[1],Math.max(1,Math.round(load*.17))],[app[2],Math.max(1,Math.round(load*.08))]];
  return {rows,load};
 }
 return {index:i,values:new Map([[app[0],memCurve[i]*.47],[app[1],.55+Math.max(0,Math.min(.4,(i-8)*.035))],[app[2],1.1-Math.max(0,Math.min(.35,(i-6)*.025))]])};
}
export const apps=[['OneDrive.exe','同步'],['msedge.exe','浏览器'],['steam.exe','游戏更新'],['AsterHub.exe','应用更新']];
const intensity=[5,8,7,14,16,39,92,74,52,24,15,13,9,11,8];
const probeRtt=[21,20,22,34,31,29,58,43,null,27,36,92,43,31,23];
const probeLoss=[0,0,0,0,0,0,1,0,null,0,0,3,1,0,0];
const upload=[5,7,11,6,9,22,18,15,29,42,24,16,10,7,9];
const endpoints=[['同步节点','192.0.2.12:443'],['网页连接','198.51.100.24:443'],['内容分发','203.0.113.35:443'],['更新分发','192.0.2.47:443']];
export const traffic=intensity.map((down,i)=>{
 const split=(total,weights)=>{const assigned=weights.map(w=>Math.floor(w*total));return [...assigned,total-assigned.reduce((a,b)=>a+b,0)]};
 const d=split(down,i>4&&i<9?[.65,.14,.08,.04]:[.27,.27,.23,.08]);
 const u=split(upload[i],i>=8?[.56,.22,.07,.04]:[.31,.29,.16,.09]);
 const flows=apps.flatMap(([app],j)=>{
  const [label,address]=endpoints[j],d1=Math.ceil(d[j]*.68),u1=Math.ceil(u[j]*.73);
  return [{app,label,address,down:d1,up:u1},{app,label:'地址未细分的连接',address:null,down:d[j]-d1,up:u[j]-u1}];
 });
 flows.push({app:null,down:d[4],up:u[4]});
 const sent=20,lost=probeLoss[i],probe=probeRtt[i]==null?null:{rttMs:probeRtt[i],sent,lost,loss:lost*100/sent};
 return {time:anchor-(intensity.length-i)*minute,flows,probe};
});


