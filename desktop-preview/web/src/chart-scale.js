const key='asterhub-chart-axis',listeners=new Set();
export function getChartScale(){try{return localStorage.getItem(key)==='full'?'full':'auto'}catch{return 'auto'}}
export function setChartScale(mode){try{localStorage.setItem(key,mode)}catch{}for(const notify of listeners)notify()}
export function subscribeChartScale(notify){listeners.add(notify);return()=>listeners.delete(notify)}
export function percentageMaximum(values){const peak=Math.max(0,...values.filter(Number.isFinite));return Math.min(100,Math.max(10,Math.ceil(peak/10)*10))}
export function percentageTicks(maximum){const step=maximum<=40?10:20;const ticks=[0];for(let value=step;value<maximum;value+=step)ticks.push(value);ticks.push(maximum);return ticks}
