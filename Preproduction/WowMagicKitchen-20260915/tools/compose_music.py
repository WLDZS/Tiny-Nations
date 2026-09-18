"""Original deterministic chamber-folk arrangement and synthesis; no sampled music.
48 kHz stereo PCM16. Periodic note tails and room taps, no whole-track fades.
Run with numpy/scipy. Score, MIDI and stems are also exported for revision.
"""
from pathlib import Path
import json
import math
import struct
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'music'
SR = 48000
RNG = np.random.default_rng(915)
PARTS = ['plucked_strings', 'woodwind', 'celesta', 'wood_percussion']

def tone(note, length, instrument):
    freq = 440 * 2 ** ((note-69)/12)
    release = {'plucked_strings': .85, 'woodwind': .10, 'celesta': 1.2, 'wood_percussion': .12}[instrument]
    t = np.arange(round((length+release)*SR))/SR
    if instrument == 'plucked_strings':
        signal = np.zeros(len(t))
        for h in range(1, 17):
            # Plucked string's high harmonics decay faster; slight natural inharmonicity.
            gain = np.sin(h*np.pi*.23) / h**1.35
            decay = np.exp(-t*(2.2 + h*.6))
            signal += gain*decay*np.sin(2*np.pi*freq*h*(1+.000025*h*h)*t)
        body = .10*np.sin(2*np.pi*freq*t)*np.exp(-t*3)
        signal += body
        env = np.minimum(t/.004,1) * np.minimum(np.maximum((length+release-t)/.04,0),1)
    elif instrument == 'woodwind':
        vibrato = .0018*np.sin(2*np.pi*4.8*t)*np.minimum(t/.28,1)
        phase = 2*np.pi*np.cumsum(freq*(1+vibrato))/SR
        signal = np.sin(phase)+.26*np.sin(3*phase)+.095*np.sin(5*phase)+.025*np.sin(2*phase)
        breath = sosfilt(butter(2, 1700, fs=SR, output='sos'), RNG.normal(0,1,len(t)))
        signal += .035*breath
        env = np.minimum(t/.024,1)*np.minimum(np.maximum((length+.10-t)/.10,0),1)
        env *= .92+.08*np.cos(2*np.pi*t/np.maximum(length,.1))
    elif instrument == 'celesta':
        signal = np.zeros(len(t))
        for ratio,gain,decay in [(1,1,3.5),(2,.22,6),(3.98,.07,9)]:
            signal += gain*np.sin(2*np.pi*freq*ratio*t)*np.exp(-t*decay)
        env = np.minimum(t/.003,1)*np.minimum(np.maximum((length+release-t)/.04,0),1)
    else:
        # Quiet pitched wood, not an order bell or kitchen event sound.
        signal = np.sin(2*np.pi*freq*t)*np.exp(-t*65)
        signal += .25*np.sin(2*np.pi*freq*1.61*t)*np.exp(-t*95)
        env = np.minimum(t/.0015,1)*np.minimum(np.maximum((length+.12-t)/.015,0),1)
    signal = signal*env
    peak = np.max(np.abs(signal))
    return signal/max(peak, .001)

def write_midi(events, bpm, path):
    def var(v):
        vals=[v&127];v>>=7
        while v: vals.insert(0,(v&127)|128);v>>=7
        return bytes(vals)
    tracks=[]
    tempo=round(60000000/bpm)
    conductor=b'\x00\xff\x51\x03'+tempo.to_bytes(3,'big')
    conductor+=b'\x00\xff\x58\x04\x04\x02\x18\x08'
    conductor+=b'\x00\xff\x59\x02\x01\x00' # G major
    conductor+=var(64*480)+b'\xff\x2f\x00'
    tracks.append(conductor)
    for channel,(name,program) in enumerate(zip(PARTS,[24,71,8,115])):
        records=[(0,bytes([0xc0+channel,program]))]
        for e in events:
            if e['part']!=name: continue
            vel=max(1,min(110,round(e['velocity']*100)))
            records.append((round(e['beat']*480),bytes([0x90+channel,e['note'],vel])))
            records.append((min(64*480,round((e['beat']+e['duration_beats'])*480)),bytes([0x80+channel,e['note'],0])))
            if e['beat']+e['duration_beats']>64:
                records.append((0,bytes([0x90+channel,e['note'],vel])))
                records.append((round((e['beat']+e['duration_beats']-64)*480),bytes([0x80+channel,e['note'],0])))
        records.sort(key=lambda z:(z[0],z[1][0]&0xf0==0x90))
        data=b'';last=0
        for tick,msg in records: data+=var(tick-last)+msg;last=tick
        data+=var(64*480-last)+b'\xff\x2f\x00';tracks.append(data)
    payload=b'MThd'+struct.pack('>IHHH',6,1,len(tracks),480)
    for track in tracks: payload+=b'MTrk'+struct.pack('>I',len(track))+track
    path.write_bytes(payload)

