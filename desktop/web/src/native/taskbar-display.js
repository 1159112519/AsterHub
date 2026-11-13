// Match the installed widget's presentation contract; telemetry remains untouched.
// Inter Medium tnum advances from the bundled font (2048 units per em).
export const taskbarGlyphWidth=c=>c==='—'?13:(c==='.'?550:1327)*12.5/2048;
export function taskbarReadoutLeftShift(id,numberStart,track,previousRight){
 if(id==='up'||id==='down')return 0;
 const digit=taskbarGlyphWidth('0'),dot=taskbarGlyphWidth('.'),dash=taskbarGlyphWidth('—');
 const widest=Math.max(Math.floor((track+.001)/digit)*digit,Math.floor(Math.max(0,track-dot+.001)/digit)*digit+dot,track>=dash?dash:0);
 return Math.min(digit/2,Math.max(0,numberStart+track-Math.min(track,widest)-previousRight-1));
}
export const taskbarUnitFamily=(id,fallback)=>{
 if(['down','up','storageRead','storageWrite'].includes(id))return ['B/s','K/s','M/s','G/s','T/s','P/s','E/s'];
 if(['cpuFrequency','gpuFrequency'].includes(id))return ['GHz','THz','PHz','EHz'];
 if(id==='gpuPower')return ['W','kW','MW','GW'];
 if(id==='memoryUsed'||id==='memoryFree')return ['GB','TB','PB','EB','GiB','TiB','PiB','EiB'];
 return [fallback];
};
export const taskbarNumberWidth=text=>[...text].reduce((n,c)=>n+taskbarGlyphWidth(c),0);
export function taskbarPlaceKey(text,index){const dot=text.includes('.')?text.indexOf('.'):text.length;return index<dot?'i'+(dot-index-1):index===dot?'dot':'f'+(index-dot);}
export function taskbarDisplay(sample,label,fallbackUnit='',budget){
 const stale=sample?.stale||sample?.value==null||!Number.isFinite(sample.value);let number=sample?.value??0,unit=stale?'':sample?.unit??fallbackUnit,text=stale?'—':sample.Text;
 let units=null,radix=1000;
 if(unit.endsWith('/s')){units=['B/s','K/s','M/s','G/s','T/s','P/s','E/s'];radix=1024;}
 else if(['GHz','THz','PHz','EHz'].includes(unit))units=['GHz','THz','PHz','EHz'];
 else if(['GB','TB','PB','EB'].includes(unit)){units=['GB','TB','PB','EB'];radix=1024;}
 else if(['W','kW','MW','GW'].includes(unit))units=['W','kW','MW','GW'];
 let index=units?.indexOf(unit)??-1;if(index>=0){while(Math.abs(number)>=radix&&index<units.length-1){number/=radix;index++;}unit=units[index];}
 const decimals=Math.abs(number)>=100?0:Math.max(0,Math.min(3,sample?.decimals??0));if(!stale)text=number.toFixed(decimals);
 const arrow=label==='↑'||label==='↓',single=!arrow&&label.length===1,long=/^[A-Za-z]{4,}$/.test(label),labelEnd=arrow?7:single?13:long?36:24;
 const start=arrow?17:single?17:long?40:31,unitStart=unit?(unit.length<=2&&long?69:unit==='%'?61:60):77,end=unit?unitStart-4:77,preferred=unit?end:73,available=budget?Math.max(0,budget(unit)):end-start;
 if(taskbarNumberWidth(text)>available+.001&&!stale){for(let p=decimals-1;p>=0;p--){const candidate=number.toFixed(p);if(taskbarNumberWidth(candidate)<=available+.001){text=candidate;break;}}
 if(taskbarNumberWidth(text)>available+.001){for(const precision of [1,0]){const candidate=number.toExponential(precision).replace(/\.0e/,'e').replace('e+','e').replace(/e(-?)0+(\d)/,'e$1$2');if(taskbarNumberWidth(candidate)<=available+.001){text=candidate;break;}}}
 if(taskbarNumberWidth(text)>available+.001)text='…';}
 return {text,unit,start,end,unitStart,labelEnd,x:Math.max(start,preferred-taskbarNumberWidth(text))};
}
