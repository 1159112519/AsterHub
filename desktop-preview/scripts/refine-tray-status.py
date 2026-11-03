"""Refine small tray health badges as supersampled circles, preserving neutral art."""
from pathlib import Path
from PIL import Image,ImageDraw
import io,struct
ROOT=Path(__file__).resolve().parents[1]/'host'/'brand-icons'
SIZES=[16,20,24,32,40,48,64]
STATES={'online':'#44D9A4','reconnecting':'#F4B74C','offline':'#8A95AA','error':'#EA6B7B'}
def save_ico(path,images):
 chunks=[];entries=[];offset=6+16*len(images)
 for size,im in images:
  stream=io.BytesIO();im.save(stream,format='PNG');data=stream.getvalue();chunks.append(data);entries.append(struct.pack('<BBBBHHII',size,size,0,0,1,32,len(data),offset));offset+=len(data)
 path.write_bytes(struct.pack('<HHH',0,1,len(images))+b''.join(entries)+b''.join(chunks))
for folder in ROOT.iterdir():
 if not folder.name.isdigit():continue
 for theme in ['light','dark']:
  for state,color in STATES.items():
   frames=[]
   for size in SIZES:
    neutral=Image.open(folder/'tray'/f'{theme}-neutral-{size}.png').convert('RGBA')
    scale=16;badge=Image.new('RGBA',(size*scale,size*scale));d=ImageDraw.Draw(badge);center=size*.83;outer=size*.16;radius=size*.126
    def circle(r,ink):d.ellipse(tuple(v*scale for v in [center-r,center-r,center+r,center+r]),fill=ink)
    circle(outer,'#202838' if theme=='dark' else '#F2F5FA');circle(radius,color)
    neutral.alpha_composite(badge.resize((size,size),Image.Resampling.LANCZOS));neutral.save(folder/'tray'/f'{theme}-{state}-{size}.png');frames.append((size,neutral))
   save_ico(folder/'tray'/f'{theme}-{state}.ico',frames)
print('Circular status badges regenerated for 9 palettes, 2 themes, 4 states, 7 sizes')
