export function taskbarScrollValues(values,unit,scale){
 const raw=values.slice(-13);if(raw.length<2)return [];
 const normalized=raw.map(v=>unit==='%'?Math.max(0,Math.min(1,v/100)):Math.max(0,v)/(Math.max(0,v)+scale));
 return [...Array(13-normalized.length).fill(normalized[0]),...normalized];
}
export function taskbarScrollPath(points,x,y,progress=1){
 if(points.length<2)return '';const d=2,px=i=>x+i*d-progress*d,py=i=>y+3-points[i]*8;
 let command=`M${px(0)},${py(0)}`;
 for(let i=1;i<points.length;i++)command+=` C${px(i)-d*.55},${py(i-1)} ${px(i)-d*.45},${py(i)} ${px(i)},${py(i)}`;
 return command;
}
