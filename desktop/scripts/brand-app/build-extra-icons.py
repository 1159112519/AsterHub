"""Rebuild the additional icons without modifying the approved 01–09 families.

Uses the 01 geometry as the vector template and the approved Windows frame's
alpha silhouette at each native size. Pillow and NumPy are required.
"""
from pathlib import Path
import importlib.util
import xml.etree.ElementTree as ET
from PIL import Image

BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[1]
HOST=ROOT/'host'/'brand-icons'
WEB=ROOT/'web'/'public'/'brand-icons'
NS={'svg':'http://www.w3.org/2000/svg'}
ET.register_namespace('', NS['svg'])
spec=importlib.util.spec_from_file_location('approved_geometry', BASE/'build-app-icons.py')
art=importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)
PALETTES={
 '10': {'body':['#3D4048','#171B23'],'wave':['#E6FC7B','#AEE754','#6FDD95','#4BC5BD'],'ports':['#DEE4EE','#FFBA76','#8D9DBB','#B8A5FF']},
 '11': {'body':['#9A4F4D','#50283D'],'wave':['#F4F7FE','#A2E5FF','#85CAFF','#ADB8FF'],'ports':['#FFD4AB','#DDF7D9','#F0ACAF','#FFDD95']},
 '12': {'body':['#DFEFE4','#AACCC5'],'wave':['#166B5B','#167891','#365AC4','#6450A2'],'ports':['#BA5135','#117369','#275477','#AD496C']},
}

def vector(palette):
    root=ET.fromstring((HOST/'01'/'symbol.svg').read_text(encoding='utf-8'))
    for gradient in root.findall('.//svg:linearGradient', NS):
        for key in ('x1','x2','y1','y2'):
            gradient.set(key, str(float(gradient.get(key))*100)+'%')
        for stop, color in zip(gradient, palette[gradient.get('id')]):
            stop.set('stop-color',color)
    ports=[node for node in root if node.tag.rsplit('}',1)[-1] in ('ellipse','rect')][1:]
    assert len(ports)==4
    for port,color in zip(ports,palette['ports']): port.set('fill',color)
    return ET.tostring(root,encoding='unicode')+'\n'

def raster(svg,palette,size=1024):
    import numpy as np
    root=ET.fromstring(svg)
    nodes=list(root)[1:]
    plate=nodes.pop(0)
    y=float(plate.get('y')); h=float(plate.get('height'))
    axis=np.clip((np.linspace(0,100,size)-y)/h,0,1)
    a,b=(art.rgb(color) for color in palette['body'])
    row=a[None,:]+axis[:,None]*(b-a)[None,:]
    master=Image.fromarray(np.uint8(np.broadcast_to(row[:,None,:],(size,size,3)))).convert('RGBA')
    for node in nodes:
        name=node.tag.rsplit('}',1)[-1]
        f=lambda key: float(node.get(key))
        if name=='rect':
            x,y=f('x'),f('y'); bounds=(x,y,x+f('width'),y+f('height'))
            op=['rect',bounds,f('rx'),False]
        elif name=='ellipse':
            x,y,rx,ry=f('cx'),f('cy'),f('rx'),f('ry');op=['circle',(x-rx,y-ry,x+rx,y+ry),0,False]
        else:
            assert name=='polyline'
            op=['line',[tuple(map(float,p.split(','))) for p in node.get('points').split()],f('stroke-width'),False]
        master.alpha_composite(art.layer(op,size,palette['wave'] if name=='polyline' else node.get('fill')))
    return master

def render(master,approved):
    n=approved.width
    image=master.resize((n,n),Image.Resampling.LANCZOS)
    image.putalpha(approved.getchannel('A'))
    return image

def build():
    approved=Image.open(HOST/'01'/'window.ico')
    approved_512=Image.open(HOST/'01'/'app-512.png').convert('RGBA')
    for key,palette in PALETTES.items():
        host=HOST/key;web=WEB/key;host.mkdir(exist_ok=True);web.mkdir(exist_ok=True)
        svg=vector(palette)
        for path in (host,web): (path/'symbol.svg').write_text(svg,encoding='utf-8')
        master=raster(svg,palette)
        sizes=[n for n,m in sorted(approved.ico.sizes()) if n==m]
        frames=[(n, render(master,approved.ico.getimage((n,n)).convert('RGBA'))) for n in sizes]
        for name in ('app.ico','window.ico'): art.save_ico(host/name,frames)
        frames[-1][1].save(host/'app-256.png');frames[-1][1].save(web/'preview.png')
        render(master,approved_512).save(host/'app-512.png')
    print(f'ICON_VARIANTS={len(PALETTES)}; native frames per variant={len(sizes)}')

if __name__=='__main__': build()
