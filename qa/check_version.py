from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
v=(root/'VERSION').read_text(encoding='utf-8').strip()
u=(root/'linux/VERSION').read_text(encoding='utf-8').strip()
for value in [v,u]:
 if not re.fullmatch(r'\d+\.\d+\.\d+',value):raise SystemExit('VERSION must be X.Y.Z')
checks={
 'installer/AQServer.iss':['#define AppVersion '+chr(34)+v+chr(34)],
 'deployment/deploy.sh':['AQServer-Setup-'+v+'.exe'],
 'linux/aqserver/storage.py':["VERSION = '"+u+"'"],
 'linux/README.md':['AQServer-'+u+'-ubuntu-all.deb'],
 'desktop/Properties/AssemblyInfo.cs':['AssemblyVersion("'+v+'.0")','AssemblyFileVersion("'+v+'.0")'],
 'desktop/src/AQServer.Desktop/Monitoring/MonitorWindow.cs':['AQ Server v'+v],
 'website/src/App.tsx':["from './release-versions.json'",'windowsVersion','ubuntuVersion'],
}
for file,needles in checks.items():
 text=(root/file).read_text(encoding='utf-8-sig')
 for needle in needles:
  if needle not in text:raise SystemExit('Version mismatch: '+file+' missing '+needle)
for file in ['website/package.json','website/package-lock.json']:
 data=json.loads((root/file).read_text(encoding='utf-8-sig'))
 if data.get('version')!=v:raise SystemExit('Version mismatch: '+file)
versions=json.loads((root/'website/src/release-versions.json').read_text(encoding='utf-8'))
if versions.get('windows')!=v or versions.get('ubuntu')!=u:raise SystemExit('Website platform versions mismatch')
from datetime import date
for platform in ['windows','ubuntu']:date.fromisoformat(versions[platform+'Date'])
print('PUBLIC_VERSIONS_CONSISTENT=windows:'+v+' ubuntu:'+u)
