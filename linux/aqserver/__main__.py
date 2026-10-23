import argparse
import os
from aiohttp import web
from .storage import VERSION


def main():
    parser=argparse.ArgumentParser(description='AQ Server Ubuntu 工作台')
    parser.add_argument('--version',action='version',version=VERSION)
    parser.add_argument('--port',type=int,default=19871)
    parser.add_argument('--serve',action='store_true',help='只运行本机后台服务')
    parser.add_argument('--desktop',action='store_true',help='打开 GTK 桌面窗口')
    parser.add_argument('--data-home',help='隔离的用户数据目录')
    parser.add_argument('--config-home',help='隔离的用户配置目录')
    args=parser.parse_args()
    if not 1024<=args.port<=65535:parser.error('端口必须在 1024–65535 之间')
    os.umask(0o077)
    if args.data_home:os.environ['XDG_DATA_HOME']=args.data_home
    if args.config_home:os.environ['XDG_CONFIG_HOME']=args.config_home
    if args.desktop:
        from .desktop import run
        run(args.port)
    else:
        from .server import create_app
        web.run_app(create_app(),host='127.0.0.1',port=args.port,access_log=None,print=lambda text:print('AQ Server '+VERSION+' · http://127.0.0.1:'+str(args.port),flush=True))

if __name__=='__main__':main()
