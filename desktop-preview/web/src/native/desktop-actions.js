import {native} from './bridge';
export async function writeClipboard(text){if(native.available)return native.request('clipboard.write',{text});return navigator.clipboard.writeText(text)}
export async function readClipboard(){if(native.available)return native.request('clipboard.read');return navigator.clipboard.readText()}
export async function openExternal(url){if(native.available)return native.request('app.openExternal',{url});window.open(url,'_blank','noopener,noreferrer')}
