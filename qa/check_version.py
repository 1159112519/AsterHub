from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
v=(root/'VERSION').read_text(encoding='utf-8').strip()
if not re.fullmatch(r'\d+\.\d+\.\d+',v):raise SystemExit('VERSION must be X.Y.Z')
checks={
 'desktop/AssemblyInfo.cs':['AssemblyVersion("'+v+'.0")','AssemblyFileVersion("'+v+'.0")'],
 'desktop/Main.cs':['AQ Server v'+v],
 'desktop/Launcher.cs':['v'+v+'-'],
 'website/src/App.tsx':['AQServer-Setup-'+v+'.exe','v'+v],
}
for file,needles in checks.items():
 text=(root/file).read_text(encoding='utf-8-sig')
 for needle in needles:
  if needle not in text:raise SystemExit('Version mismatch: '+file+' missing '+needle)
for file in ['website/package.json','website/package-lock.json']:
 data=json.loads((root/file).read_text(encoding='utf-8-sig'))
 if data.get('version')!=v:raise SystemExit('Version mismatch: '+file)
print('PUBLIC_VERSION_CONSISTENT='+v)
