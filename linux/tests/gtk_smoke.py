"""Render the installed native GTK/WebKit application in an isolated X display."""
import json
from pathlib import Path
import sys
import gi

gi.require_version('Gtk','3.0')
from gi.repository import Gtk,Gdk,GLib
from aqserver.desktop import run

output=Path(sys.argv[1]);output.parent.mkdir(parents=True,exist_ok=True)
result={'kind':'native GTK/WebKit installed application','screenshot':str(output)}
def capture():
 try:
  windows=[w for w in Gtk.Window.list_toplevels() if w.get_visible()]
  window=next(w for w in windows if w.get_title()=='AQ Server')
  pixbuf=Gdk.pixbuf_get_from_window(window.get_window(),0,0,window.get_allocated_width(),window.get_allocated_height())
  pixbuf.savev(str(output),'png',[],[])
  result.update(title=window.get_title(),width=window.get_allocated_width(),height=window.get_allocated_height(),ok=True)
 except Exception as error:result.update(ok=False,error=type(error).__name__)
 print(json.dumps(result),flush=True);Gtk.main_quit();return False
GLib.timeout_add(8000,capture)
run(19871)
if not result.get('ok'):raise SystemExit(1)
