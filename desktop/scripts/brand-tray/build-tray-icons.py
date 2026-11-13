from pathlib import Path
import json,io,struct
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(__file__).resolve().parent
GEOMETRY=json.loads((SOURCE/'selected-geometry.json').read_text(encoding='utf8'))
PALETTES=json.loads((SOURCE/'palettes.json').read_text(encoding='utf8'))
SIZES=[16,20,24,32,40,48,64]
STATES={'neutral':None,'online':'#44D9A4','reconnecting':'#F4B74C','offline':'#8A95AA','error':'#EA6B7B'}
def xy(x,y,k):return ((4+(x-20)*92/60)*k,(4+(y-9)*92/82)*k)
def render(v,size,theme,state):
 n=size*8;k=n/100;im=Image.new('RGBA',(n,n));d=ImageDraw.Draw(im)
 fg='#EDF1FA' if theme=='dark' else '#35415A'
 for index,(kind,data,r,cut) in enumerate(GEOMETRY):
  ink=fg if index==0 else (v['wave'][0] if theme=='dark' else v['wave'][-1]) if index==3 else (0,0,0,0)
  if kind=='line':
   points=[xy(x,y,k) for x,y in data];width=max(1,round(8*k));d.line(points,fill=ink,width=width,joint='curve')
   for x,y in points:d.ellipse((x-width/2,y-width/2,x+width/2,y+width/2),fill=ink)
  else:
   p1=xy(data[0],data[1],k);p2=xy(data[2],data[3],k);box=(*p1,*p2)
   if kind=='rect':d.rounded_rectangle(box,radius=(15 if index==0 else 3)*k,fill=ink)
   else:d.ellipse(box,fill=ink)
 if STATES[state]:
  ring='#202838' if theme=='dark' else '#F2F5FA';d.ellipse((n*.69,n*.69,n*.995,n*.995),fill=ring);d.ellipse((n*.745,n*.745,n*.95,n*.95),fill=STATES[state])
 return im.resize((size,size),Image.Resampling.LANCZOS)
def save_ico(path,images):
 chunks=[];entries=[];offset=6+16*len(images)
 for size,im in images:
  b=io.BytesIO();im.save(b,format='PNG');data=b.getvalue();chunks.append(data);entries.append(struct.pack('<BBBBHHII',size,size,0,0,1,32,len(data),offset));offset+=len(data)
 path.write_bytes(struct.pack('<HHH',0,1,len(images))+b''.join(entries)+b''.join(chunks))
if __name__=='__main__':
 for v in PALETTES:
  for theme in ['light','dark']:
   for state in STATES:save_ico(ROOT/'host/brand-icons'/v['key']/'tray'/f'{theme}-{state}.ico',[(n,render(v,n,theme,state)) for n in SIZES])
 print('TRAY_ICONS_BUILT=60; SIZES=16,20,24,32,40,48,64')
