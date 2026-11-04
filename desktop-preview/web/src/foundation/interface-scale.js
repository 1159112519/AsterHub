// Host scale is the physical WebView zoom. Relabel its old 90% as the new
// 100% baseline without resizing an existing installation or migrating storage.
export function interfaceScaleOptions(current){
 const options=[[.9,'标准 · 100%'],[.99,'较大 · 110%'],[1.08,'大号 · 120%'],[1.17,'特大 · 130%']];
 if(Number.isFinite(current)&&current>=.9&&current<=1.3&&!options.some(([value])=>Math.abs(value-current)<.00001))options.push([current,Math.round(current/.9*100)+'%']);
 return options.sort((a,b)=>a[0]-b[0]);
}
