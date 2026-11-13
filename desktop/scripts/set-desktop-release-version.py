import argparse,re
from pathlib import Path
p=argparse.ArgumentParser(description='Advance the desktop workbench version');p.add_argument('version');args=p.parse_args()
root=Path(__file__).resolve().parents[1];anchor=root/'VERSION';old=anchor.read_text().strip();new=args.version
pattern=r'[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?'
if not all(re.fullmatch(pattern,v) for v in (old,new)) or tuple(map(int,new.split('.'))) <= tuple(map(int,old.split('.'))):p.error('Expected an increasing Windows release version')
changes={}
for name,count in [('host/AssemblyInfo.cs',3),('installer/notice.txt',1)]:
 f=root/name;s=f.read_text(encoding='utf-8');assert s.count(old)==count,name;changes[f]=s.replace(old,new)
for f,s in changes.items():f.write_text(s,encoding='utf-8')
(root/'backend/Properties/AssemblyInfo.cs').write_text((root/'backend/Properties/AssemblyInfo.cs').read_text(encoding='utf-8').replace(old,new),encoding='utf-8')
import json
component_path=root/'app/components.json';components=json.loads(component_path.read_text(encoding='utf-8'));components['version']=new
for component in components['components']:component['version']=new
component_path.write_text(json.dumps(components,indent=2)+'\n',encoding='utf-8')
(root.parent/'VERSION').write_text(new+'\n',encoding='ascii')
anchor.write_text(new+'\n',encoding='ascii');print('DESKTOP_VERSION='+new)
