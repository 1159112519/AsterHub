"""Linux metrics probe; also executed over an authenticated SSH channel."""
import csv
import io
import json
import os
import pathlib
import platform
import pwd
import shutil
import subprocess
import time


def read(path, default=""):
    try:
        return pathlib.Path(path).read_text(errors="replace")
    except OSError:
        return default


def number(value):
    try:
        return float(value)
    except (ValueError, TypeError):
        return None


def process_info(pid):
    try:
        base = pathlib.Path('/proc') / str(int(pid))
        raw = (base / 'stat').read_text()
        rest = raw.rsplit(')', 1)[1].split()
        return {'pid': int(pid), 'name': raw[raw.index('(')+1:raw.rindex(')')],
                'username': pwd.getpwuid(base.stat().st_uid).pw_name,
                'startToken': rest[19], 'rss': int(rest[21]) * os.sysconf('SC_PAGE_SIZE')}
    except (OSError, ValueError, KeyError, IndexError):
        return {'pid': int(pid), 'name': '', 'username': '—', 'startToken': None, 'rss': None}


def collect():
    cpu = []
    for line in read('/proc/stat').splitlines():
        parts = line.split()
        if parts and parts[0].startswith('cpu'):
            counters = [int(x) for x in parts[1:9]]
            cpu.append({'id': parts[0], 'total': sum(counters), 'idle': counters[3]+counters[4]})
    mem = {}
    for line in read('/proc/meminfo').splitlines():
        key, _, value = line.partition(':')
        mem[key] = int(value.strip().split()[0]) * 1024
    blocks = read('/proc/cpuinfo').strip().split('\n\n')
    model, sockets, cores, current = '', set(), set(), []
    for block in blocks:
        values = dict(line.split(':', 1) for line in block.splitlines() if ':' in line)
        values = {k.strip(): v.strip() for k, v in values.items()}
        model = values.get('model name', values.get('Processor', model))
        sockets.add(values.get('physical id', '0'))
        cores.add((values.get('physical id', '0'), values.get('core id', values.get('processor', '0'))))
        if number(values.get('cpu MHz')):
            current.append(number(values['cpu MHz']))
    max_khz = number(read('/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq').strip())
    os_name = ''
    for line in read('/etc/os-release').splitlines():
        if line.startswith('PRETTY_NAME='):
            os_name = line.split('=', 1)[1].strip('"')
    net = [0, 0]
    for line in read('/proc/net/dev').splitlines()[2:]:
        name, _, values = line.partition(':')
        values = values.split()
        if name.strip() != 'lo' and len(values) >= 9:
            net[0] += int(values[0]); net[1] += int(values[8])
    disk = shutil.disk_usage(os.path.expanduser('~'))
    gpus, gpu_processes, gpu_error = [], [], None
    if shutil.which('nvidia-smi'):
        query = 'index,uuid,name,memory.total,memory.used,utilization.gpu,temperature.gpu,power.draw,power.limit,fan.speed,driver_version'
        try:
            data = subprocess.check_output(['nvidia-smi', '--query-gpu='+query,
                '--format=csv,noheader,nounits'], timeout=5, stderr=subprocess.DEVNULL).decode()
            for row in csv.reader(io.StringIO(data)):
                if len(row) != 11: continue
                row = [x.strip() for x in row]
                gpus.append(dict(zip(['index','uuid','name','memoryTotalMiB','memoryUsedMiB','utilization','temperature','powerWatts','powerLimitWatts','fanPercent','driver'],
                    [int(row[0]),row[1],row[2]]+[number(x) for x in row[3:10]]+[row[10]])))
            try:
                processes = subprocess.check_output(['nvidia-smi','--query-compute-apps=pid,gpu_uuid,used_memory,process_name','--format=csv,noheader,nounits'], timeout=5, stderr=subprocess.DEVNULL).decode()
                for row in csv.reader(io.StringIO(processes)):
                    if len(row) < 4: continue
                    item = process_info(row[0].strip())
                    item.update(gpuUuid=row[1].strip(),memoryMiB=number(row[2].strip()),name=row[3].strip())
                    gpu_processes.append(item)
            except (OSError, subprocess.SubprocessError):
                gpu_error = 'GPU 进程信息暂不可用'
        except (OSError, subprocess.SubprocessError):
            gpu_error = 'NVIDIA 驱动当前不可用'
    processes = []
    for entry in pathlib.Path('/proc').iterdir():
        if entry.name.isdigit():
            item = process_info(int(entry.name))
            if item['rss'] is not None: processes.append(item)
    processes.sort(key=lambda p: p['rss'], reverse=True)
    return {'timestamp': time.time(), 'cpuCounters': cpu,
        'memory': {'total':mem.get('MemTotal',0),'available':mem.get('MemAvailable',mem.get('MemFree',0)),
                   'swapTotal':mem.get('SwapTotal',0),'swapFree':mem.get('SwapFree',0)},
        'disk':dict(total=disk.total,used=disk.used,free=disk.free), 'networkCounters': net,
        'gpus':gpus,'gpuProcesses':gpu_processes,'processes':processes[:40], 'gpuError':gpu_error,
        'hardware':{'os':os_name or platform.system(),'hostname':platform.node(),'architecture':platform.machine(),
                    'cpuModel':model or platform.machine(),'logicalCpus':max(0,len(cpu)-1),'physicalCores':len(cores),
                    'cpuSockets':len(sockets),'cpuMaxMhz':max_khz/1000 if max_khz else None,
                    'cpuCurrentMhz':sum(current)/len(current) if current else None,'memoryType':None,'memoryFrequency':None}}

if __name__ == '__main__':
    print(json.dumps(collect(), separators=(',',':'), ensure_ascii=False))
