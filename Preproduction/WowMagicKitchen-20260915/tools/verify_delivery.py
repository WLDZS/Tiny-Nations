"""Read-only acceptance checks on the actual delivered files."""
from pathlib import Path
import json
import struct
import numpy as np
from PIL import Image
from scipy.io import wavfile

root=Path(__file__).resolve().parents[1]
results={}
for name in ['A','B']:
    im=Image.open(root/'concepts'/f'concept_{name}.png')
    results['concept_'+name+'_16_9']=im.width*9==im.height*16
data=(root/'sprites'/'chef_blockout.aseprite').read_bytes()
size,magic,frames,w,h=struct.unpack_from('<IHHHH',data,0)
results['aseprite_64x64_11frames']=(magic==0xa5e0 and frames==11 and w==64 and h==64 and size==len(data))
for p in (root/'sprites').glob('*.png'):
    a=np.asarray(Image.open(p))
    results['alpha_'+p.name]=(a.ndim==3 and a.shape[2]==4 and a[:,:,3].min()==0 and a[:,:,3].max()==255)
for p in (root/'modules').glob('*.obj'):
    vertices=[];faces=[];uv=[]
    for line in p.read_text().splitlines():
        parts=line.split()
        if parts and parts[0]=='v': vertices.append([float(s) for s in parts[1:]])
        if parts and parts[0]=='vt': uv.append([float(s) for s in parts[1:]])
        if parts and parts[0]=='f': faces.append([[int(i) for i in s.split('/')] for s in parts[1:]])
    v=np.array(vertices)
    valid=True
    for face in faces:
        idx=[i[0]-1 for i in face];tex=[i[1]-1 for i in face]
        if min(idx)<0 or max(idx)>=len(v) or min(tex)<0 or max(tex)>=len(uv): valid=False;continue
        points=v[idx];normal=np.cross(points[1]-points[0],points[2]-points[0])
        box_start=(idx[0]//8)*8;center=v[box_start:box_start+8].mean(axis=0)
        if np.dot(normal,points.mean(axis=0)-center)<=0: valid=False
    results['obj_outward_nonzero_faces_'+p.name]=valid
    results['obj_touches_ground_'+p.name]=abs(v[:,1].min())<1e-6
for name in ['A_prep_90bpm','B_service_110bpm']:
    sr,a=wavfile.read(root/'music'/(name+'_loop.wav'))
    _,repeat=wavfile.read(root/'music'/name/'listen_three_loops.wav')
    results[name+'_three_loops_identical']=repeat.shape[0]==a.shape[0]*3 and all(np.array_equal(repeat[i*len(a):(i+1)*len(a)],a) for i in range(3))
    results[name+'_pcm_stereo_48k_no_clipping']=sr==48000 and a.dtype==np.int16 and a.shape[1]==2 and np.max(np.abs(a.astype(float)))<32767
    stems=[]
    for part in ['plucked_strings','woodwind','celesta','wood_percussion']:
        _,stem=wavfile.read(root/'music'/name/(part+'.wav'));stems.append(stem.astype(np.int32))
    results[name+'_stems_sum_within_quantization']=np.max(np.abs(sum(stems)-a.astype(np.int32)))<=3
report=dict(checks=results,failed=[k for k,v in results.items() if not bool(v)])
(root/'qa'/'delivery_checks.json').write_text(json.dumps(report,indent=2,default=bool),encoding='utf-8')
print(json.dumps(dict(total_checks=len(results),failed=report['failed']),indent=2))
