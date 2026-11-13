// Sum physical device rates only; absent counters are unknown, while zero is valid.
export function storageRate(sample,key){const values=(sample?.storage||[]).map(d=>d[key]).filter(v=>Number.isFinite(v)&&v>=0);return values.length?values.reduce((sum,value)=>sum+value,0):null;}
export function storageRateSamples(samples){return samples.map(s=>({time:s.time,network:{DownloadBytesPerSecond:storageRate(s,'ReadBytesPerSecond'),UploadBytesPerSecond:storageRate(s,'WriteBytesPerSecond')}}));}
