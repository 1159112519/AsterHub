export function networkScaleState(samples,previous){
 const values=samples.flatMap(s=>[s.network?.DownloadBytesPerSecond,s.network?.UploadBytesPerSecond]).filter(Number.isFinite),peak=Math.max(1,...values),power=10**Math.floor(Math.log10(peak*1.15)),max=Math.ceil(peak*1.15/power)*power,end=samples.at(-1)?.time??0;
 if(!previous||max>previous.max||end<previous.end)return {max,end,smallerSince:null};
 if(max===previous.max)return {...previous,end,smallerSince:null};
 const smallerSince=previous.smallerSince??end;
 return end-smallerSince>=30000?{max,end,smallerSince:null}:{...previous,end,smallerSince};
}
