import test from 'node:test';import assert from 'node:assert/strict';
import {taskbarMetrics,taskbarDefaults} from './taskbar-layout.js';
import {groups,migrateLayout} from './taskbar-composer.js';
test('default installation remains enabled and legacy defaults migrate in order',()=>{
 const options=taskbarDefaults(),layout=migrateLayout(options);assert.equal(options.enabled,true);assert.equal(layout.pages.length,3);
 assert.deepEqual(layout.pages.map(p=>p.slots.map(s=>s.module?.metricId??null)),options.pages);
});
test('each catalogue metric appears in exactly one category',()=>{
 const ids=groups.flatMap(([,items])=>items);assert.equal(ids.length,new Set(ids).size);assert.deepEqual([...ids].sort(),taskbarMetrics.map(([id])=>id).sort());
});
