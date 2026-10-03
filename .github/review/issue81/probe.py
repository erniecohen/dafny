"""Diagnostic only: record every result, including expected failures, and exit zero."""
import json
import os
from pathlib import Path
import subprocess

out = Path('issue81-results')
out.mkdir(exist_ok=True)
source = Path('Source/DafnyCore/DafnyPrelude.bpl').read_text()
heap = source[source.index('type Heap ='):source.index('function $HeapSuccGhost(Heap, Heap): bool;')]
# The production declaration of $Heap needs symbols outside the reduced theory.
heap = heap.replace('var $Heap: Heap where $IsGoodHeap($Heap) && $IsHeapAnchor($Heap);', '')
boxing = '''type ref;
type Field;
type Box;
const unique alloc: Field;
function $Box<T>(T): Box;
function $Unbox<T>(Box): T;
axiom (forall<T> x: T :: { $Box(x) } $Unbox($Box(x)) == x);
'''
original = '$IsGoodHeap(update(h, r, f, x)) ==>'
guard = '(f != alloc || (($Unbox(read(h, r, alloc)): bool) ==> ($Unbox(x): bool))) &&\n  ' + original
assert source.count(original) == 1

restore = '''
procedure Restore(h: Heap, r: ref) {
  var oldBox: Box;
  var allocated, restored: Heap;
  assume $IsGoodHeap(h);
  assume !($Unbox(read(h, r, alloc)): bool);
  oldBox := read(h, r, alloc);
  allocated := update(h, r, alloc, $Box(true));
  restored := update(allocated, r, alloc, oldBox);
  // Explicit terms expose the applicable quantifier instances.
  assert restored == h;
  assert $HeapSucc(allocated, restored);
  assert false;
}
'''
control = '''
procedure Control(h: Heap, r: ref) {
  var allocated: Heap;
  assume $IsGoodHeap(h);
  assume !($Unbox(read(h, r, alloc)): bool);
  allocated := update(h, r, alloc, $Box(true));
  assume $IsGoodHeap(allocated);
  assert $HeapSucc(h, allocated);
  assert ($Unbox(read(allocated, r, alloc)): bool);
  assert false;
}
'''
modes = {'arrays': ['/typeEncoding:m'], 'array-axioms': ['/typeEncoding:m', '/useArrayAxioms'], 'arguments': ['/typeEncoding:a']}
rows = []
for scope, text in [('reduced', boxing + heap), ('full', source)]:
    for variant in ['original', 'guarded']:
        prelude = text if variant == 'original' else text.replace(original, guard)
        for name, procedure in [('restore', restore), ('control', control)]:
            bpl = out / f'{scope}-{variant}-{name}.bpl'
            bpl.write_text(prelude + procedure)
            for mode, flags in modes.items():
                stem = f'{scope}-{variant}-{name}-{mode}'
                command = ['.tools/boogie', '/nologo', '/errorTrace:0', '/timeLimit:15', '/vcsCores:1', '/proverOpt:PROVER_PATH=' + os.environ['Z3'], '/proverLog:' + str(out / (stem + '.smt2')), *flags, str(bpl)]
                row = {'case': stem, 'command': command}
                try:
                    result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=25)
                    row.update(exit=result.returncode, output=result.stdout)
                except subprocess.TimeoutExpired as e:
                    row.update(exit='process-timeout', output=(e.stdout or b'').decode() if isinstance(e.stdout, bytes) else e.stdout or '')
                (out / (stem + '.txt')).write_text(row['output'])
                rows.append(row)
                (out / 'results.json').write_text(json.dumps(rows, indent=2))
                print(stem, row['exit'], row['output'].strip().splitlines()[-1:] , flush=True)
summary = '# Issue 81 diagnostic results\n\nThese are diagnostic outcomes, not an acceptance gate.\n\n'
for row in rows:
    last = row['output'].strip().splitlines()[-1:] or ['No output']
    summary += f"- {row['case']}: exit {row['exit']}; {last[0]}\n"
Path(os.environ['GITHUB_STEP_SUMMARY']).write_text(summary)
