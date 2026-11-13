import test from 'node:test';import assert from 'node:assert/strict';
import {terminalAnsiKeys,terminalThemes,terminalTheme} from './terminal-themes.js';
import {contrast} from '../appearance/resolve-appearance.js';
test('every terminal scheme includes foreground, cursor, selection and all 16 ANSI colors',()=>{for(const [id,t] of Object.entries(terminalThemes)){for(const key of [...terminalAnsiKeys,'foreground','background','cursor','cursorAccent','selectionBackground','selectionForeground'])assert.match(t[key],/^#[0-9a-f]{6}$/i,id+'/'+key);assert(contrast(t.foreground,t.background)>=7,id+' normal text contrast');assert(contrast(t.selectionForeground,t.selectionBackground)>=4.5,id+' selection contrast');assert.equal(t.cursorAccent,t.background)}});
test('presets have distinct text palettes and all light ANSI bright colors are readable',()=>{assert.notEqual(terminalThemes.slate.red,terminalThemes.forest.red);assert.notEqual(terminalThemes.ocean.cyan,terminalThemes.graphite.cyan);for(const key of terminalAnsiKeys)assert(contrast(terminalThemes.light[key],terminalThemes.light.background)>=3,'light '+key)});
test('every numbered skin and mode resolves a complete theme without mutating presets',()=>{for(const theme of ['graphite','slate','forest','ocean','light','01','02','03','04','05','06','07','08','09'])for(const appearanceMode of ['dark','light']){const t=terminalTheme({theme,appearanceMode,terminalTheme:'follow'});assert.equal(Object.keys(t).length,22);assert(contrast(t.foreground,t.background)>=4.5,theme+'/'+appearanceMode);assert(t.selectionForeground);assert(t.cursorAccent)}const copy=terminalTheme({terminalTheme:'ocean'});copy.red='bad';assert.notEqual(terminalThemes.ocean.red,'bad')});

test('custom foreground and ANSI colors override the selected scheme without changing other entries',()=>{const t=terminalTheme({terminalTheme:'ocean',terminalColorOverrides:{foreground:'#b4d5ec',red:'#ed7084',background:'invalid',cursor:'javascript:bad'}});assert.equal(t.foreground,'#b4d5ec');assert.equal(t.selectionForeground,'#b4d5ec');assert.equal(t.red,'#ed7084');assert.equal(t.background,terminalThemes.ocean.background);assert.equal(t.cursor,terminalThemes.ocean.cursor);assert.notEqual(terminalThemes.ocean.red,t.red);assert.deepEqual(terminalTheme({terminalTheme:'ocean',terminalColorOverrides:{}}),terminalThemes.ocean)});
import * as schemes from './terminal-themes.js';
test('scheme catalogue can search names and shows each preset once',()=>{assert(Array.isArray(schemes.terminalSchemes));assert.equal(new Set(schemes.terminalSchemes.map(s=>s.id)).size,schemes.terminalSchemes.length);for(const id of ['follow',...Object.keys(terminalThemes)])assert(schemes.terminalSchemes.some(s=>s.id===id),id);assert.deepEqual(schemes.filterTerminalSchemes('tabby default light').map(s=>s.id),['tabby-default-light']);assert(schemes.filterTerminalSchemes('深海').some(s=>s.id==='ocean'));assert.equal(schemes.filterTerminalSchemes('no such scheme').length,0)});
test('Tabby source defaults retain their primary-source ANSI values',()=>{const dark=terminalTheme({terminalTheme:'tabby-default'}),light=terminalTheme({terminalTheme:'tabby-default-light',terminalAllowLightInDark:true});assert.equal(dark.background,'#171717');assert.equal(dark.foreground,'#cacaca');assert.equal(dark.cursor,'#bbbbbb');assert.deepEqual(terminalAnsiKeys.map(k=>dark[k]),['#000000','#ff615a','#b1e969','#ebd99c','#5da9f6','#e86aff','#82fff7','#dedacf','#313131','#f58c80','#ddf88f','#eee5b2','#a5c7ff','#ddaaff','#b7fff9','#ffffff']);assert.equal(light.foreground,'#4d4d4c');assert.equal(light.background,'#ffffff');assert.deepEqual(terminalAnsiKeys.map(k=>light[k]),['#000000','#c82829','#718c00','#eab700','#4271ae','#8959a8','#3e999f','#ffffff','#000000','#c82829','#718c00','#eab700','#4271ae','#8959a8','#3e999f','#ffffff'])});
test('switch migrates active legacy overrides, loads the target defaults, and restores independent edits on return',()=>{
 const current={terminalTheme:'ocean',terminalColorOverrides:{foreground:'#b4d5ec',red:'#ed7084'},terminalFont:'Cascadia Mono'};
 const next=schemes.selectTerminalScheme(current,'tabby-default');
 assert.equal(next.terminalTheme,'tabby-default');assert.deepEqual(next.terminalColorOverrides,{});assert.deepEqual(next.terminalSchemeOverrides.ocean,current.terminalColorOverrides);assert.equal(terminalTheme(next).red,terminalThemes['tabby-default'].red);
 const edited={...next,terminalColorOverrides:{blue:'#123456'}};const restored=schemes.selectTerminalScheme(edited,'ocean');
 assert.deepEqual(restored.terminalColorOverrides,current.terminalColorOverrides);assert.deepEqual(restored.terminalSchemeOverrides['tabby-default'],{blue:'#123456'});assert.equal(restored.terminalFont,current.terminalFont);
 assert.deepEqual(schemes.selectTerminalScheme(restored,'tabby-default').terminalColorOverrides,{blue:'#123456'});assert.equal(current.terminalSchemeOverrides,undefined);assert.deepEqual(schemes.selectTerminalScheme(current,'missing'),current);assert.deepEqual(terminalTheme(JSON.parse(JSON.stringify(restored))),terminalTheme(current));
});
test('clicking the active scheme retains edits and a catalogue card predicts the colors after switching',()=>{
 const settings={terminalTheme:'ocean',terminalColorOverrides:{background:'#123456',red:'#abcdef'},terminalSchemeOverrides:{ocean:{red:'#999999'},forest:{foreground:'#bbccdd',red:'#fedcba'}}};
 assert.deepEqual(schemes.selectTerminalScheme(settings,'ocean'),settings);assert.deepEqual(schemes.terminalSchemeTheme('ocean',settings),terminalTheme(settings));
 for(const id of ['ocean','forest','tabby-default','follow'])assert.deepEqual(schemes.terminalSchemeTheme(id,settings),terminalTheme(schemes.selectTerminalScheme(settings,id)),id);
 assert.equal(schemes.terminalSchemeTheme('forest',settings).red,'#fedcba');assert.equal(schemes.terminalSchemeTheme('tabby-default',settings).background,'#171717');
});
test('per-scheme migration and preview filter malformed colors and unsupported xterm keys',()=>{
 const settings={terminalTheme:'ocean',terminalColorOverrides:{red:'#abcdef',cursor:'javascript:bad',fontSize:'#ffffff'},terminalSchemeOverrides:{forest:{green:'#123456',background:'invalid',unknown:'#abcdef'}}};
 const next=schemes.selectTerminalScheme(settings,'forest');assert.deepEqual(next.terminalColorOverrides,{green:'#123456'});assert.deepEqual(next.terminalSchemeOverrides.ocean,{red:'#abcdef'});assert.equal(terminalTheme(next).background,terminalThemes.forest.background);assert.equal(schemes.terminalSchemeTheme('forest',settings).background,terminalThemes.forest.background);
 for(const invalid of [null,[],42,'invalid']){const value=schemes.selectTerminalScheme({terminalTheme:'ocean',terminalColorOverrides:invalid,terminalSchemeOverrides:invalid},'forest');assert.deepEqual(value.terminalColorOverrides,{});assert.deepEqual(value.terminalSchemeOverrides.ocean,{})}
});
test('reset affects only the current scheme and follow overrides survive round trips',()=>{
 const initial={theme:'01',appearanceMode:'light',terminalColorOverrides:{foreground:'#234567'}};const forest=schemes.selectTerminalScheme(initial,'forest');assert.deepEqual(forest.terminalSchemeOverrides.follow,{foreground:'#234567'});
 const changed={...forest,terminalColorOverrides:{blue:'#abcdef'}};const back=schemes.selectTerminalScheme(changed,'follow');assert.equal(terminalTheme(back).foreground,'#234567');
 const reset={...back,terminalColorOverrides:{}};assert.equal(schemes.terminalSchemeTheme('follow',reset).foreground,terminalTheme({theme:'01',appearanceMode:'light',terminalTheme:'follow'}).foreground);const forestAgain=schemes.selectTerminalScheme(reset,'forest');assert.deepEqual(forestAgain.terminalColorOverrides,{blue:'#abcdef'});assert.deepEqual(schemes.selectTerminalScheme(forestAgain,'follow').terminalColorOverrides,{});
});

test('all terminal palettes can be selected directly regardless of interface mode or obsolete light lock',()=>{
 const saved={theme:'graphite',appearanceMode:'dark',terminalTheme:'light',terminalColorOverrides:{foreground:'#224433'},terminalAllowLightInDark:false};
 assert.equal(schemes.terminalLightAllowed(saved),true);assert.equal(terminalTheme(saved).foreground,'#224433');
 for(const scheme of schemes.terminalSchemes){const next=schemes.selectTerminalScheme(saved,scheme.id);assert.deepEqual(terminalTheme(next),schemes.terminalSchemeTheme(scheme.id,saved),scheme.id);}
 for(const theme of [terminalTheme(saved),terminalTheme({terminalTheme:'tabby-default-light'})]){const chrome=schemes.terminalChrome(theme);assert(contrast(chrome['--terminal-fg'],chrome['--terminal-surface'])>=4.5);assert(contrast(chrome['--terminal-muted'],chrome['--terminal-surface'])>=4.5);}
});

test('60 new full palettes use unique surfaces and meaningful foreground/ANSI variation',()=>{
 const all=schemes.terminalSchemes.filter(x=>x.id.startsWith('palette-')).map(x=>terminalThemes[x.id]);assert.equal(all.length,60);assert.equal(new Set(all.map(t=>t.background)).size,60);assert(new Set(all.map(t=>t.foreground)).size>=36);
 for(const theme of all){assert(contrast(theme.foreground,theme.background)>=7);assert(contrast(theme.selectionForeground,theme.selectionBackground)>=4.5);assert(contrast(theme.cursor,theme.background)>=4.5);for(const key of schemes.terminalAnsiKeys.slice(1,7))assert(contrast(theme[key],theme.background)>=4.5);}
});
