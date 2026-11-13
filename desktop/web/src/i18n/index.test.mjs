import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
const dictionary=JSON.parse(fs.readFileSync(new URL('./en.json',import.meta.url),'utf8'));
function runtime(saved='zh-CN'){
 const storage=new Map([['asterhub-language',saved]]),document={documentElement:{dataset:{}}};
 const source=fs.readFileSync(new URL('./index.js',import.meta.url),'utf8').replace(/import[^;]+;/g,'').replace(/export /g,'');
 const context=vm.createContext({english:dictionary,document,localStorage:{getItem:k=>storage.get(k),setItem:(k,v)=>storage.set(k,v)},useSyncExternalStore:()=>{}});
 vm.runInContext(source+';globalThis.api={t,tr,setLanguage,getLanguage,locale};',context);return {api:context.api,document,storage};
}
test('language switching is immediate and persisted without reload',()=>{
 const {api,document,storage}=runtime();assert.equal(api.t('内存'),'内存');api.setLanguage('en');assert.equal(api.t('内存'),'Memory');assert.equal(document.documentElement.lang,'en');assert.equal(storage.get('asterhub-language'),'en');api.setLanguage('zh-CN');assert.equal(api.t('内存'),'内存');assert.equal(api.locale(),'zh-CN');
});
test('fixed message parameters preserve user-controlled names and paths',()=>{
 const {api}=runtime('en');assert.equal(api.t('选择 {0}',['内存']),'Select 内存');assert.equal(api.tr('选择 训练集群'),'Select 训练集群');assert.equal(api.tr('选择 C:\\内存\\训练集群'),'Select C:\\内存\\训练集群');assert.equal(api.tr('峰值 19:25 · 48.0%'),'Peak 19:25 · 48.0%');assert.equal(api.t('  内存  '),'  Memory  ');
});
test('file targets and saved device names bypass the UI dictionary',()=>{
 const dir=new URL('../native/',import.meta.url);
 for(const name of ['FilesPage.jsx','Devices.jsx','NativeApp.jsx','PeripheralPages.jsx','ResourceWidgets.jsx','StorageTools.jsx']){
  const source=fs.readFileSync(new URL(name,dir),'utf8');
  assert.doesNotMatch(source,/tr\((?:plan\.(?:sourceLabel|targetLabel|targetPath)|plan\.names\.(?:slice|join)|job\.names\.(?:slice|join)|dialog\.plan\.names|n\.Name|d\.Name|current\?\.name|activeDevice\?\.name)/,name);
 }
});
test('catalog tokens agree and nontext values pass through unchanged',()=>{
 for(const [key,value] of Object.entries(dictionary)){
  assert.equal(typeof value,'string',key);const tokens=s=>[...new Set(s.match(/\{\d+\}/g)||[])].sort();assert.deepEqual(tokens(value),tokens(key),key);
 }
 const {api}=runtime('en');const node={type:'span'};assert.equal(api.tr(node),node);assert.equal(api.tr(0),0);
});

test('shared value renderers preserve external labels',()=>{
 const dir=new URL('../',import.meta.url);
 for(const [name,pattern] of [['themed-select.jsx',/tr\((?:o\.label|options\[current\]\?\.label)/],['ProcessPath.jsx',/tr\(path\)/],['gpu-inspector.jsx',/tr\(command\)/],['native/PeripheralPages.jsx',/tr\((?:v\.Label|v\?\?)/],['native/GpuProcessSummary.jsx',/tr\((?:p\.user|command)\)/]])assert.doesNotMatch(fs.readFileSync(new URL(name,dir),'utf8'),pattern,name);
});
