"""Audit selected app/tray frame identity and packaged copies without changing resources."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import argparse,json,runpy
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--out',required=True);parser.add_argument('--source-only',action='store_true');args=parser.parse_args();out=Path(args.out);out.mkdir(parents=True,exist_ok=True)
source=ROOT/'host'/'brand-icons';package=ROOT/'app'/'resources'/'brand-icons';web=ROOT/'web'/'public'/'brand-icons'
metrics=runpy.run_path(str(ROOT/'scripts'/'brand-app'/'build-windows-list-icons.py'));offsets=metrics['offsets'];small_sizes=metrics['SHELL_SIZES']
assert (source/'01'/'window.ico').read_bytes()==(ROOT/'host'/'AsterHub.Process.ico').read_bytes()
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',12)
sheet=Image.new('RGB',(1170,9*118),'#1c2330');draw=ImageDraw.Draw(sheet);rows=[];frames=pngs=files=0
for number in range(1,10):
 key=f'{number:02}';folder=source/key;app=Image.open(folder/'app.ico');window=Image.open(folder/'window.ico');row={'id':key,'smallWindowOffsets':{},'frames':0,'pngs':0,'copiedFiles':0}
 assert app.ico.sizes()==window.ico.sizes()
 assert Image.open(web/key/'preview.png').convert('RGBA').tobytes()==Image.open(folder/'app-256.png').convert('RGBA').tobytes()
 assert app.ico.getimage((256,256)).convert('RGBA').tobytes()==Image.open(folder/'app-256.png').convert('RGBA').tobytes()
 images=[('APP',app.ico.getimage((64,64))),('WINDOW 16',window.ico.getimage((16,16))),('WINDOW 32',window.ico.getimage((32,32)))]
 for theme in ['dark','light']:
  for state in ['neutral','online','reconnecting','offline','error']:
   icon=Image.open(folder/'tray'/f'{theme}-{state}.ico');assert set(icon.ico.sizes())=={(n,n) for n in [16,20,24,32,40,48,64]}
   for size in [16,20,24,32,40,48,64]:
    actual=icon.ico.getimage((size,size)).convert('RGBA');paired=Image.open(folder/'tray'/f'{theme}-{state}-{size}.png').convert('RGBA');assert actual.tobytes()==paired.tobytes()
    if state=='neutral':assert actual.tobytes()==app.ico.getimage((size,size)).convert('RGBA').tobytes()
    row['frames']+=1;row['pngs']+=1
   images.append((theme+' '+state,icon.ico.getimage((32,32))))
 for size in sorted(app.ico.sizes()):
  original=app.ico.getimage(size).convert('RGBA');aligned=window.ico.getimage(size).convert('RGBA');assert original.size==aligned.size==size
  if size[0] in small_sizes:
   before=float(abs(offsets(original)).max());after=float(abs(offsets(aligned)).max());assert after<=before+1e-8
   row['smallWindowOffsets'][size[0]]={'before':round(before,4),'after':round(after,4)}
   if key=='01' and size[0]==20:assert after<0.12 and after<before*.7
  else:assert original.tobytes()==aligned.tobytes()
  row['frames']+=2
 for file in folder.rglob('*'):
  if not args.source_only and file.is_file():assert file.read_bytes()==(package/key/file.relative_to(folder)).read_bytes();row['copiedFiles']+=1
 row['pngs']+=1;frames+=row['frames'];pngs+=row['pngs'];files+=row['copiedFiles'];rows.append(row);draw.text((7,number*118-70),key,font=font,fill='white')
 for index,(label,image) in enumerate(images):
  x=42+index*86;y=(number-1)*118+12;tile=Image.new('RGBA',(64,64));image=image.copy();image.thumbnail((58,58));tile.alpha_composite(image,((64-image.width)//2,(64-image.height)//2));sheet.paste(tile,(x,y),tile);draw.text((x-1,y+73),label,font=font,fill='white')
sheet.save(out/'icons-after.png');summary={'result':'PASS','families':9,'decodedIcoFrames':frames,'pngs':pngs,'sourcePackageFilesCompared':files,'neutralTrayEqualsSelectedAppAtEverySize':True,'webPreviewMatchesAllSelectedApps':True,'rows':rows};(out/'assets-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in summary.items() if k!='rows'}))
