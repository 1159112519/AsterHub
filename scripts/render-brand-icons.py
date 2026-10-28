"""Render the original AQ quiet-rack mark and all platform icon sizes.
Requires Pillow. Geometry is authored here; no generated images or remote assets.
"""
from pathlib import Path
import json,shutil
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[1]
out=root/'assets/brand';out.mkdir(parents=True,exist_ok=True)
S=1024
bg='#30292B';ink='#EADBC9';accent='#9EBDB1'
def mark(d,main,cut,dot):
 for y in [240,534]:
  d.rounded_rectangle((216,y,808,y+198),radius=46,fill=main)
  d.polygon([(748,y),(808,y),(808,y+60)],fill=cut)
  d.rounded_rectangle((292,y+72,548,y+126),radius=25,fill=cut)
 d.ellipse((656,594,734,672),fill=dot)
def render(size,kind='app'):
 im=Image.new('RGBA',(S,S),(0,0,0,0));d=ImageDraw.Draw(im)
 if kind=='app':
  d.rounded_rectangle((48,48,976,976),radius=214,fill=bg)
  mark(d,ink,bg,accent)
 else:
  mark(d,'#F2F2F2' if kind=='light' else '#282426',(0,0,0,0),'#F2F2F2' if kind=='light' else '#282426')
  # A tray has a transparent canvas and a larger silhouette than the app tile.
  im=im.crop((160,196,864,776)).resize((S,844),Image.Resampling.LANCZOS)
  full=Image.new('RGBA',(S,S),(0,0,0,0));full.alpha_composite(im,(0,90));im=full
 return im.resize((size,size),Image.Resampling.LANCZOS)
app=render(1024);app.save(out/'aq-server.png')
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024"><rect x="48" y="48" width="928" height="928" rx="214" fill="{bg}"/><g fill="{ink}"><rect x="216" y="240" width="592" height="198" rx="46"/><rect x="216" y="534" width="592" height="198" rx="46"/></g><g fill="{bg}"><path d="M748 240h60v60zM748 534h60v60z"/><rect x="292" y="312" width="256" height="54" rx="25"/><rect x="292" y="606" width="256" height="54" rx="25"/></g><circle cx="695" cy="633" r="39" fill="{accent}"/></svg>'''
(out/'aq-server.svg').write_text(svg,encoding='utf-8')
sizes=[16,24,32,48,64,128,256]
app.save(root/'desktop/assets/AQServer.ico',sizes=[(n,n) for n in sizes],bitmap_format='png')
for n in sizes:render(n).save(out/f'aq-server-{n}.png')
for kind in ['light','dark']:
 render(256,kind).save(root/f'desktop/assets/AQTray{kind.title()}.ico',sizes=[(n,n) for n in [16,20,24,32]],bitmap_format='png')
 for n in [16,20,24,32]:render(n,kind).save(out/f'tray-{kind}-{n}.png')
shutil.copyfile(root/'desktop/assets/AQServer.ico',root/'linux/web/icon.ico')
shutil.copyfile(root/'desktop/assets/AQServer.ico',root/'website/public/favicon.ico')
render(256).save(root/'linux/packaging/aq-server.png')
sheet=Image.new('RGB',(980,400),'#D9D9D9');d=ImageDraw.Draw(sheet)
for row,color in enumerate(['#30292B','#FFFFFF','#838383']):
 d.rectangle((0,row*125,980,(row+1)*125),fill=color)
 x=24
 for n in [16,24,32,48,64]:
  sheet.paste(render(n),(x,row*125+32),render(n));x+=n+36
 for kind in ['light','dark']:
  for n in [16,20,24,32]:
   im=render(n,kind);sheet.paste(im,(x,row*125+32),im);x+=n+20
sheet.save(out/'icon-size-review.png')
verify={'original':'AQ Server quiet-rack, authored vector geometry','icoLayers':sorted([list(n) for n in Image.open(root/'desktop/assets/AQServer.ico').ico.sizes()]),'trayLayers':sorted([list(n) for n in Image.open(root/'desktop/assets/AQTrayLight.ico').ico.sizes()]),'physicalTaskbarAcceptance':False}
(out/'verification.json').write_text(json.dumps(verify,indent=2)+'\n',encoding='utf-8')
print('BRAND_ICONS_WRITTEN')