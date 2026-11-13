"""Assert the extra icon resources and generate a native-size comparison sheet."""
import argparse
from hashlib import sha256
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser=argparse.ArgumentParser();parser.add_argument('output',type=Path);parser.add_argument('--package',type=Path);args=parser.parse_args()
root=Path(__file__).resolve().parents[2];host=root/'host'/'brand-icons';web=root/'web'/'public'/'brand-icons'
keys=['01','10','11','12'];native_sizes={16,20,24,32,40,48,64,128,256}
base=Image.open(host/'01'/'window.ico')
assert (host/'01'/'window.ico').read_bytes()==(root/'host'/'AsterHub.Process.ico').read_bytes()
frames={};hashes=set();checks=0
for key in keys:
    folder=host/key
    with Image.open(folder/'window.ico') as icon:
        assert icon.ico.sizes()=={(n,n) for n in native_sizes}
        frames[key]={n:icon.ico.getimage((n,n)).convert('RGBA') for n in native_sizes}
    if key=='01':continue
    assert (folder/'app.ico').read_bytes()==(folder/'window.ico').read_bytes()
    assert (folder/'symbol.svg').read_bytes()==(web/key/'symbol.svg').read_bytes()
    for preview in [folder/'app-256.png',web/key/'preview.png']:
        assert Image.open(preview).convert('RGBA').tobytes()==frames[key][256].tobytes()
    for n,img in frames[key].items():
        mask=img.getchannel('A');assert mask.tobytes()==base.ico.getimage((n,n)).convert('RGBA').getchannel('A').tobytes()
        identity=sha256(img.tobytes()).hexdigest();assert identity not in hashes;hashes.add(identity);checks+=1
    if args.package:
        for p in folder.iterdir(): assert p.read_bytes()==(args.package/'resources'/'brand-icons'/key/p.name).read_bytes()
        for p in (web/key).iterdir(): assert p.read_bytes()==(args.package/'resources'/'workbench'/'brand-icons'/key/p.name).read_bytes()

args.output.parent.mkdir(parents=True,exist_ok=True)
sheet=Image.new('RGB',(960,458),'#171A21');d=ImageDraw.Draw(sheet)
reg=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n,index=0)
strong=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyhbd.ttc',n,index=0)
d.text((34,24),'AsterHub  /  应用图标',font=strong(29),fill='#f1f4fc')
d.text((35,70),'同一轮廓 · 四种配色。下方尺寸按像素原大显示。',font=reg(15),fill='#a7b1c5')
def paste(center,img):sheet.paste(img,(int(center[0]-img.width/2),int(center[1]-img.height/2)),img)
for index,key in enumerate(keys):
    x=34+index*225;w=214;cx=x+w/2
    d.rounded_rectangle((x,116,x+w,421),radius=16,fill='#21252E',outline='#323948')
    d.text((x+18,131),key+'  默认' if key=='01' else key,font=strong(15),fill='#b8c3d9')
    paste((cx,215),frames[key][128].resize((92,92),Image.Resampling.LANCZOS))
    d.rounded_rectangle((x+13,279,x+w-13,326),radius=8,fill='#101318')
    d.rounded_rectangle((x+13,332,x+w-13,379),radius=8,fill='#e6e9f0')
    for dx,n in zip([x+54,x+106,x+161],[16,20,32]):
        for y in (303,356):paste((dx,y),frames[key][n])
        d.text((dx,398),str(n)+' px',anchor='mm',font=reg(12),fill='#aebbd0')
sheet.save(args.output)
print(f'PASS icon families 10–12, {checks} distinct ICO frames, original outline at every size, exact app/tray and web preview identity')
