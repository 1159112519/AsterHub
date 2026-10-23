"""Real PTY / SSH shells, with lifecycle scoped to each terminal tab."""
import asyncio
import fcntl
import json
import os
from pathlib import Path
import signal
import struct
import subprocess
import termios
from aiohttp import WSMsgType, web


async def terminal(request):
    manager=request.app['manager']
    device=manager.devices.get(request.query.get('device','local'))
    if not device:raise web.HTTPNotFound()
    if device.config['type']=='ssh' and device.status!='connected':raise web.HTTPConflict(text='设备未连接')
    ws=web.WebSocketResponse(max_msg_size=65536,heartbeat=20)
    await ws.prepare(request)
    process=None;master=None;channel=None
    try:
        if device.config['type']=='local':
            master,slave=os.openpty()
            env=dict(os.environ,TERM='xterm-256color',COLORTERM='truecolor',LANG=os.environ.get('LANG','C.UTF-8'))
            process=subprocess.Popen(['/bin/bash','--login','-i'],stdin=slave,stdout=slave,stderr=slave,
                                     cwd=str(Path.home()),env=env,start_new_session=True,close_fds=True)
            os.close(slave);os.set_blocking(master,False)
        else:
            channel=await asyncio.to_thread(device.client.invoke_shell,term='xterm-256color',width=100,height=30)
            channel.settimeout(.1)

        async def output():
            while not ws.closed:
                if master is not None:
                    try:data=os.read(master,32768)
                    except BlockingIOError:data=None
                    except OSError:break
                    if data==b'':break
                    if process.poll() is not None and not data:break
                else:
                    data=channel.recv(32768) if channel.recv_ready() else None
                    if channel.closed or channel.exit_status_ready():break
                if data:await ws.send_bytes(data)
                else:await asyncio.sleep(.016)
            await ws.close()

        reader=asyncio.create_task(output())
        try:
            async for message in ws:
                if message.type!=WSMsgType.TEXT:continue
                data=json.loads(message.data)
                if data.get('type')=='resize':
                    cols=max(20,min(500,int(data.get('cols',100))));rows=max(5,min(200,int(data.get('rows',30))))
                    if master is not None:fcntl.ioctl(master,termios.TIOCSWINSZ,struct.pack('HHHH',rows,cols,0,0))
                    else:channel.resize_pty(width=cols,height=rows)
                elif data.get('type')=='input':
                    raw=str(data.get('data','')).encode()[:32768]
                    if master is not None:
                        pending=memoryview(raw)
                        while pending:
                            try:written=os.write(master,pending);pending=pending[written:]
                            except BlockingIOError:await asyncio.sleep(.01)
                    else:await asyncio.to_thread(channel.sendall,raw)
        finally:
            reader.cancel();await asyncio.gather(reader,return_exceptions=True)
    finally:
        if channel:channel.close()
        if master is not None:os.close(master)
        if process and process.poll() is None:
            try:os.killpg(process.pid,signal.SIGHUP)
            except ProcessLookupError:pass
            try:await asyncio.to_thread(process.wait,2)
            except subprocess.TimeoutExpired:
                try:os.killpg(process.pid,signal.SIGTERM)
                except ProcessLookupError:pass
        await ws.close()
    return ws
