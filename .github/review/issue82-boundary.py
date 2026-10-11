#!/usr/bin/env python3
"""Replay the conditional diagonal boundary control for issue #82.

This deliberately small, ground SMT fragment is not a translation of a Dafny
source program and does not certify all prelude axioms. The diagonal equation
is an additional assumption, explicitly supplied here. Each equation is an
instance of one stated schema. Grounding removes quantifier search from this
regression; it does not add any new representation principle.

The repaired control includes active empty/singleton defining instances. Adding
one instance of the removed arbitrary-map bridge changes sat to unsat. Removing
the extra diagonal equation makes that old-bridge instance satisfiable again.
"""

import argparse
import json
import re
from pathlib import Path
import subprocess


def fixture(old_bridge: bool, diagonal: bool) -> str:
    text = """; Minimal conditional boundary regression, not the complete Dafny prelude.
(set-logic QF_AUF)
(set-option :produce-models true)
(set-option :rlimit 1000000)
(set-option :timeout 10000)
(declare-sort Box 0)
(declare-sort Set 0)
(declare-fun C ((Array Box Bool)) Set)
(declare-fun Member (Set Box) Bool)
(declare-fun BoxSet (Set) Box)
(declare-fun UnboxSet (Box) Set)
(declare-const diagonal (Array Box Bool))
(declare-const element Box)
(define-fun converted () Set (C diagonal))
(define-fun b () Box (BoxSet converted))
; Ground forward-boxing law: UnboxSet(BoxSet(s)) = s, at s = C(diagonal).
(assert (= (UnboxSet b) converted))
; Ordinary finite source instances are still present and active.
(define-fun emptyMap () (Array Box Bool) ((as const (Array Box Bool)) false))
(define-fun singletonMap () (Array Box Bool) (store emptyMap element true))
(assert (distinct b element))
(assert (= (Member (C emptyMap) b) (select emptyMap b)))
(assert (= (Member (C emptyMap) element) (select emptyMap element)))
(assert (= (Member (C singletonMap) b) (select singletonMap b)))
(assert (= (Member (C singletonMap) element) (select singletonMap element)))
"""
    if diagonal:
        text += """; EXTRA diagonal assumption, at b:
; select(diagonal, bx) = !Member(UnboxSet(bx), bx).
(assert (= (select diagonal b) (not (Member (UnboxSet b) b))))
"""
    if old_bridge:
        text += """; Mutation: ground instance of the removed unrestricted schema
; Member(C(m), bx) = select(m, bx), at m = diagonal and bx = b.
(assert (= (Member converted b) (select diagonal b)))
"""
    return text + "(check-sat)\n(get-info :all-statistics)\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--z3', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--emit-only', action='store_true')
    args = parser.parse_args()
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    cases = [('repaired', False, True, 'sat'),
             ('old-bridge-mutation', True, True, 'unsat'),
             ('old-bridge-without-extra-diagonal', True, False, 'sat')]
    report = []
    if not args.emit_only:
        version = subprocess.run([args.z3, '-version'], capture_output=True, text=True,
                                 timeout=10, check=True)
        (out / 'solver-version.txt').write_text(version.stdout + version.stderr)
        if 'Z3 version 5.1.0' not in version.stdout:
            raise RuntimeError('This regression requires Z3 5.1.0')
    failed = False
    for name, old_bridge, diagonal, expected in cases:
        dest = out / name
        dest.mkdir(parents=True, exist_ok=True)
        source = dest / 'input.smt2'
        source.write_text(fixture(old_bridge, diagonal))
        command = [args.z3, '-smt2', str(source)]
        (dest / 'command.json').write_text(json.dumps(command, indent=2) + '\n')
        row = {'case': name, 'expected': expected, 'extra_diagonal_assumption': diagonal,
               'old_bridge_instance': old_bridge}
        if not args.emit_only:
            try:
                result = subprocess.run(command, capture_output=True, text=True, timeout=20)
                (dest / 'stdout.txt').write_text(result.stdout)
                (dest / 'stderr.txt').write_text(result.stderr)
                answer = next((line for line in result.stdout.splitlines()
                               if line in ('sat', 'unsat', 'unknown')), None)
                resource = re.search(r':rlimit-count\s+(\d+)', result.stdout)
                row.update(exit=result.returncode, answer=answer,
                           resource_count=int(resource[1]) if resource else None)
                row['pass'] = result.returncode == 0 and answer == expected
            except subprocess.TimeoutExpired:
                row.update(answer='wall-time safety cap', **{'pass': False})
            failed |= not row['pass']
        report.append(row)
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2), flush=True)
    return int(failed)


if __name__ == '__main__':
    raise SystemExit(main())
