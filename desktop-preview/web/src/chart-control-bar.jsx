import React,{useSyncExternalStore} from 'react';
import {ThemedSelect} from './themed-select';
import {getChartScale,setChartScale,subscribeChartScale} from './chart-scale';
import './chart-control-bar.css';

// The scale store is shared with Chart so switching this row updates the axis
// without replacing a curve or restarting the live timeline animation.
export function ChartControlBar({scalable=true,rangeMinutes=15,onRangeChange,timeLabel='图表时间范围'}){
 const axisMode=useSyncExternalStore(subscribeChartScale,getChartScale);
 return <div className="chart-control-bar" role="group" aria-label="趋势图显示范围" onClick={e=>e.stopPropagation()} onPointerMove={e=>e.stopPropagation()}>
  {scalable&&<ThemedSelect className="chart-scale-select" aria-label="曲线纵轴范围" value={axisMode} onChange={e=>setChartScale(e.target.value)}><option value="auto">自动范围</option><option value="full">固定 0–100%</option></ThemedSelect>}
  <ThemedSelect className="chart-time-select" aria-label={timeLabel} value={rangeMinutes} onChange={e=>onRangeChange(Number(e.target.value))}><option value={15}>最近 15 分钟</option><option value={60}>最近 1 小时</option><option value={120}>最近 2 小时</option></ThemedSelect>
 </div>
}
