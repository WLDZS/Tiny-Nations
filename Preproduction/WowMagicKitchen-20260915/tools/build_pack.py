"""Package geometry, manifests and read-only image/audio QA. Never edits rasters."""
from pathlib import Path
import json
import base64
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
M=ROOT/'modules'

def box(name,center,size,material):
    x,y,z=center;w,h,d=[v/2 for v in size]
    v=[[x-w,y-h,z-d],[x+w,y-h,z-d],[x+w,y-h,z+d],[x-w,y-h,z+d],
       [x-w,y+h,z-d],[x+w,y+h,z-d],[x+w,y+h,z+d],[x-w,y+h,z+d]]
    return dict(name=name,vertices=v,faces=[[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]],material=material)

modules={
 'floor_tile':[box('tile',(0,.035,0),(.98,.07,.98),'ceramic')],
 'wood_counter':[box('cabinet',(0,.395,0),(.90,.79,.90),'wood'),
                 box('countertop',(0,.84,0),(1,.10,1),'ceramic'),
                 box('front_panel',(0,.46,.46),(.72,.49,.035),'wood'),
                 box('handle',(0,.60,.50),(.18,.035,.05),'terracotta')],
 'serving_hatch':[box('left_post',(-.69,1.13,0),(.14,2.26,.18),'wood'),
                  box('right_post',(.69,1.13,0),(.14,2.26,.18),'wood'),
                  box('header',(0,2.19,0),(1.52,.14,.20),'wood'),
                  box('sill',(0,.90,.12),(1.62,.12,.58),'wood'),
                  box('awning',(0,2.28,.24),(1.68,.10,.70),'terracotta'),
                  box('awning_edge',(0,2.17,.55),(1.68,.16,.08),'terracotta')]
}
mtl=[]
for material in ['ceramic','wood','terracotta','sage']:
    mtl.extend([f'newmtl {material}','Ka 0.2 0.2 0.2','Kd 1 1 1','Ks 0 0 0','d 1','illum 1',f'map_Kd {material}_64.png',''])
(M/'kitchen.mtl').write_text('\n'.join(mtl),encoding='ascii')
meshqa={}
for name,objects in modules.items():
    lines=['mtllib kitchen.mtl'];offset=0;texoffset=0
    for b in objects:
        lines.append('o '+b['name']);lines.append('usemtl '+b['material'])
        for v in b['vertices']: lines.append('v '+' '.join(f'{x:.4f}' for x in v))
        lines.extend(['vt 0 0','vt 1 0','vt 1 1','vt 0 1'])
        for face in b['faces']:
            # Reverse screen-projection winding for outward-facing right-handed OBJ.
            for indices in [(0,2,1),(0,3,2)]:
                lines.append('f '+' '.join(f'{face[i]+offset+1}/{texoffset+i+1}' for i in indices))
        offset+=8;texoffset+=4
    (M/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='ascii')
    meshqa[name]=dict(vertices=offset,triangles=len(objects)*12,pivot='bottom center, y=0',units='meters',separate_boxes=len(objects))
(M/'geometry.json').write_text(json.dumps(modules,indent=2),encoding='utf-8')

qa={}
for path in sorted((ROOT/'sprites').glob('*.png')):
    im=Image.open(path);a=np.asarray(im.convert('RGBA'));mask=a[:,:,3]>0
    yy,xx=np.nonzero(mask)
    qa[path.name]=dict(size=list(im.size),mode=im.mode,alpha_min=int(a[:,:,3].min()),alpha_max=int(a[:,:,3].max()),
        visible_pixels=int(mask.sum()),partial_alpha_pixels=int(((a[:,:,3]>0)&(a[:,:,3]<255)).sum()),
        rgba_colors=len(np.unique(a.reshape(-1,4),axis=0)),bounds=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)],
        clipped=bool(mask[0].any() or mask[-1].any() or mask[:,0].any() or mask[:,-1].any()))
for state in ['normal','accident']:
    for kind in ['fire','ice']:
        a=np.asarray(Image.open(ROOT/'sprites'/f'{kind}_{state}.png'))
        qa[f'{kind}_{state}.png']['central_12x17_window_alpha_max']=int(a[6:23,10:22,3].max())
texture_qa={}
for p in M.glob('*_64.png'):
    a=np.asarray(Image.open(p));texture_qa[p.name]=dict(size=list(Image.open(p).size),
      left_right_edges_equal=bool(np.array_equal(a[:,0],a[:,-1])),top_bottom_edges_equal=bool(np.array_equal(a[0],a[-1])))
(ROOT/'qa'/'asset_metrics.json').write_text(json.dumps(dict(sprites=qa,meshes=meshqa,textures=texture_qa),indent=2),encoding='utf-8')

frames=[]
for index,name in enumerate(['idle_south','idle_east','idle_north','idle_west']+[f'walk_east_{i}' for i in range(1,5)]+[f'cast_east_{i}' for i in range(1,4)]):
    frames.append(dict(name=name,rect_top_left=[index*64,0,64,64],pivot_top_left_pixels=[32,58],
                       unity_pivot_normalized=[.5,.09375],duration_ms=120 if index<8 else [140,100,180][index-8]))
manifest=dict(title='WoW，魔法厨房｜小规模视听预研',version=1,
 concept_status='ImageGen visual targets only. No Unity renderer implementation claimed.',
 camera=dict(projection='orthographic',pitch_degrees=50,yaw_degrees=45,rotation_locked=True),
 pixel_scale=dict(chef_canvas=[64,64],chef_body_height=50,chef_ppu=32,small_sprite_canvas=[32,32],preview_canvas=[640,360]),
 chef=dict(status='cleaned pixel blockout, animation and turnaround still need art cleanup',sheet='sprites/chef_all.png',frames=frames,
           direction_definition='screen-facing south/east/north/west. Map world input to screen facing for fixed camera.'),
 slime=dict(path='sprites/slime_idle.png',pivot=[.5,.125],ppu=32,body_alpha=210),
 food=dict(paths=['sprites/bread_raw.png','sprites/bread_baked.png','sprites/bread_burnt.png'],pivot=[.5,.1875],ppu=32),
 fx=dict(paths=[f'sprites/{k}_{s}.png' for k in ['fire','ice'] for s in ['normal','accident']],pivot=[.5,.09375],ppu=32),
 modules=meshqa,pipeline='ImageGen references; Aseprite cleanup, pixel sampling and layered action blockouts; procedural OBJ meshes; local score and audio synthesis.',
 limitations=['No Unity import or Play Mode verification','No human repeated-listening fatigue approval','Not a complete gameplay package','Animation is a reusable-source blockout, not finished hand-drawn animation'])
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

# Embedded generated assets make the visual audition page portable and offline.
assets={}
for folder in ['concepts','sprites','modules']:
    for p in (ROOT/folder).glob('*.png'):
        if p.name.endswith('_source.png'): continue
        assets[folder+'/'+p.name]='data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode('ascii')
data='window.PACK='+json.dumps(dict(assets=assets,geometry=modules,manifest=manifest),ensure_ascii=False)+';'
(ROOT/'pack-data.js').write_text(data,encoding='utf-8')
print(json.dumps(dict(sprite_files=len(qa),clipped=[k for k,v in qa.items() if v['clipped']],alpha_fail=[k for k,v in qa.items() if v['mode']!='RGBA' or v['alpha_min']!=0],meshes=meshqa),indent=2))
