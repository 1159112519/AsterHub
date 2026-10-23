"""Native GTK/WebKit host; collection remains in the local service."""
import os
import json
import subprocess
from .storage import Store
import sys
import time
import urllib.request


def run(port):
    import gi
    gi.require_version('Gtk','3.0')
    try:gi.require_version('WebKit2','4.1')
    except ValueError:gi.require_version('WebKit2','4.0')
    from gi.repository import Gtk,WebKit2
    url='http://127.0.0.1:'+str(port)
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
    def ready():
        try:
            with opener.open(url+'/api/health',timeout=.5) as response:
                health=json.load(response)
                if health.get('uid')!=os.getuid():raise SystemExit('本地端口已被其他用户占用，请使用 --port 指定其他端口')
                return response.status==200
        except OSError:return False
    if not ready():
        subprocess.Popen([sys.executable,'-m','aqserver','--serve','--port',str(port)],stdin=subprocess.DEVNULL,
                         stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        for _ in range(60):
            if ready():break
            time.sleep(.1)
        else:raise SystemExit('AQ Server 本地服务未能启动，请运行 aq-server --serve 查看原因')
    window=Gtk.Window(title='AQ Server')
    window.set_icon_from_file('/usr/share/icons/hicolor/256x256/apps/aq-server.png')
    window.set_default_size(1280,850);window.set_size_request(860,600)
    webview=WebKit2.WebView()
    settings=webview.get_settings();settings.set_enable_developer_extras(False)
    store=Store()
    webview.load_uri(url+'/?key='+store.access_key)
    window.add(webview)
    window.connect('delete-event',lambda *args:(Gtk.main_quit(),False)[1])
    window.show_all();Gtk.main()
