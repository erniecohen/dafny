#!/usr/bin/env python3
"""Check the exact public reused compiler archive and extracted files; pure."""
import json,sys
from pathlib import Path
from plan import HERE,digest
from prepare import components
record=json.loads((HERE/'current-final-archive.json').read_text())
archive,executable,output=map(Path,sys.argv[1:4])
if digest(archive)!=record['archive_sha256']:raise SystemExit('Reused archive hash differs')
root=executable.resolve().parent
actual={p.relative_to(root).as_posix():digest(p) for p in root.rglob('*') if p.is_file()}
if actual!=record['files_sha256'] or any(p.is_symlink() for p in root.rglob('*')):raise SystemExit('Reused extracted archive closure differs')
if components(executable)!=record['components_sha256']:raise SystemExit('Managed component identity differs')
record.update(line='shipped',side='final',product_revision=record['product'],source_model='Exact public build archive reuse',archive_sha256=digest(archive))
output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps(dict(files=len(actual),components=len(record['components_sha256']))))
