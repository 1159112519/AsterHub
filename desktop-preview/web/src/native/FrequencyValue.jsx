import React from 'react';
import {NumberValue} from '../components';
export function FrequencyValue({value}){return Number.isFinite(value)&&value>0?<NumberValue value={value>=1000?value/1000:value} decimals={value>=1000?2:0} suffix={value>=1000?' GHz':' MHz'}/>:<span>—</span>}
