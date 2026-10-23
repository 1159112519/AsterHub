"""Drive the installed GTK/WebKit UI, recording actions and actual screenshots.
Run as the dedicated test user under xvfb-run, never with a production profile.
The action plan is authored by the tester. This tool never declares product acceptance.
"""
import argparse
import json
from pathlib import Path
import re
import sys
import time
import gi

gi.require_version('Gtk','3.0')
from gi.repository import Gtk,Gdk,GLib
from aqserver.desktop import run
from aqserver.storage import VERSION

p=argparse.ArgumentParser();p.add_argument('--plan',required=True);p.add_argument('--evidence',required=True);args=p.parse_args()
plan=json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
if not isinstance(plan,list) or len(plan)>100:raise SystemExit('A plan must contain at most 100 actions')
out=Path(args.evidence);out.mkdir(parents=True,exist_ok=True)
report={'version':VERSION,'driver':'installed-native-gtk-webkit','started':time.time(),'actions':[],'screenshots':[],'ok':False}
window=None;webview=None;index=0;current=None

def save():
 (out/'actions.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')

def stop(error=None):
 if error:
  report['error']=str(error)[:500]
  if window and window.get_window():
   target=out/'failure.png'
   pixbuf=Gdk.pixbuf_get_from_window(window.get_window(),0,0,window.get_allocated_width(),window.get_allocated_height())
   pixbuf.savev(str(target),'png',[],[]);report['screenshots'].append(str(target))
 report['ended']=time.time();save();Gtk.main_quit();return False

def javascript(script,done):
 def finished(view,result,*unused):
  try:
   value=view.run_javascript_finish(result).get_js_value()
   text=value.to_json(0) if not value.is_undefined() else 'null'
   done(json.loads(text))
  except Exception as error:stop(type(error).__name__+': '+str(error))
 webview.run_javascript(script,None,finished,None)

def next_action():
 global index,current
 if index>=len(plan):report['ok']=True;return stop()
 current=plan[index];index+=1
 entry={'index':index,'action':current.get('action'),'at':time.time()};report['actions'].append(entry)
 action=current.get('action')
 if action=='wait':
  seconds=max(.05,min(15,float(current.get('seconds',1))));entry['seconds']=seconds
  GLib.timeout_add(int(seconds*1000),next_action);return False
 if action=='screenshot':
  label=re.sub('[^a-zA-Z0-9_-]','-',str(current.get('name',index)))[:80]
  target=out/(str(index).zfill(2)+'-'+label+'.png')
  pixbuf=Gdk.pixbuf_get_from_window(window.get_window(),0,0,window.get_allocated_width(),window.get_allocated_height())
  pixbuf.savev(str(target),'png',[],[]);entry['screenshot']=str(target);report['screenshots'].append(str(target));save()
  GLib.timeout_add(100,next_action);return False
 if action=='resize':
  width=max(860,min(1800,int(current.get('width',1280))));height=max(600,min(1200,int(current.get('height',850))))
  window.resize(width,height);entry.update(width=width,height=height);GLib.timeout_add(500,next_action);return False
 def complete(value):
  entry['result']=value;entry['finished']=time.time();save();GLib.timeout_add(200,next_action)
 selector=json.dumps(current.get('selector',''))
 if action=='click':script=f"(()=>{{let e=document.querySelector({selector});if(!e)throw Error('selector missing');e.click();return true;}})()"
 elif action=='input':
  value=json.dumps(str(current.get('value','')))
  script=f"(()=>{{let e=document.querySelector({selector});if(!e)throw Error('selector missing');e.value={value};e.dispatchEvent(new Event('input',{{bubbles:true}}));e.dispatchEvent(new Event('change',{{bubbles:true}}));return true;}})()"
 elif action=='key':
  key=json.dumps(str(current.get('key','Enter')))
  script=f"(()=>{{let e=document.querySelector({selector});if(!e)throw Error('selector missing');e.dispatchEvent(new KeyboardEvent('keydown',{{key:{key},bubbles:true}}));return true;}})()"
 elif action=='terminal':
  command=str(current.get('command',''))
  if len(command)>2000:raise ValueError('Terminal action too long')
  entry['command']=command
  value=json.dumps(command)
  script=f"(()=>{{let e=document.querySelector('.terminal-panel:not([hidden]) .xterm-helper-textarea');if(!e)throw Error('terminal missing');e.focus();let d=new DataTransfer();d.setData('text/plain',{value});e.dispatchEvent(new ClipboardEvent('paste',{{clipboardData:d,bubbles:true}}));e.dispatchEvent(new KeyboardEvent('keydown',{{key:'Enter',code:'Enter',keyCode:13,which:13,bubbles:true,cancelable:true}}));return true;}})()"
 elif action=='evaluate':script=str(current.get('script','null'))
 elif action=='assert-text':
  expected=json.dumps(str(current.get('contains','')))
  script=f"(()=>{{let e=document.querySelector({selector});if(!e||!e.textContent.includes({expected}))throw Error('expected text missing');return e.textContent.slice(0,500);}})()"
 elif action=='chart-select':
  fraction=max(0,min(1,float(current.get('fraction',.5))))
  script=f"(()=>{{let e=document.querySelector({selector});if(!e)throw Error('chart missing');let r=e.getBoundingClientRect();e.dispatchEvent(new PointerEvent('pointermove',{{clientX:r.left+40+(r.width-45)*{fraction},clientY:r.top+r.height/2,bubbles:true}}));return true;}})()"
  def clicked(value):
   GLib.timeout_add(100,lambda:(javascript(f"document.querySelector({selector}).click()",complete),False)[1])
  javascript(script,clicked);return False
 else:return stop('Unsupported action: '+str(action))
 if action in ('click','input','key','assert-text','chart-select'):entry['selector']=current.get('selector')
 javascript(script,complete);return False


def begin():
 global window,webview
 windows=[w for w in Gtk.Window.list_toplevels() if w.get_visible() and w.get_title()=='AQ Server']
 if not windows:return stop('Native AQ Server window missing')
 window=windows[0];webview=window.get_child()
 return next_action()
GLib.timeout_add(4500,begin)
GLib.timeout_add(180000,lambda:stop('Native driver timeout'))
run(19871)
print(json.dumps({'ok':report['ok'],'actions':len(report['actions']),'evidence':str(out),'error':report.get('error')}),flush=True)
if not report['ok']:raise SystemExit(1)
