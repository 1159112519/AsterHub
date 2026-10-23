"""Filesystem operations are copies; source paths are never removed."""
import asyncio
import hashlib
import mimetypes
import os
from pathlib import Path, PurePosixPath
import posixpath
import stat
import tempfile
import time
import uuid
import zipfile
from aiohttp import web


def safe_relative(value):
    value=value.replace('\\','/')
    path=PurePosixPath(value)
    if not value or path.is_absolute() or '..' in path.parts or '\x00' in value or len(value)>4096:
        raise ValueError('文件相对路径无效')
    return str(path)


class Filesystem:
    def __init__(self,device):
        self.device=device; self.remote=device.config['type']=='ssh'
        self.sftp=device.sftp() if self.remote else None

    def path(self,value):
        home=self.device.home or '/'
        value=str(value or home)
        if value=='~': return home
        if value.startswith('~/'): value=posixpath.join(home,value[2:])
        if '\x00' in value or len(value)>4096: raise ValueError('路径无效')
        return posixpath.normpath(posixpath.join(home,value)) if self.remote else os.path.abspath(os.path.expanduser(value))

    def stat(self,path): return self.sftp.stat(path) if self.remote else os.stat(path)
    def exists(self,path):
        try: self.stat(path); return True
        except FileNotFoundError: return False
    def open(self,path,mode): return self.sftp.open(path,mode) if self.remote else open(path,mode)
    def mkdirs(self,path):
        if not self.remote: Path(path).mkdir(parents=True,exist_ok=True); return
        try:
            if not stat.S_ISDIR(self.stat(path).st_mode): raise ValueError('目标不是文件夹')
        except FileNotFoundError:
            parent=posixpath.dirname(path)
            if parent!=path: self.mkdirs(parent)
            self.sftp.mkdir(path)
    def remove(self,path):
        if self.remote: self.sftp.remove(path)
        else: os.unlink(path)
    def listing(self,path):
        path=self.path(path)
        if self.remote:
            entries=[(x.filename,x.st_mode,x.st_size,x.st_mtime) for x in self.sftp.listdir_attr(path)]
        else:
            entries=[]
            with os.scandir(path) as scan:
                for x in scan:
                    try:
                        info=x.stat(follow_symlinks=False)
                        entries.append((x.name,info.st_mode,info.st_size,info.st_mtime))
                    except OSError: continue
        result=[dict(name=n,directory=stat.S_ISDIR(mode),symlink=stat.S_ISLNK(mode),size=size,modified=mtime,
                     path=posixpath.join(path,n)) for n,mode,size,mtime in entries]
        result.sort(key=lambda x:(not x['directory'],x['name'].casefold()))
        return {'path':path,'parent':posixpath.dirname(path),'entries':result[:10000],'truncated':len(result)>10000}
    def close(self):
        if self.sftp:self.sftp.close()


