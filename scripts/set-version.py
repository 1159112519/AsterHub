#!/usr/bin/env python3
"""Update public version locations together; leaves historical changelogs intact."""
import argparse,json,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('version');args=p.parse_args()
if not re.fullmatch(r'\d+\.\d+\.\d+',args.version):p.error('version must be X.Y.Z')
r=Path(__file__).resolve().parents[1];old=(r/'VERSION').read_text(encoding='utf-8').strip();new=args.version
if tuple(map(int,new.split('.')))<=tuple(map(int,old.split('.'))):p.error('version must increase')
files=['desktop/AssemblyInfo.cs','desktop/Main.cs','desktop/Launcher.cs','website/src/App.tsx','scripts/verify-packaged.ps1','scripts/verify-installer.ps1','deployment/deploy.sh']
changes={}
for name in files:
 f=r/name;raw=f.read_bytes();bom=raw.startswith(b'\xef\xbb\xbf');text=raw.decode('utf-8-sig')
 if old not in text:raise SystemExit('Expected previous version missing in '+name)
 changes[f]=text.replace(old,new).encode('utf-8-sig' if bom else 'utf-8')
for name in ['website/package.json','website/package-lock.json']:
 f=r/name;data=json.loads(f.read_text(encoding='utf-8'));data['version']=new
 if name.endswith('lock.json'):data['packages']['']['version']=new
 changes[f]=(json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode()
changes[r/'VERSION']=(new+'\n').encode()
for f,data in changes.items():f.write_bytes(data)
print('VERSION_UPDATED='+new+'; add CHANGELOG entry, rebuild, verify, sign and deploy before publishing.')
