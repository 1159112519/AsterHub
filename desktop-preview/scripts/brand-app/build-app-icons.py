from pathlib import Path
import copy,json,io,struct
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(__file__).resolve().parent
ops=json.loads((SOURCE/'selected-geometry.json').read_text(encoding='utf8'))
V=json.loads((SOURCE/'palettes.json').read_text(encoding='utf8'))
def alpha(ops, size):
    s = 4
    im = Image.new('L', (size * s, size * s))
    dr = ImageDraw.Draw(im)
    k = size * s / 100
    for kind, data, r, cut in ops:
        ink = 0 if cut else 255
        if kind == 'rect':
            dr.rounded_rectangle(tuple((v * k for v in data)), radius=r * k, fill=ink)
        elif kind == 'circle':
            dr.ellipse(tuple((v * k for v in data)), fill=ink)
        elif kind == 'poly':
            dr.polygon([(x * k, y * k) for x, y in data], fill=ink)
        else:
            pts = [(x * k, y * k) for x, y in data]
            dr.line(pts, fill=ink, width=round(r * k), joint='curve')
            for x, y in pts:
                dr.ellipse((x - r * k / 2, y - r * k / 2, x + r * k / 2, y + r * k / 2), fill=ink)
    return im.resize((size, size), Image.Resampling.LANCZOS)

def rgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)])

def grad(size, colors, axis='x'):
    t = np.linspace(0, len(colors) - 1, size)
    a = np.array([rgb(c) for c in colors])
    out = np.stack([np.interp(t, np.arange(len(colors)), a[:, k]) for k in range(3)], axis=-1)
    arr = np.broadcast_to(out[None, :, :] if axis == 'x' else out[:, None, :], (size, size, 3))
    return Image.fromarray(np.uint8(arr)).convert('RGBA')

def layer(op, size, fill):
    item = copy.deepcopy(op)
    item[3] = False
    if isinstance(fill, list):
        xmin = min((p[0] for p in item[1])) - item[2] / 2
        xmax = max((p[0] for p in item[1])) + item[2] / 2
        t = np.clip((np.linspace(0, 100, size) - xmin) / (xmax - xmin), 0, 1) * (len(fill) - 1)
        cs = np.array([rgb(c) for c in fill])
        row = np.stack([np.interp(t, np.arange(len(fill)), cs[:, k]) for k in range(3)], axis=-1)
        im = Image.fromarray(np.uint8(np.broadcast_to(row[None, :, :], (size, size, 3)))).convert('RGBA')
    else:
        im = Image.new('RGBA', (size, size), fill)
    im.putalpha(alpha([item], size))
    return im

def mark(v, size):
    b = grad(size, v['body'], 'y')
    if v['key'] == 'zones':
        a = np.array(b)
        a[:round(size * 0.36), :, :3] = rgb(v['body'][0])
        a[round(size * 0.36):round(size * 0.7), :, :3] = rgb(v['body'][1])
        a[round(size * 0.7):, :, :3] = rgb(v['body'][2])
        b = Image.fromarray(a)
    b.putalpha(alpha([ops[0]], size))
    for idx, c in zip([1, 2, 4, 5], v['ports']):
        b.alpha_composite(layer(ops[idx], size, c))
    b.alpha_composite(layer(ops[3], size, v['wave']))
    return b


def render(v,size):
    image=mark(v,1024)
    image=image.crop(image.getchannel('A').getbbox())
    image.thumbnail((round(size*.91),round(size*.91)),Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',(size,size));canvas.alpha_composite(image,((size-image.width)//2,(size-image.height)//2))
    return canvas

def save_ico(path,images):
    chunks=[];entries=[];offset=6+16*len(images)
    for size,image in images:
        buffer=io.BytesIO();image.save(buffer,format='PNG');payload=buffer.getvalue();chunks.append(payload)
        entries.append(struct.pack('<BBBBHHII',size if size<256 else 0,size if size<256 else 0,0,0,1,32,len(payload),offset));offset+=len(payload)
    path.write_bytes(struct.pack('<HHH',0,1,len(images))+b''.join(entries)+b''.join(chunks))
if __name__=='__main__':
    for v in V:
        folder=ROOT/'host/brand-icons'/v['key']
        for name in ['app.ico','window.ico']:save_ico(folder/name,[(n,render(v,n)) for n in [16,20,24,32,40,48,64,128,256]])
        render(v,256).save(folder/'app.png')
    (ROOT/'host/AsterHub.ico').write_bytes((ROOT/'host/brand-icons/night/app.ico').read_bytes())
    render(V[0],256).save(ROOT/'host/AsterHub.png')
    print('TRANSPARENT_APP_ICON_SETS=6')
