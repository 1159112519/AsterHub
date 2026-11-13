export const taskbarMetrics=[['cpu','CPU 利用率','CPU','%'],['memory','内存利用率','内存','%'],['storage','存储占用','存储','%'],['gpu','GPU 利用率','GPU','%'],['down','下载速度','↓','K/s'],['up','上传速度','↑','K/s'],['cpuFrequency','CPU 频率','CPU','GHz'],['gpuFrequency','GPU 频率','GPU','GHz'],['gpuMemory','显存利用率','显存','%'],['gpuTemperature','GPU 温度','GPU','°C'],['gpuPower','GPU 功耗','功耗','W'],['gpuFan','GPU 风扇','风扇','%'],['storageRead','磁盘读取','R','M/s'],['storageWrite','磁盘写入','W','M/s'],['memoryUsed','已用内存','已用','GB'],['cpuTemperature','CPU 温度','CPU','°C'],['ping','延迟','延迟','ms'],['loss','丢包率','丢包','%'],['io','磁盘负载','负载','%']];
export const taskbarRefreshIntervals=[250,500,1000,2000,3000,5000,10000];
export const taskbarRefreshMs=value=>taskbarRefreshIntervals.includes(value)?value:[30000,60000].includes(value)?10000:250;
export const taskbarCellWidth=(_columns,wide=false)=>wide?118:88;
export const taskbarFitStages=(columns,wide)=>[
 {columns,graphs:true,width:18+columns*taskbarCellWidth(columns,wide)},
 ...(wide?[{columns,graphs:false,width:18+columns*taskbarCellWidth(columns)}]:[]),
 ...Array.from({length:columns-1},(_,i)=>columns-1-i).map(n=>({columns:n,graphs:false,width:18+n*taskbarCellWidth(n)})),
];
export const taskbarAutoPageIntervals=[30,60,120,300,600];
export const taskbarDefaults=()=>({enabled:true,motion:true,deviceId:'local',slots:4,page:0,background:false,backgroundStyle:'mist',autoPage:false,clickDetails:false,intervalSeconds:taskbarAutoPageIntervals[0],refreshMs:taskbarRefreshIntervals[0],numberMotion:'direct',pages:[['cpu','memory','storage','gpu'],['down','up','cpuFrequency','gpuFrequency'],['gpuMemory','gpuTemperature','gpuPower','gpuFan']]});
