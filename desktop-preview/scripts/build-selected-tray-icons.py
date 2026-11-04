"""Keep each selected app palette in every tray state; add only the health badge.
The paired PNG and ICO frames come from the same per-size app.ico raster.
"""
from pathlib import Path
import io,struct
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
SIZES=[16,20,24,32,40,48,64]
STATES={'neutral':None,'online':'#44D9A4','reconnecting':'#F4B74C','offline':'#8A95AA','error':'#EA6B7B'}
def save_ico(path,frames):
 chunks=[];entries=[];offset=6+16*len(frames)
 for size,image in frames:
  output=io.BytesIO();image.save(output,format='PNG');data=output.getvalue();chunks.append(data);entries.append(struct.pack('<BBBBHHII',size,size,0,0,1,32,len(data),offset));offset+=len(data)
 path.write_bytes(struct.pack('<HHH',0,1,len(frames))+b''.join(entries)+b''.join(chunks))
def render(base,theme,state):
 image=base.convert('RGBA').copy();size=image.width
 if STATES[state]:
  scale=16;badge=Image.new('RGBA',(size*scale,size*scale));draw=ImageDraw.Draw(badge);center=size*.83
  for radius,color in [(size*.16,'#202838' if theme=='dark' else '#F2F5FA'),(size*.126,STATES[state])]:draw.ellipse(tuple(v*scale for v in [center-radius,center-radius,center+radius,center+radius]),fill=color)
  image.alpha_composite(badge.resize((size,size),Image.Resampling.LANCZOS))
 return image
if __name__=='__main__':
 for number in range(1,10):
  icon_id=f'{number:02}';source=Image.open(ROOT/'host'/'brand-icons'/icon_id/'app.ico')
  for location in [ROOT/'host'/'brand-icons',ROOT/'app'/'resources'/'brand-icons']:
   folder=location/icon_id/'tray';folder.mkdir(parents=True,exist_ok=True)
   for theme in ['light','dark']:
    for state in STATES:
     frames=[(size,render(source.ico.getimage((size,size)),theme,state)) for size in SIZES]
     for size,image in frames:image.save(folder/f'{theme}-{state}-{size}.png')
     save_ico(folder/f'{theme}-{state}.ico',frames)
 print('MATCHING_SELECTED_TRAY_SETS=9; ICO=180; PNG=1260 (host + packaged)')