def make_score(busy):
    events=[]
    def add(part,beat,note,dur,vel):
        events.append(dict(part=part,beat=beat,note=note,duration_beats=dur,velocity=vel))
    # I - vi - IV - V. Second half changes route, ending V to resolve into loop I.
    chords=[(43,55,59,62),(40,55,59,64),(36,55,60,64),(38,54,57,62),
            (43,55,59,62),(47,54,59,62),(36,55,60,64),(38,54,57,62),
            (40,55,59,64),(36,55,60,64),(43,55,59,62),(38,54,57,62),
            (36,55,60,64),(43,55,59,62),(45,57,60,64),(38,54,57,62)]
    # Original short question/answer motif, G major; both cues use this same score core.
    phrases={
      0:[(0,74,.7),(.9,76,.4),(1.5,79,.7),(2.5,78,.4),(3.1,76,.6)],
      1:[(.5,74,.7),(1.5,71,.65),(2.5,67,.85)],
      2:[(.5,72,.6),(1.5,76,.5),(2.3,74,1.0)],
      3:[(1,69,.6),(2,74,.5),(3,78,.6)],
      4:[(0,74,.7),(.9,76,.4),(1.5,79,.7),(2.5,78,.4),(3.1,76,.6)],
      5:[(.5,74,.6),(1.5,71,1.1)],
      6:[(.5,72,.6),(1.5,76,.6),(2.5,79,.7)],
      7:[(.5,78,.55),(1.5,74,.8)],
      8:[(.5,76,.6),(1.5,79,.55),(2.5,78,.75)],
      9:[(1,76,.6),(2,72,.9)],
      10:[(.5,71,.6),(1.5,74,.6),(2.5,79,.8)],
      11:[(1,78,.6),(2,74,.8)],
      12:[(.5,76,.6),(1.5,74,.55),(2.5,72,.7)],
      13:[(.5,71,.6),(1.5,74,.6),(2.5,79,.8)],
      14:[(1,76,.6),(2,72,.8)],
      15:[(.5,69,.55),(1.5,74,.6),(2.5,78,.65)]}
    for bar,chord in enumerate(chords):
        start=bar*4
        add('plucked_strings',start,chord[0],1.5,.46 if bar==0 else .60)
        add('plucked_strings',start+2,chord[0]+7,1.25,.39)
        pattern=[(.5,1),(1,2),(1.5,3),(2.5,2),(3,1),(3.5,3)] if busy else [(.75,1),(1.5,2),(2.5,3),(3.25,2)]
        for b,k in pattern: add('plucked_strings',start+b,chord[k],.65,.31 if busy else .29)
        # Breath gaps: prep omits 4 response bars; busy returns those phrases, shorter and sprung.
        if busy or bar not in (2,5,9,12):
            for b,n,d in phrases[bar]:
                if bar==0 and b==0: continue # loop pickup supplies this theme note
                b=round(b*2)/2 if busy else b
                add('woodwind',start+b,n,d*(.64 if busy else .95),.30 if busy else .28)
        if bar in (1,3,6,9,11,14):
            add('celesta',start+3.5,chord[2]+24,.3,.10)
        if busy:
            for b,n,v in [(0,72,.10),(1.5,77,.065),(2,72,.09),(3.5,79,.07)]:
                add('wood_percussion',start+b,n,.04,v)
        elif bar%2==1:
            add('wood_percussion',start+2,72,.04,.045)
    # A real dominant-to-tonic pickup sustains across the seam. No master envelope.
    add('woodwind',63.5,74,.98,.29)
    add('plucked_strings',63.5,62,.65,.31)
    return events

