// Preserve filesystem semantics: a backslash is a valid POSIX filename character.
export function pathCrumbs(path=''){
 if(!path)return [];
 const windows=/^[A-Za-z]:[\\/]/.test(path)||/^\\\\/.test(path),sep=windows?'\\':'/';
 let root='',rest=path;
 if(windows&&/^\\\\/.test(path)){const parts=path.slice(2).split(/[\\/]+/);root='\\\\'+parts.shift()+'\\'+(parts.shift()||'');rest=parts.join('\\');}
 else if(windows){root=path.slice(0,2)+'\\';rest=path.slice(3);}
 else if(path.startsWith('/')){root='/';rest=path.slice(1);}
 const result=root?[{name:root,path:root}]:[];let current=root;
 for(const name of rest.split(windows?/[\\/]+/:/\//).filter(Boolean)){current=current?(current.endsWith(sep)?current:current+sep)+name:name;result.push({name,path:current});}
 return result;
}

// Record only successful directory loads; refreshes do not generate history.
export function visitDirectory(history,path,travelTo=null){
 if(history?.entries[travelTo]===path)return {...history,cursor:travelTo};
 if(history?.entries[history.cursor]===path)return history;
 const entries=[...(history?.entries.slice(0,history.cursor+1)||[]),path].slice(-100);
 return {entries,cursor:entries.length-1};
}