class FileService:
    def __init__(self,manager):
        self.manager=manager; self.transfers=[]; self.undo={}

    def device(self,ident):
        if ident not in self.manager.devices: raise ValueError('设备不存在')
        return self.manager.devices[ident]

    def transfer(self,name,direction,total=0):
        task=dict(id=uuid.uuid4().hex,name=name,direction=direction,total=total,done=0,status='transferring',started=time.time())
        self.transfers.append(task);self.transfers=self.transfers[-80:];return task

    async def list(self,request):
        fs=await asyncio.to_thread(Filesystem,self.device(request.query.get('device','local')))
        try:return web.json_response(await asyncio.to_thread(fs.listing,request.query.get('path','~')))
        finally:fs.close()

    async def upload(self,request):
        device=self.device(request.query.get('device','local'))
        fs=await asyncio.to_thread(Filesystem,device)
        task=None; temp=None
        try:
            directory=fs.path(request.query.get('path','~'))
            relative=safe_relative(request.query.get('relative','upload'))
            destination=posixpath.join(directory,relative)
            task=self.transfer(relative,'upload',max(0,int(request.query.get('size','0'))))
            if await asyncio.to_thread(fs.exists,destination): raise web.HTTPConflict(text='目标文件已存在，请先重命名；原文件未改动')
            await asyncio.to_thread(fs.mkdirs,posixpath.dirname(destination))
            temp=destination+'.aq-part-'+uuid.uuid4().hex
            reader=await request.multipart(); field=await reader.next()
            if not field or field.name!='file': raise ValueError('没有选择文件')
            digest=hashlib.sha256()
            stream=await asyncio.to_thread(fs.open,temp,'wb')
            try:
                while True:
                    chunk=await field.read_chunk(256*1024)
                    if not chunk: break
                    await asyncio.to_thread(stream.write,chunk);digest.update(chunk);task['done']+=len(chunk)
            finally:await asyncio.to_thread(stream.close)
            if fs.remote:
                await asyncio.to_thread(fs.sftp.rename,temp,destination)
            else:
                await asyncio.to_thread(os.link,temp,destination)
                await asyncio.to_thread(os.unlink,temp)
            temp=None
            task.update(status='completed',total=task['done'],canUndo=True)
            self.undo[task['id']]=(device.config['id'],destination,digest.hexdigest(),task['done'])
            return web.json_response(task)
        except Exception as error:
            cause=error.text if isinstance(error,web.HTTPException) else str(error) if isinstance(error,ValueError) else '传输未完成，请检查连接或路径；源文件保留'
            if task:task.update(status='failed',error=cause[:200])
            raise
        finally:
            if temp:
                try:await asyncio.to_thread(fs.remove,temp)
                except OSError:pass
            fs.close()

    async def undo_upload(self,request):
        data=await request.json();ident=data.get('id')
        if ident not in self.undo:raise ValueError('此传输不能撤销')
        device,path,digest,size=self.undo[ident]
        fs=await asyncio.to_thread(Filesystem,self.device(device))
        try:
            if (await asyncio.to_thread(fs.stat,path)).st_size!=size:raise ValueError('文件已修改，不能撤销')
            def check():
                result=hashlib.sha256()
                with fs.open(path,'rb') as source:
                    for chunk in iter(lambda:source.read(256*1024),b''):result.update(chunk)
                return result.hexdigest()
            if await asyncio.to_thread(check)!=digest:raise ValueError('文件已修改，不能撤销')
            await asyncio.to_thread(fs.remove,path);del self.undo[ident]
            for task in self.transfers:
                if task['id']==ident:task.update(status='undone',canUndo=False)
            return web.json_response({'ok':True})
        finally:fs.close()

    async def download(self,request):
        fs=await asyncio.to_thread(Filesystem,self.device(request.query.get('device','local')))
        path=fs.path(request.query.get('path','~'));temp=None;stream=None;task=None
        try:
            info=await asyncio.to_thread(fs.stat,path);name=posixpath.basename(path) or 'files'
            if stat.S_ISDIR(info.st_mode):
                fd,filename=tempfile.mkstemp(prefix='aq-download-',suffix='.zip',dir=str(self.manager.store.data_dir));os.close(fd);temp=Path(filename)
                def archive():
                    def add(folder,relative,z):
                        for entry in fs.listing(folder)['entries']:
                            if entry['symlink']:continue
                            arc=posixpath.join(relative,entry['name'])
                            if entry['directory']:z.writestr(arc+'/',b'');add(entry['path'],arc,z)
                            else:
                                with fs.open(entry['path'],'rb') as source,z.open(arc,'w') as target:
                                    for chunk in iter(lambda:source.read(256*1024),b''):target.write(chunk)
                    with zipfile.ZipFile(str(temp),'w',zipfile.ZIP_DEFLATED) as z:add(path,name,z)
                await asyncio.to_thread(archive)
                stream=open(temp,'rb');size=temp.stat().st_size;name+='.zip'
            else:
                stream=await asyncio.to_thread(fs.open,path,'rb');size=info.st_size
            from urllib.parse import quote
            task=self.transfer(name,'download',size)
            response=web.StreamResponse(headers={'Content-Type':'application/octet-stream','Content-Length':str(size),
                'Content-Disposition':"attachment; filename*=UTF-8''"+quote(name,safe='')})
            await response.prepare(request)
            while True:
                chunk=await asyncio.to_thread(stream.read,256*1024)
                if not chunk:break
                await response.write(chunk);task['done']+=len(chunk)
            task['status']='completed';await response.write_eof();return response
        except Exception:
            if task:task['status']='failed'
            raise
        finally:
            if stream:stream.close()
            fs.close()
            if temp:temp.unlink(missing_ok=True)
