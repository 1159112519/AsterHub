import React from 'react';
import './process-path.css';

// Isolate the LTR path inside an RTL clipping box: ellipsis removes its beginning.
export function ProcessPath({process,as:Tag='strong'}){
 const path=String(process.path??'').trim()||String(process.name??'').trim()||'—';
 return <Tag className="process-executable-path" title={path} dir="rtl"><bdi dir="ltr">{path}</bdi></Tag>;
}
