import {ThemedSelect} from "./themed-select";
import React from 'react';
import {Check} from 'lucide-react';
const styles=[{id:'classic',name:'经典卡片',description:'大数字与双进度条，温度、风扇和功耗一目了然。'},{id:'trend',name:'趋势卡片',description:'紧凑指标与迷你曲线，同时观察多张显卡的变化。'}];
export function DisplayStyleSelect({value,onChange}){
 return <ThemedSelect className="display-style-select" aria-label="GPU 显示风格" value={value} onChange={e=>onChange(e.target.value)}>{styles.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</ThemedSelect>
}
export function DisplayStyleChoices({value,onChange}){
 return <><div className="display-style-options" role="radiogroup" aria-label="显示风格">{styles.map(s=><button type="button" role="radio" aria-checked={value===s.id} aria-label={s.name} key={s.id} className={`display-style-option ${value===s.id?'selected':''}`} onClick={()=>onChange(s.id)}>
  <div className={`style-preview ${s.id}`} aria-hidden="true"><div className="style-preview-heading"><span>GPU 0</span><i/></div><div className="style-preview-reading"><strong>78<small>%</small></strong>{s.id==='trend'?<svg viewBox="0 0 90 32"><path d="M1 29 C8 29 9 12 16 16 S25 25 32 18 S40 29 48 10 S58 27 65 14 S76 18 89 2"/></svg>:<span>利用率</span>}</div><div className="style-preview-meter"><i/></div>{s.id==='classic'?<><div className="style-preview-caption"><span>显存</span><span>21.03 / 48 GiB</span></div><div className="style-preview-meter memory"><i/></div></>:<div className="style-preview-caption"><span>21.03 / 48 GiB</span><span>68°C</span></div>}</div>
  <div className="style-option-title"><strong>{s.name}</strong>{value===s.id&&<Check size={16}/>}</div><p>{s.description}</p>
 </button>)}</div><p className="display-style-note">同步应用于概览和 GPU 卡片；配色主题、界面密度和动态效果可独立设置。</p></>
}
