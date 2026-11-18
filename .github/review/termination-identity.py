#!/usr/bin/env python3
"""Identity control for termination across modules by default (https://github.com/erniecohen/dafny/issues/211).

Claim: the default rules change nothing for a program that does not use the design, and the hidden
--legacy-no-modular-termination restores the base exactly.

  boogie BASE_DLL BRANCH_DLL LIT_ROOT OUT [--jobs N]
      Translates every .dfy under LIT_ROOT with each build (legacy CLI, /noVerify, so nothing is verified),
      under the legacy and the refreshed type system, and compares the diagnostics, the exit code and every
      printed Boogie file.  Both builds read the same files, so paths agree.  It checks:
        (a) branch with /legacyNoModularTermination:1 == base, for every program;
        (b) branch by default == base, unless the program uses the design.  A program uses the design when
            its text mentions {:global}, {:callback} or termination_closed, or when its legacy-off run
            reports an unmarked extension of a trait of another module.  The oracle is independent of the
            code under test.
      Exit 0 when both hold.

  ru BASE_RESULTS BRANCH_RESULTS
      Compares two `lit-verdicts.py run --measurements` directories: per program, the outcome and the
      resource count of every verification result.  Exit 0 when they agree.
"""
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

CONFIGS = {
    'legacy-types': [],
    'refreshed-types': ['/typeSystemRefresh:1', '/generalTraits:datatype', '/generalNewtypes:1'],
}
DESIGN = re.compile(r'\{:global\}|\{:callback\}|termination_closed')
CROSS = 'is in a different module than trait'


def translate(dll, source, flags, scratch):
    with tempfile.TemporaryDirectory(dir=scratch) as work:
        bpl = os.path.join(work, 'p.bpl')
        try:
            run = subprocess.run(['dotnet', dll, '/compile:0', '/noVerify', '/useBaseNameForFileName',
                                  '/print:' + bpl] + flags + [str(source)],
                                 cwd=source.parent, capture_output=True, text=True, timeout=300)
            out, code = run.stdout + run.stderr, run.returncode
        except subprocess.TimeoutExpired:
            out, code = 'TIMEOUT', -1
        # Stack traces of crashes name source lines of the build; keep only the message lines.
        lines = [l for l in out.splitlines() if 'deprecated' not in l and not l.lstrip().startswith('at ')]
        digest = hashlib.sha256()
        for name in sorted(os.listdir(work)):
            # The header records the command line, which names the scratch file and the flags compared.
            text = Path(work, name).read_bytes().split(b'\n')
            text = [l for l in text if not l.startswith(b'// Command Line Options:')]
            digest.update(name.encode() + b'\0' + b'\n'.join(text))
        return {'exit': code, 'out': '\n'.join(lines), 'boogie': digest.hexdigest()}


def boogie(base, branch, root, out, jobs):
    root, out = Path(root).resolve(), Path(out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    scratch = out / 'work'
    scratch.mkdir(exist_ok=True)
    sources = sorted(p for p in root.rglob('*.dfy') if 'Output' not in p.parts)

    cache = out / 'rows'
    cache.mkdir(exist_ok=True)

    def one(source):
        # Each translation is kept, by build role, so an interrupted run resumes where it stopped, and a rebuilt
        # branch reuses the base's translations once its own are cleared.
        name = str(source.relative_to(root))
        key = hashlib.sha256(name.encode()).hexdigest() + '.json'
        row = {}
        for config, flags in CONFIGS.items():
            row[config] = {}
            for role, dll, extra in (('base', base, []), ('default', branch, []),
                                     ('legacy-off', branch, ['/legacyNoModularTermination:1'])):
                kept = cache / config / role / key
                if kept.exists():
                    row[config][role] = json.loads(kept.read_text())
                else:
                    row[config][role] = translate(dll, source, flags + extra, scratch)
                    kept.parent.mkdir(parents=True, exist_ok=True)
                    kept.write_text(json.dumps(row[config][role]))
        text = source.read_text(errors='replace')
        row['uses'] = bool(DESIGN.search(text)) or any(CROSS in row[c]['legacy-off']['out'] for c in CONFIGS)
        return name, row

    with ThreadPoolExecutor(jobs) as pool:
        rows = dict(pool.map(one, sources))
    (out / 'rows.json').write_text(json.dumps(rows, indent=1))

    failures, changed = [], []
    for name, row in rows.items():
        for config in CONFIGS:
            r = row[config]
            if r['legacy-off'] != r['base']:
                failures.append('(a) %s [%s]: legacy-off differs from the base' % (name, config))
            if r['default'] != r['base']:
                if row['uses']:
                    changed.append('%s [%s]' % (name, config))
                else:
                    failures.append('(b) %s [%s]: changed by default, but does not use the design' % (name, config))
    uses = sum(1 for r in rows.values() if r['uses'])
    print('programs: %d; configurations: %d; translations: %d' % (len(rows), len(CONFIGS), 3 * len(CONFIGS) * len(rows)))
    print('(a) legacy-off identical to the base: %d of %d' % (len(rows) * len(CONFIGS) - sum(
        1 for f in failures if f.startswith('(a)')), len(rows) * len(CONFIGS)))
    print('(b) programs that use the design: %d; their translations changed by default: %d' % (uses, len(changed)))
    print('(b) programs that do not use the design, changed by default: %d' % sum(
        1 for f in failures if f.startswith('(b)')))
    for c in changed:
        print('  changed (uses the design): ' + c)
    for f in failures:
        print('FAILURE ' + f)
    return 1 if failures else 0


def ru(base, branch):
    def load(directory):
        result = {}
        for f in Path(directory).rglob('results.json'):
            text = f.read_text().strip()
            data = json.loads(text) if text else {}
            result[str(f.parent.relative_to(directory))] = sorted(
                (r.get('name', ''), r.get('outcome', ''), r.get('resourceCount', 0))
                for r in data.get('verificationResults', []))
        return result
    a, b = load(base), load(branch)
    differ = [k for k in sorted(set(a) | set(b)) if a.get(k) != b.get(k)]
    total = lambda m: sum(c for v in m.values() for (_, _, c) in v)
    print('programs: base %d, branch %d; resource units: base %d, branch %d; differing programs: %d'
          % (len(a), len(b), total(a), total(b), len(differ)))
    for k in differ:
        print('  differs: ' + k)
    return 1 if differ else 0


if __name__ == '__main__':
    args = sys.argv[1:]
    jobs = 3
    if '--jobs' in args:
        i = args.index('--jobs')
        jobs = int(args[i + 1])
        del args[i:i + 2]
    if args and args[0] == 'boogie' and len(args) == 5:
        sys.exit(boogie(*args[1:], jobs))
    if args and args[0] == 'ru' and len(args) == 3:
        sys.exit(ru(*args[1:]))
    sys.exit(__doc__)
