"""Per-user configuration and durable timestamped snapshots."""
import json
import os
from pathlib import Path
import sqlite3
import secrets
import time

VERSION = '0.1.3'
DEFAULTS = {'theme':'dark','interval':2,'terminalFontSize':14,'terminalFontFamily':'monospace',
            'retentionDays':30,'historyOrder':['chart','gpus','processes'],
            'devices':[{'id':'local','name':'本机','type':'local','enabled':True},
                       {'id':'aq-server','name':'aq-server','type':'ssh','host':'117.50.162.34','port':22,'username':'baiyang','keyPath':'','enabled':False}]}


def data_roots(config=None, data=None):
    config = Path(config or os.environ.get('XDG_CONFIG_HOME', Path.home()/'.config'))
    data = Path(data or os.environ.get('XDG_DATA_HOME', Path.home()/'.local/share'))
    config, data = config/'aq-server', data/'aq-server'
    for path in (config, data):
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
        path.chmod(0o700)
    return config, data


class Store:
    def __init__(self, config=None, data=None):
        self.config_dir, self.data_dir = data_roots(config, data)
        self.settings_path = self.config_dir/'settings.json'
        self.db = self.data_dir/'history.sqlite3'
        self.access_key_path = self.config_dir/'access.key'
        if not self.access_key_path.exists():
            fd=os.open(str(self.access_key_path),os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
            with os.fdopen(fd,'w') as f:f.write(secrets.token_urlsafe(32))
        self.access_key_path.chmod(0o600)
        self.access_key=self.access_key_path.read_text().strip()
        if self.settings_path.exists():
            self.settings = json.loads(self.settings_path.read_text())
            for key, value in DEFAULTS.items(): self.settings.setdefault(key, value)
        else:
            self.settings = json.loads(json.dumps(DEFAULTS))
            self.save()
        with self.connection() as c:
            c.execute('PRAGMA journal_mode=WAL')
            c.execute('CREATE TABLE IF NOT EXISTS samples (id INTEGER PRIMARY KEY,device TEXT NOT NULL,ts REAL NOT NULL,cpu REAL NOT NULL,memory REAL NOT NULL,gpu REAL NOT NULL,payload TEXT NOT NULL)')
            c.execute('CREATE INDEX IF NOT EXISTS samples_device_ts ON samples(device,ts)')
        self.db.chmod(0o600)
        self.insert_count = 0

    def connection(self):
        return sqlite3.connect(str(self.db), timeout=10)

    def save(self):
        clean = json.loads(json.dumps(self.settings))
        for device in clean['devices']:
            for key in ('password','passphrase'): device.pop(key,None)
        temp = self.settings_path.with_suffix('.pending')
        fd = os.open(str(temp), os.O_WRONLY|os.O_CREAT|os.O_TRUNC, 0o600)
        with os.fdopen(fd, 'w') as f: json.dump(clean,f,ensure_ascii=False,indent=2)
        os.replace(str(temp),str(self.settings_path))
        self.settings_path.chmod(0o600)

    def append(self, device, sample):
        gpu = sample.get('gpus',[])
        mean = sum(x.get('utilization') or 0 for x in gpu)/len(gpu) if gpu else 0
        with self.connection() as c:
            c.execute('INSERT INTO samples(device,ts,cpu,memory,gpu,payload) VALUES(?,?,?,?,?,?)',
                (device,sample['timestamp'],sample['cpuPercent'],sample['memory']['percent'],mean,json.dumps(sample,separators=(',',':'),ensure_ascii=False)))
            self.insert_count += 1
            if self.insert_count % 150 == 0:
                c.execute('DELETE FROM samples WHERE ts < ?', (time.time()-self.settings['retentionDays']*86400,))

    def history(self, device, start, end, limit=900):
        limit = max(2,min(1200,int(limit)))
        with self.connection() as c:
            count = c.execute('SELECT COUNT(*) FROM samples WHERE device=? AND ts BETWEEN ? AND ?', (device,start,end)).fetchone()[0]
            stride = max(1,(count+limit-1)//limit)
            rows = c.execute('SELECT id,ts,cpu,memory,gpu FROM samples WHERE device=? AND ts BETWEEN ? AND ? ORDER BY ts,id',(device,start,end))
            result = [dict(zip(['id','timestamp','cpu','memory','gpu'],row)) for i,row in enumerate(rows) if i%stride==0]
        return {'samples':result,'total':count,'start':start,'end':end}

    def snapshot(self, ident, device):
        with self.connection() as c:
            row = c.execute('SELECT payload FROM samples WHERE id=? AND device=?',(int(ident),device)).fetchone()
        return json.loads(row[0]) if row else None

    def count(self):
        with self.connection() as c: return c.execute('SELECT COUNT(*) FROM samples').fetchone()[0]
