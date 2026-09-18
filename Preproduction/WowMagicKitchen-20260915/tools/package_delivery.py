from pathlib import Path
import hashlib
import json
import zipfile

root=Path(__file__).resolve().parents[1]
paths=sorted(p for p in root.rglob('*') if p.is_file() and '__pycache__' not in p.parts
             and not p.name.endswith('.log') and p.name!='SHA256SUMS.txt')
checksums=[]
for p in paths:
    checksums.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(root).as_posix())
(root/'SHA256SUMS.txt').write_text('\n'.join(checksums)+'\n',encoding='utf-8')
paths.append(root/'SHA256SUMS.txt')
archive=root.parent/(root.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=5) as z:
    for p in paths: z.write(p,root.name+'/'+p.relative_to(root).as_posix())
with zipfile.ZipFile(archive) as z: bad=z.testzip()
print(json.dumps(dict(archive=str(archive),files=len(paths),zip_mb=round(archive.stat().st_size/1024**2,2),crc_failure=bad),ensure_ascii=False))
