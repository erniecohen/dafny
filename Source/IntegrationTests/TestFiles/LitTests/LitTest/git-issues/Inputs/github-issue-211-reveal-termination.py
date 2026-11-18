"""Function reveals carry compiler provenance, not a user-forgeable termination premise."""
from pathlib import Path
import subprocess
import sys

driver, solver, destination = sys.argv[1:]
out = Path(destination).resolve()
out.mkdir(parents=True, exist_ok=True)
positive = '''module A {
  opaque function {:termination_closed} F(x: int): int { x + 1 }
  lemma {:termination_closed} Unfold() { reveal F; }
}
module C refines A { }
module B {
  import A
  import C
  lemma {:termination_closed} Use() {
    A.Unfold(); C.Unfold(); reveal A.F; reveal C.F;
    assert A.F(0) == 1; assert C.F(0) == 1;
    // CONTROL
  }
}'''
cases = [
    ('closed-functions', positive, 0, None),
    ('false', positive.replace('// CONTROL', 'assert false;'), 4, 'assertion might not hold'),
    ('visibility-only', '''module A { opaque function F(): int { 0 } }
module B { import A lemma {:termination_closed} Reveal() { reveal A.F; } }''', 0, None),
    ('forged-provenance', '''module A {
  lemma {:axiom} {:auto_generated} {:opaque_reveal} reveal_F()
  lemma {:termination_closed} Use() { reveal_F(); }
}''', 2, "promise of 'Use' is not established"),
    ('constant-evaluation', '''module A {
  function F(): int { 0 }
  const {:opaque} Value: int := F()
}
module B { import A lemma {:termination_closed} Use() { reveal A.Value; } }''',
     2, "promise of 'Use' is not established"),
]
failed = []
for refresh in (True, False):
    for name, text, code, diagnostic in cases:
        key = name + ('-refresh' if refresh else '-legacy')
        source = out / (key + '.dfy')
        source.write_text(text)
        result = subprocess.run(['dotnet', driver, 'verify', str(source), '--standard-libraries:false',
                                 '--type-system-refresh=' + str(refresh).lower(), '--solver-path', solver,
                                 '--cores', '1', '--resource-limit', '16000000', '--show-snippets:false'],
                                capture_output=True, text=True, timeout=600)
        output = result.stdout + result.stderr
        (out / (key + '.log')).write_text(output)
        errors = [line for line in output.splitlines() if 'Error:' in line]
        ok = result.returncode == code and (diagnostic is None or diagnostic in output)
        if name == 'false':
            ok &= len(errors) == 1 and 'assertion might not hold' in errors[0]
        ok &= not any(word in output.lower() for word in ('timed out', 'out of resource', 'unhandled exception'))
        print(key + ': ' + ('PASS' if ok else 'FAIL'))
        if not ok:
            print(output)
            failed.append(key)
print('Function reveal termination: %d/10 checks passed' % (10 - len(failed)))
sys.exit(bool(failed))
