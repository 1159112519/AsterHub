#!/usr/bin/env python3
"""Increase independently released platform versions after checking every anchor."""
import argparse, json, re
from datetime import date
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('version')
p.add_argument('--platform',choices=['windows','ubuntu','all'],default='windows')
p.add_argument('--date',default=date.today().isoformat(),help='Platform release date (YYYY-MM-DD)')
a=p.parse_args()
try:date.fromisoformat(a.date)
except ValueError:p.error('date must be YYYY-MM-DD')
if not re.fullmatch(r'\d+\.\d+\.\d+',a.version):p.error('version must be X.Y.Z')
r=Path(__file__).resolve().parents[1]
versions=json.loads((r/'website/src/release-versions.json').read_text(encoding='utf-8'))
changes={}
platforms=['windows','ubuntu'] if a.platform=='all' else [a.platform]
anchors={
 'windows':['desktop/Properties/AssemblyInfo.cs','desktop/src/AQServer.Desktop/Monitoring/MonitorWindow.cs','deployment/deploy.sh','installer/AQServer.iss'],
 'ubuntu':['linux/aqserver/storage.py','linux/README.md'],
}
for platform in platforms:
 version_file=r/('VERSION' if platform=='windows' else 'linux/VERSION')
 old=version_file.read_text(encoding='utf-8-sig').strip()
 if versions[platform]!=old:raise SystemExit('Website platform version mismatch: '+platform)
 if tuple(map(int,a.version.split('.')))<=tuple(map(int,old.split('.'))):p.error(platform+' version must increase')
 for name in anchors[platform]:
  f=r/name;raw=f.read_bytes();bom=raw.startswith(b'\xef\xbb\xbf');text=raw.decode('utf-8-sig')
  if old not in text:raise SystemExit('Expected previous version missing in '+name)
  changes[f]=text.replace(old,a.version).encode('utf-8-sig' if bom else 'utf-8')
 changes[version_file]=(a.version+'\n').encode()
 versions[platform]=a.version
 versions[platform+'Date']=a.date
if 'windows' in platforms:
 for name in ['website/package.json','website/package-lock.json']:
  f=r/name;data=json.loads(f.read_text(encoding='utf-8'));data['version']=a.version
  if name.endswith('lock.json'):data['packages']['']['version']=a.version
  changes[f]=(json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode()
changes[r/'website/src/release-versions.json']=(json.dumps(versions,indent=2)+'\n').encode()
for f,data in changes.items():f.write_bytes(data)
print('VERSION_UPDATED='+a.platform+':'+a.version+'; add CHANGELOG entry, rebuild, verify, sign and deploy before publishing.')
