import {test} from 'node:test';import assert from 'node:assert/strict';import {recentUpdates} from './generate-recent-updates.mjs';
test('recent updates are generated from the newest three dated changelog sections',()=>{
 const source='# Changelog\n\n## 0.3.0-preview.16 · 2026-11-03\n\n- 新增文件工具。\n- 改善返回过渡。\n- 修复记录遗漏。\n\n提示：旧版本不变。\n\n## 0.3.0-preview.15 · 2026-11-03\n- 统一布局。\n\n## 0.3.0-preview.14 · 2026-11-02\n- 支持后台扫描。\n\n## 0.3.0-preview.13 · 2026-11-02\n- 不应进入列表。\n';
 const r=recentUpdates(source,'0.3.0-preview.16');assert.equal(r.length,3);assert.deepEqual(r[0],{version:'0.3.0-preview.16',date:'2026-11-03',added:['新增文件工具。'],improved:['改善返回过渡。'],fixed:['修复记录遗漏。']});assert.equal(r[2].version,'0.3.0-preview.14');assert(!JSON.stringify(r).includes('提示'));
});
test('a version without its own changelog blocks packaging rather than shipping stale notes',()=>{assert.throws(()=>recentUpdates('## 0.3.0-preview.15 · 2026-11-03\n- 改善布局。','0.3.0-preview.16'),/changelog/i);assert.throws(()=>recentUpdates('# Empty','0.3.0-preview.16'),/changelog/i)});
