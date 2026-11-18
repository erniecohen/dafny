"""Refinement must establish inherited global and callback bound obligations."""
from pathlib import Path
import re
import shutil
import subprocess
import sys

binary, solver, directory = [str(Path(p).resolve()) for p in sys.argv[1:]]
out = Path(directory)
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)
original = (Path(__file__).parent.parent / 'github-issue-493.dfy').read_text()
positive = original.split('module Later {')[0].replace(
    'function K(a: A, b: B, n: nat): int   // no bound',
    'function K(a: A, b: B, n: nat): int decreases {:global} n')
# Set the slot bounds explicitly: G's newly inherited n+1 is above K's n.
positive = positive.replace('function G(a: A, b: B, n: nat): int\n      decreases {:global} n',
                            'function G(a: A, b: B, n: nat): int\n      decreases {:global} n + 1')
positive += '''
module Use {
  import Tr
  import Top1
  class Leaf extends Tr.A {
    constructor () {}
    function K(a: Tr.A, b: Tr.B, n: nat): int
      decreases {:global} n
    { n }
  }
  method Exercise() {
    var c := new Top1.T();
    var k := new Leaf();
    c.UnfoldG(k, c, 0);
    assert c.G(k, c, 0) == 1;
    // CONTROL
  }
}
'''

def abstract_import(text):
    return text.replace('module Later {', 'abstract module Later {').replace(
        '  import Top1\n', '  import R : Top1\n').replace('new Top1.T()', 'new R.T()') + '''
module Concrete refines Later { import R = Top1 }
'''

trait = original.replace(
    '  class T extends Tr.B { constructor () {} }',
    '  trait T extends Tr.B {}\n  class CT extends T { constructor () {} }').replace(
    'new Top1.T()', 'new Top1.CT()')
# These slots are themselves copied by refinement, so their bound expressions have
# inherited origins. Rechecking only bounds whose syntax is new would miss them.
callback = '''
module Base {
  trait B {
    function G(f: int -> int): int
      decreases {:global} 0 decreases {:callback f} 0
  }
  type T { function G(f: int -> int): int { f(0) } }
}
module Refined refines Base {
  class T extends B { constructor () {} }
}
'''
method = '''
module Base {
  trait A { function K(): int decreases {:global} 0 }
  trait B { method G(a: A) decreases {:global} 0 }
  type T { method G(a: A) { var x := a.K(); } }
}
module Refined refines Base {
  class T extends B { constructor () {} }
}
'''
callback_forward = '''
module Base {
  trait S { function Use(f: int -> int): int decreases {:global} 0 decreases {:callback f} 0 }
  type C { function Use(f: int -> int): int { 0 } }
  type T {
    function G(c: C, f: int -> int): int
      decreases {:global} 2 decreases {:callback f} 1 { c.Use(f) }
  }
}
module Refined refines Base {
  class C extends S { constructor () {} }
}
'''
bounded_apply = '''
module Base {
  trait B {
    function G(c: bounded<nat, (int -> int)>): int
      requires c.bound == 0 decreases {:global} 0
  }
  type T {
    function G(c: bounded<nat, (int -> int)>): int
      requires c.bound == 0 { c.erase(0) }
  }
}
module Refined refines Base {
  class T extends B { constructor () {} }
}
'''
bounded_forward = callback_forward.replace(
    'G(c: C, f: int -> int)', 'G(c: C, f: bounded<nat, (int -> int)>)').replace(
    'decreases {:global} 2 decreases {:callback f} 1', 'requires f.bound == 1').replace(
    'c.Use(f)', 'c.Use(f.erase)')

for refresh in [False, True]:
    label = 'refresh' if refresh else 'legacy'
    options = ['--solver-path', solver, '--cores', '1', '--resource-limit', '16000000',
               '--type-system-refresh:' + str(refresh).lower(), '--use-basename-for-filename']
    def run(name, text, expected=0, contains=(), extra=()):
        source = out / (label + '-' + name + '.dfy')
        source.write_text(text)
        result = subprocess.run(['dotnet', binary, 'verify'] + options + list(extra) + [str(source)],
                                capture_output=True, text=True)
        output = result.stdout + result.stderr
        (out / (label + '-' + name + '.log')).write_text(output)
        assert result.returncode == expected and all(x in output for x in contains), output
        assert not re.search(r'out of resource|timed out|Unhandled exception', output, re.I), output
        print(label + ': ' + name + ' PASS')
        return output
    for name, source, extra in [
        ('original', original, []),
        ('abstract-base', original.replace('module Top0 {', 'abstract module Top0 {'), []),
        ('trait-parent', trait, []),
        ('export-view', original.replace('module Top1 refines Top0 {',
                                         'module Top1 refines Top0 {\n  export reveals *'), []),
        ('abstract-import', abstract_import(original), []),
        ('method', method, []),
        ('callback', callback, []),
        ('callback-forward', callback_forward, []),
        ('bounded-apply', bounded_apply, []),
        ('bounded-forward', bounded_forward, []),
    ]:
        run(name, source, 4, ['Error: decreases clause might not decrease'], extra)
    run('positive', positive)
    text = run('false', positive.replace('// CONTROL', 'assert false;'), 4,
               ['Error: assertion might not hold', '1 error'])
    assert text.count('Error: assertion might not hold') == 1, text
    run('legacy-opt-out', original, 2, ["class 'T' is in a different module"],
        ['--legacy-no-modular-termination'])
    run('old-recursion-opt-out', original, 4, ['Error: decreases clause might not decrease'],
        ['--legacy-inherited-termination-checks'])
