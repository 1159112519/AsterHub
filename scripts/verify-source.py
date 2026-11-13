from pathlib import Path
import base64,json,re
root=Path(__file__).resolve().parents[1]
version=(root/'VERSION').read_text().strip()
assert re.fullmatch(r"\d+\.\d+\.\d+(?:\.\d+)?",version),version
assert (root/'desktop/VERSION').read_text().strip()==version
for part in ['host','backend/Properties']:
 text=(root/'desktop'/part/'AssemblyInfo.cs').read_text(encoding='utf-8')
 for key in ['AssemblyVersion','AssemblyFileVersion']:
  assert f'{key}("{version}")' in text,(part,key)
components=json.loads((root/'desktop/app/components.json').read_text())
assert components['version']==version
assert all(c['version']==version and 'AQServer' not in c['name'] for c in components['components'])
channel=json.loads((root/'website/public/releases/windows-preview.json').read_text())
release=json.loads(base64.b64decode(channel['payload']))
assert release['appId']=='AsterHubPreview'
assert release['version']==version,(release['version'],version)
for name in ['desktop-preview','linux','desktop/AQServer.sln','installer/AQServer.iss']:
 assert not (root/name).exists(),name
for name in ['desktop/build.ps1','desktop/host/App.config','desktop/web/package-lock.json','desktop/scripts/package-app.mjs','desktop/installer/AsterHub.iss','agent/linux/asterhub_agent.py']:
 assert (root/name).is_file(),name
settings=(root/'desktop/backend/src/AsterHub.Storage/SettingsStore.cs').read_text(encoding='utf-8')
assert 'return LocalServer();' in settings
print('PASS source structure, version '+version+', compatibility channel and local-only default')
