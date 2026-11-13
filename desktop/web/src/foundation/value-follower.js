// Shared clock: samples update a target, never queue/restart easing timelines.
const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
export function createDampedValue(initial,{epsilon=.001,tau=.17}={}){
 let value=initial,target=initial,velocity=0,lastSample=null,timeConstant=tau;
 return {get value(){return value},get target(){return target},get active(){return value!==target||velocity!==0},
 setTarget(next,now=0,settleMs){if(!Number.isFinite(next)||next===target)return;if(lastSample!==null&&now>lastSample)timeConstant=clamp((now-lastSample)*.00016,.04,tau);if(Number.isFinite(settleMs)&&settleMs>0)timeConstant=clamp(settleMs/10000,.008,tau);lastSample=now;target=next},
 snap(next){if(Number.isFinite(next)){value=target=next;velocity=0}},
 advance(seconds){if(!this.active)return value;const dt=clamp(seconds,0,.1),omega=1/timeConstant,error=value-target,c=velocity+omega*error,decay=Math.exp(-omega*dt),next=target+(error+c*dt)*decay;velocity=(velocity-omega*c*dt)*decay;value=(next-target)*error<0?target:next;if(value===target||Math.abs(value-target)<epsilon&&Math.abs(velocity)<epsilon*omega){value=target;velocity=0}return value}
 };
}
export function createFrameClock({request=cb=>requestAnimationFrame(cb),cancel=id=>cancelAnimationFrame(id),now=()=>performance.now(),onError=error=>typeof globalThis.reportError==='function'?globalThis.reportError(error):console.error(error)}={}){
 const active=new Set();let frame=null,last=0,ticking=false;
 const schedule=()=>{if(active.size&&frame===null&&!ticking)frame=request(tick)};
 const tick=time=>{frame=null;ticking=true;const dt=Math.max(0,time-last)/1000;last=time;
  try{for(const follower of [...active]){if(!active.has(follower))continue;try{if(!follower.step(dt))active.delete(follower)}catch(error){active.delete(follower);try{onError(error)}catch{}}}}
  finally{ticking=false;schedule()}
 };
 return {now,add(follower){active.add(follower);if(frame===null&&!ticking)last=now();schedule()},remove(follower){active.delete(follower);if(!active.size&&frame!==null){cancel(frame);frame=null}},get size(){return active.size}};
}
let sharedClock;
export function createValueFollower(initial,paint,{epsilon=.001,tau=.17,clock}={}){
 clock??=sharedClock??=createFrameClock();const state=createDampedValue(initial,{epsilon,tau});let disposed=false,deadline=Infinity;
 const follower={step:dt=>{if(clock.now()>=deadline)state.snap(state.target);paint(state.advance(dt));return state.active}};
 return {target(next,{immediate=false,settleMs}={}){if(disposed||!Number.isFinite(next))return;if(immediate){deadline=Infinity;state.snap(next);clock.remove(follower);paint(next);return}deadline=Number.isFinite(settleMs)&&settleMs>0?clock.now()+settleMs:Infinity;state.setTarget(next,clock.now(),settleMs);if(state.active)clock.add(follower)},dispose(){disposed=true;clock.remove(follower)},get value(){return state.value}};
}
