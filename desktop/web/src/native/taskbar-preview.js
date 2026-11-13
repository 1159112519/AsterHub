// Only reads the cache; display throttling never modifies device sampling.
export function createTaskbarPreviewFeed(client,deviceId,onSnapshot,{setTimer=setTimeout,clearTimer=clearTimeout,interval=1000,minInterval=0,now=()=>performance.now()}={}){
 let active=true,pending=false,dirty=false,timer=null,last=-Infinity;const controller=new AbortController();
 const remaining=()=>Math.max(0,minInterval-(now()-last));
 function schedule(){if(!active)return;dirty=true;if(!pending){clearTimer(timer);timer=setTimer(read,Math.max(60,remaining()));}}
 async function read(){if(!active||pending)return;clearTimer(timer);if(remaining()>0){timer=setTimer(read,remaining());return;}last=now();pending=true;dirty=false;
  try{const result=await client.request('taskbar.status',{deviceId},{signal:controller.signal});if(active)onSnapshot(result?.snapshot??null);}
  catch(error){if(active&&error.name!=='AbortError')onSnapshot(null);}
  finally{pending=false;if(active)timer=setTimer(read,Math.max(remaining(),dirty?60:interval));}
 }
 const offCapture=client.subscribe(schedule,'capture'),offLight=client.subscribe(schedule,'capture-light');read();
 return()=>{active=false;controller.abort();clearTimer(timer);offCapture();offLight();};
}
