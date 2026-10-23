"""Authenticated loopback-only workbench API and static UI."""
import asyncio
import hmac
import math
import os
from pathlib import Path
import secrets
import shlex
import signal
import time
import paramiko
from aiohttp import web
from .devices import Manager, HostKeyRequired
from .files import FileService
from .storage import Store, VERSION
from .terminal import terminal
from . import probe


def finite(value):
    result=float(value)
    if not math.isfinite(result):raise ValueError('时间范围无效')
    return result


def create_app(store=None,web_root=None):
    store=store or Store()
    manager=Manager(store)
    files=FileService(manager)
    token=store.access_key
    web_root=Path(web_root or Path(__file__).resolve().parents[1]/'web').resolve()

    @web.middleware
    async def guard(request,handler):
        port=request.transport.get_extra_info('sockname')[1]
        hosts={f'127.0.0.1:{port}',f'localhost:{port}',f'[::1]:{port}'}
        if request.host not in hosts:raise web.HTTPForbidden(text='仅限本机工作台')
        api=request.path.startswith('/api/')
        if api and request.path!='/api/health':
            if not hmac.compare_digest(request.cookies.get('aq_session',''),token):raise web.HTTPUnauthorized()
            origin=request.headers.get('Origin')
            if origin and origin not in {f'http://{host}' for host in hosts}:raise web.HTTPForbidden()
            if request.method not in ('GET','HEAD') and request.headers.get('X-AQ-Request')!='1':raise web.HTTPForbidden()
        try:
            response=await handler(request)
        except HostKeyRequired as error:
            response=web.json_response({'error':str(error),'code':'host-key','fingerprint':error.fingerprint},status=409)
        except paramiko.AuthenticationException:
            response=web.json_response({'error':'SSH 认证失败，请检查用户名、密钥或密码'},status=400)
        except paramiko.BadHostKeyException:
            response=web.json_response({'error':'SSH 主机密钥已变化，请核对服务器身份'},status=409)
        except (ValueError,KeyError,TypeError) as error:
            response=web.json_response({'error':str(error)[:200]},status=400)
        except PermissionError:
            response=web.json_response({'error':'当前用户没有此路径或进程的权限'},status=403)
        except (OSError,paramiko.SSHException):
            response=web.json_response({'error':'操作未完成，请检查连接或路径；原始文件不会被移动'},status=400)
        if not response.prepared:
            response.headers['X-Content-Type-Options']='nosniff'
            response.headers['X-Frame-Options']='DENY'
            response.headers['Referrer-Policy']='no-referrer'
            response.headers['Cache-Control']='no-store' if api else 'no-cache'
            response.headers['Content-Security-Policy']="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' ws://127.0.0.1:* ws://localhost:*; frame-ancestors 'none'; object-src 'none'"
        return response

    app=web.Application(middlewares=[guard],client_max_size=1024**4)
    app['manager']=manager;app['store']=store;app['files']=files

    async def index(request):
        if not hmac.compare_digest(request.cookies.get('aq_session',''),token) and not hmac.compare_digest(request.query.get('key',''),token):
            raise web.HTTPForbidden(text='请从 AQ Server 应用菜单打开工作台')
        response=web.FileResponse(web_root/'index.html')
        response.set_cookie('aq_session',token,httponly=True,samesite='Strict',path='/')
        return response

    async def state(request):
        return web.json_response({'version':VERSION,'platform':'Ubuntu','settings':store.settings,
            'devices':[x.public() for x in manager.devices.values()],
            'archive':{'path':str(store.db),'count':await asyncio.to_thread(store.count)},'transfers':files.transfers})

    async def settings(request):
        data=await request.json()
        if data.get('theme') not in ('light','dark','dusk'):raise ValueError('主题无效')
        interval=int(data.get('interval',2));font=int(data.get('terminalFontSize',14));retention=int(data.get('retentionDays',30))
        if not 1<=interval<=60 or not 10<=font<=24 or not 1<=retention<=3650:raise ValueError('更新频率、字体或存档天数超出范围')
        order=data.get('historyOrder',store.settings['historyOrder'])
        if sorted(order)!=['chart','gpus','processes']:raise ValueError('历史模块顺序无效')
        family=str(data.get('terminalFontFamily','monospace'))
        if family not in ('monospace','DejaVu Sans Mono','Noto Sans Mono','Liberation Mono'):raise ValueError('终端字体无效')
        store.settings.update(theme=data['theme'],interval=interval,terminalFontSize=font,
                              terminalFontFamily=family,retentionDays=retention,historyOrder=order)
        store.save();return web.json_response({'ok':True})

    async def device_action(request):
        data=await request.json();action=data.get('action','add')
        if action=='add':return web.json_response({'device':manager.add(data).public()})
        device=manager.devices.get(data.get('id'))
        if not device:raise ValueError('设备不存在')
        if action=='connect':
            start=time.perf_counter();await manager.enable(device,data.get('password'),data.get('fingerprint'))
            return web.json_response({'ok':True,'connectMs':round((time.perf_counter()-start)*1000),'message':'连接成功'})
        if action=='disconnect':await manager.disable(device);return web.json_response({'ok':True})
        if action=='remove':
            if device.config['id']=='local':raise ValueError('不能删除本机')
            await manager.disable(device);del manager.devices[device.config['id']];manager.persist()
            return web.json_response({'ok':True,'historyPreserved':True})
        raise ValueError('操作无效')

    async def history(request):
        end=finite(request.query.get('end',time.time()));start=finite(request.query.get('start',end-3600))
        if end<start:raise ValueError('结束时间必须晚于开始时间')
        return web.json_response(await asyncio.to_thread(store.history,request.query.get('device','local'),start,end,request.query.get('limit',900)))

    async def snapshot(request):
        result=await asyncio.to_thread(store.snapshot,request.match_info['id'],request.query.get('device','local'))
        if not result:raise web.HTTPNotFound()
        return web.json_response(result)

    async def end_process(request):
        data=await request.json();pid=int(data.get('pid',0));start=str(data.get('startToken',''))
        if data.get('confirm') is not True or pid<=1 or not start.isdigit():raise ValueError('请先确认进程身份和结束操作')
        device=manager.devices.get(data.get('device','local'))
        if not device:raise ValueError('设备不存在')
        if device.config['type']=='local':
            if pid==os.getpid():raise ValueError('不能结束工作台自身')
            current=await asyncio.to_thread(probe.process_info,pid)
            if current['startToken']!=start:raise ValueError('进程已退出或 PID 已改变，请刷新后重试')
            os.kill(pid,signal.SIGTERM)
        else:
            code="import pathlib,os,signal,sys;p=int(sys.argv[1]);s=pathlib.Path('/proc/'+str(p)+'/stat').read_text().rsplit(')',1)[1].split()[19];assert s==sys.argv[2],'PID changed';os.kill(p,signal.SIGTERM)"
            _,out,_=await asyncio.to_thread(device.client.exec_command,'python3 -c '+shlex.quote(code)+' '+str(pid)+' '+shlex.quote(start),timeout=8)
            if await asyncio.to_thread(out.channel.recv_exit_status):raise ValueError('进程已变化或无权结束，请刷新后重试')
        return web.json_response({'ok':True,'signal':'SIGTERM'})

    async def health(request):return web.json_response({'version':VERSION,'platform':'linux','uid':os.getuid()})
    app.router.add_get('/',index)
    app.router.add_get('/api/health',health)
    app.router.add_get('/api/state',state)
    app.router.add_post('/api/settings',settings)
    app.router.add_post('/api/devices',device_action)
    app.router.add_get('/api/history',history)
    app.router.add_get(r'/api/history/{id:\d+}',snapshot)
    app.router.add_get('/api/files',files.list)
    app.router.add_post('/api/files/upload',files.upload)
    app.router.add_post('/api/files/undo',files.undo_upload)
    app.router.add_get('/api/files/download',files.download)
    app.router.add_post('/api/process/end',end_process)
    app.router.add_get('/api/terminal',terminal)
    app.router.add_static('/assets/',str(web_root),show_index=False)
    async def startup(app):await manager.start()
    async def cleanup(app):await manager.stop()
    app.on_startup.append(startup);app.on_cleanup.append(cleanup)
    return app
