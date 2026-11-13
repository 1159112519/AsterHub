export const storageSizeUnits={MiB:2**20,GiB:2**30};
export const storageSizePresets=[['全部大小',0],[100,'MiB'],[500,'MiB'],[1,'GiB'],[3,'GiB'],[5,'GiB'],[10,'GiB']].map(([amount,unit])=>unit?{label:`大于 ${amount} ${unit}`,bytes:amount*storageSizeUnits[unit]}:{label:amount,bytes:0});

export function parseStorageSize(raw,unit){
 const text=String(raw).trim();
 if(!/^(?:[0-9]{1,12}(?:\.[0-9]{1,9})?|\.[0-9]{1,9})$/.test(text)||!Object.hasOwn(storageSizeUnits,unit))return null;
 const [whole,fraction='']=text.split('.'),denominator=10n**BigInt(fraction.length);
 const bytes=(BigInt(whole||'0')*denominator+BigInt(fraction||'0'))*BigInt(storageSizeUnits[unit])/denominator;
 if(bytes<1n||bytes>=BigInt(Number.MAX_SAFE_INTEGER))return null;
 const amount=(whole.replace(/^0+(?=[0-9])/,'')||'0')+(fraction.replace(/0+$/,'')?'.'+fraction.replace(/0+$/,''):'');
 return {amount,unit,bytes:Number(bytes)};
}
// The storage bridge accepts an inclusive minimum. The UI says strictly greater than.
export function storageQueryMinimum(threshold){return threshold>0?threshold+1:0}
