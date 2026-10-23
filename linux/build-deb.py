#!/usr/bin/env python3
"""Build a normal directory-based Ubuntu .deb, using system-maintained runtimes."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

root=Path(__file__).resolve().parent
version=(root/'VERSION').read_text(encoding='utf-8-sig').strip()
p=argparse.ArgumentParser();p.add_argument('--out',default=str(root.parent/'artifacts'));args=p.parse_args()
out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='aqserver-deb-') as temp:
 stage=Path(temp)
 for folder in ['DEBIAN','usr/bin','usr/lib/aq-server','usr/share/aq-server','usr/share/applications','usr/share/icons/hicolor/256x256/apps','usr/share/doc/aq-server','usr/lib/systemd/user']:
  (stage/folder).mkdir(parents=True,exist_ok=True)
 shutil.copytree(root/'aqserver',stage/'usr/lib/aq-server/aqserver',ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
 shutil.copytree(root/'web',stage/'usr/share/aq-server/web')
 (stage/'usr/lib/aq-server/web').symlink_to('/usr/share/aq-server/web')
 for source,target in [('aq-server','usr/bin/aq-server'),('aq-server.desktop','usr/share/applications/aq-server.desktop'),('aq-server.service','usr/lib/systemd/user/aq-server.service')]:
  (stage/target).write_text((root/'packaging'/source).read_text(encoding='utf-8-sig').replace('\r\n','\n'),encoding='utf-8')
 shutil.copyfile(root/'packaging/aq-server.png',stage/'usr/share/icons/hicolor/256x256/apps/aq-server.png')
 for source in ['README.md','THIRD-PARTY.txt']:
  shutil.copyfile(root/source,stage/'usr/share/doc/aq-server'/source)
 control=f'''Package: aq-server
Version: {version}
Section: utils
Priority: optional
Architecture: all
Maintainer: AQ Server <noreply@localhost>
Depends: python3 (>= 3.10), python3-aiohttp (>= 3.8), python3-paramiko (>= 2.9), python3-gi, gir1.2-gtk-3.0, gir1.2-webkit2-4.1 | gir1.2-webkit2-4.0, fonts-wqy-zenhei
Description: Local and SSH server monitoring workbench
 CPU, NVIDIA GPU, memory, persistent history, embedded terminal and file transfers.
 The local workbench binds only to loopback and requires a per-user access key.
'''
 (stage/'DEBIAN/control').write_text(control)
 for path in stage.rglob('*'):
  if not path.is_symlink():path.chmod(0o755 if path.is_dir() else 0o644)
 (stage/'usr/bin/aq-server').chmod(0o755)
 package=out/f'AQServer-{version}-ubuntu-all.deb'
 subprocess.run(['dpkg-deb','--root-owner-group','--build',str(stage),str(package)],check=True)
 digest=hashlib.sha256(package.read_bytes()).hexdigest()
 metadata={'version':version,'platform':'ubuntu','package':package.name,'sha256':digest,'bytes':package.stat().st_size,
  'testedSystems':['Ubuntu 22.04 amd64'],'architecture':'all','minimumPython':'3.10',
  'installCommand':f'sudo apt install ./{package.name}','startCommand':'aq-server --desktop','serviceCommand':'systemctl --user enable --now aq-server'}
 (out/f'ubuntu-{version}.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(metadata,ensure_ascii=False,indent=2))
