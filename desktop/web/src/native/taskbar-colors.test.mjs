import test from 'node:test';
import assert from 'node:assert/strict';
import {hex,fromHsv,toHsv,withColor,metricCss,tabs,moveTab} from './taskbar-colors.mjs';
test('valid opaque hex and HSV round trip',()=>{
 assert.equal(hex(' a1B2c3 '),'#A1B2C3');assert.equal(hex('#12zz56'),null);assert.equal(hex('#ff008066'),null);
 for(const v of ['#000000','#FFFFFF','#D34679','#45B03E','#477ADB'])assert.equal(fromHsv(...toHsv(v)),v);
});
test('draft map never mutates other metrics or other parts and default removes override',()=>{
 const first=withColor({},'cpu','label','#abcdef');const second=withColor(first,'down','reading','#D34679');
 assert.deepEqual(first,{cpu:{label:'#ABCDEF'}});assert.strictEqual(withColor(second,'cpu','graph','bad'),second);
 assert.deepEqual(metricCss(second,'cpu'),{'--tb-custom-name':'#ABCDEF','--tb-custom-reading':undefined,'--tb-custom-graph':undefined});
 assert.deepEqual(withColor(second,'cpu','label',null),{down:{reading:'#D34679'}});
});
test('keyboard tab order wraps and Home/End follow the three labels',()=>{
 assert.deepEqual(tabs.map(x=>x[1]),['组件','颜色','位置']);assert.equal(moveTab('components','ArrowLeft'),'tuning');assert.equal(moveTab('tuning','ArrowRight'),'components');assert.equal(moveTab('components','ArrowRight'),'colors');assert.equal(moveTab('colors','End'),'tuning');
});