def render(name,bpm,busy):
    beatsec=60/bpm;n=round(64*beatsec*SR)
    events=make_score(busy)
    stems={p:np.zeros((n,2),np.float64) for p in PARTS}
    pans={'plucked_strings':-.22,'woodwind':.15,'celesta':.34,'wood_percussion':-.12}
    for e in events:
        note=tone(e['note'],e['duration_beats']*beatsec,e['part'])*e['velocity']
        pan=pans[e['part']];stereo=note[:,None]*np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)])
        idx=(round(e['beat']*beatsec*SR)+np.arange(len(note)))%n
        np.add.at(stems[e['part']],idx,stereo)
    # Small room made periodic by circular taps. This is not a crossfade.
    for p in PARTS:
        dry=stems[p].copy()
        for delay,gain in [(.047,.08),(.083,.055),(.139,.035),(.227,.018)]:
            stems[p]+=np.roll(dry,round(delay*SR),axis=0)[:,::-1]*gain
    mix=sum(stems.values())
    rms=np.sqrt(np.mean(mix**2));gain=10**(-21/20)/rms
    gain=min(gain,10**(-3/20)/np.max(np.abs(mix)))
    for p in PARTS: stems[p]*=gain
    mix=sum(stems.values())
    def write(path,a): wavfile.write(path,SR,np.round(np.clip(a,-1,1)*32767).astype(np.int16))
    folder=OUT/name;folder.mkdir(exist_ok=True)
    write(OUT/(name+'_loop.wav'),mix)
    write(folder/'listen_three_loops.wav',np.tile(mix,(3,1)))
    # 4 s before + 4 s after boundary for quick listening; no extra processing.
    write(folder/'seam_audition.wav',np.concatenate([mix[-4*SR:],mix[:4*SR]]))
    for p,a in stems.items(): write(folder/(p+'.wav'),a)
    payload=dict(name=name,bpm=bpm,meter='4/4',key='G major',bars=16,sample_rate=SR,samples=n,
                 loop_start_sample=0,loop_end_exclusive=n,events=events,
                 original_theme_midi=[74,76,79,78,76,74,71,67],synthesis='local original additive/plucked timbres, not recorded instruments')
    (folder/'score.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
    write_midi(events,bpm,folder/'arrangement.mid')
    # Verify encoded PCM too, because rounding changes boundary deltas.
    _,pcm=wavfile.read(OUT/(name+'_loop.wav'));a=pcm.astype(float)/32768
    diff=np.abs(np.diff(a,axis=0));seam=np.abs(a[0]-a[-1])
    rms100=[float(np.sqrt(np.mean(a[i:i+4800]**2))) for i in range(0,n-4800,4800)]
    before=np.sqrt(np.mean(a[-4800:]**2));after=np.sqrt(np.mean(a[:4800]**2))
    metrics=dict(seconds=n/SR,bpm=bpm,bars=16,channels=2,sample_rate=SR,pcm_bits=16,
      peak_dbfs=float(20*np.log10(np.max(np.abs(a)))),rms_dbfs=float(20*np.log10(np.sqrt(np.mean(a*a)))),
      clipped_samples=int(np.sum(np.abs(pcm)>=32767)),dc_offset=np.mean(a,axis=0).tolist(),
      boundary_step=seam.tolist(),boundary_step_dbfs=(20*np.log10(np.maximum(seam,1e-12))).tolist(),
      adjacent_step_p99=np.quantile(diff,.99,axis=0).tolist(),
      seam_100ms_rms_delta_db=float(20*np.log10(after/before)),
      min_100ms_rms_dbfs=float(20*np.log10(min(rms100))),
      duration_error_samples=n-64*60/bpm*SR,
      tail_method='All notes and room tails wrap modulo the exact loop length; no track fade.',
      listening_status='Numeric checks passed when stated in report; subjective repeated-listening fatigue requires human audition.')
    return name,metrics

if __name__=='__main__':
    OUT.mkdir(exist_ok=True)
    results=dict([render('A_prep_90bpm',90,False),render('B_service_110bpm',110,True)])
    (ROOT/'qa'/'audio_metrics.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(json.dumps(results,indent=2))
