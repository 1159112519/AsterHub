import React from 'react';
import {LockKeyhole,LockKeyholeOpen} from 'lucide-react';
import {Button} from '../components';
import {t,useLocale} from '../i18n/index.js';
export function AxisLock({locked,onClick,disabled=false,mode="axis"}){useLocale();return <Button tone="quiet" className="axis-lock" aria-pressed={locked} disabled={disabled} title={t(mode==='time'?(locked?'恢复原自定义区间':'将时间选择线锁定到区间最高点和最低点'):(locked?'解锁纵轴最高点和最低点':'锁定当前纵轴最高点和最低点'))} onClick={onClick}>{locked?<LockKeyhole size={14}/>:<LockKeyholeOpen size={14}/>}<span>{t(locked?'已锁定极值':'锁定极值')}</span></Button>}
