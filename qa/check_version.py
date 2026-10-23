from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
v=(root/'VERSION').read_text(encoding='utf-8').strip()
if not re.fullmatch(r'\d+\.\d+\.\d+',v):raise SystemExit('VERSION must be X.Y.Z')
checks={
 'installer/AQServer.iss':['#define AppVersion '+chr(34)+v+chr(34)],
 'deployment/deploy.sh':['AQServer-Setup-'+v+'.exe'],
 'linux/aqserver/storage.py':["VERSION = '"+v+"'"],
 'linux/README.md':['AQServer-'+v+'-ubuntu-all.deb'],
 'desktop/Properties/AssemblyInfo.cs':['AssemblyVersion("'+v+'.0")','AssemblyFileVersion("'+v+'.0")'],
 'desktop/src/AQServer.Desktop/Monitoring/MonitorWindow.cs':['AQ Server v'+v],
 'website/src/App.tsx':['AQServer-Setup-'+v+'.exe','v'+v],
}
for file,needles in checks.items():
 text=(root/file).read_text(encoding='utf-8-sig')
 for needle in needles:
  if needle not in text:raise SystemExit('Version mismatch: '+file+' missing '+needle)
for file in ['website/package.json','website/package-lock.json']:
 data=json.loads((root/file).read_text(encoding='utf-8-sig'))
 if data.get('version')!=v:raise SystemExit('Version mismatch: '+file)
if (root/'linux/VERSION').read_text(encoding='utf-8-sig').strip()!=v:raise SystemExit('Linux version mismatch')
print('PUBLIC_VERSION_CONSISTENT='+v)
