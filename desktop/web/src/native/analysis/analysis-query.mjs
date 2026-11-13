// Keep a small in-memory cache shared across tool mounts. Same-key callers
// share one native read; a different device, metric or range never sees it.
export function createAnalysisQuery(read,{limit=6,ttl=30000,now=Date.now}={}){
 const results=new Map(),pending=new Map();
 function peek(key){
  const entry=results.get(key);
  if(entry){results.delete(key);results.set(key,entry);}
  return entry;
 }
 function query(key,params,{force=false}={}){
  if(pending.has(key))return pending.get(key);
  const cached=peek(key);
  if(!force&&cached&&now()-cached.at<ttl)return Promise.resolve(cached.value);
  const request=Promise.resolve().then(()=>read(params)).then(value=>{
   results.delete(key);results.set(key,{value,at:now()});
   while(results.size>limit)results.delete(results.keys().next().value);
   return value;
  }).finally(()=>pending.delete(key));
  pending.set(key,request);return request;
 }
 return {peek,query};
}
