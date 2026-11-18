"""Check immutable allowances and fresh producer results against the public checked-Std protocol."""
from pathlib import Path
import re
import subprocess
import sys

driver, solver, out = sys.argv[1:4]
refresh = '--legacy' not in sys.argv[4:]
out = Path(out).resolve()
out.mkdir(parents=True, exist_ok=True)
here = Path(__file__).resolve().parent
root = next(p for p in here.parents if (p / 'Source/DafnyStandardLibraries').is_dir())
protocol = root / 'Source/DafnyStandardLibraries/modular-termination/overlay/CallPermissions.dfy'
original = (here / 'github-issue-211-produced-allowances.dfy').read_text()
failures = []


def check(name, text, expected, diagnostic=None):
    source = out / (name + '.dfy')
    source.write_text(text)
    args = ['dotnet', driver, 'verify', '--modular-termination', '--standard-libraries:false',
            '--type-system-refresh=' + str(refresh).lower(), '--general-traits=datatype', '--solver-path', solver,
            '--cores', '1', '--resource-limit', '16000000', '--verification-time-limit', '0',
            '--use-basename-for-filename', '--show-snippets:false', str(protocol), str(source)]
    result = subprocess.run(args, text=True, capture_output=True, timeout=600)
    output = result.stdout + result.stderr
    (out / (name + '.log')).write_text(output)
    errors = [line.strip() for line in output.splitlines() if 'Error:' in line]
    print('%s: exit %d, %d errors' % (name, result.returncode, len(errors)))
    for line in errors:
        print('  ' + line)
    if result.returncode != expected or (diagnostic and diagnostic not in output):
        failures.append(name)
    if re.search(r'timed out|out of resource|internal error|Unhandled exception', output, re.I):
        failures.append(name + ' infrastructure')
    if name == 'false' and (len(errors) != 1 or 'assertion might not hold' not in errors[0]):
        failures.append('false must be the only failure')


check('chain', original, 0)
check('false', original.replace('// CONTROL', 'assert false;'), 4, 'assertion might not hold')
check('equal', original.replace('(2 * depth + 1) as ORDINAL', '(2 * depth) as ORDINAL'), 4,
      'postcondition could not be proved')
check('greater', original.replace('(2 * depth + 1) as ORDINAL',
                                 '(if depth == 0 then 0 else 2 * depth - 1) as ORDINAL'), 4,
      'postcondition could not be proved')
check('reassigned', original.replace('// CONTROL', 'first.allowance := 100;'), 2, 'mutable')
check('mutable', '''module Mutable {
  import opened Std.CallPermissions
  class Bad extends Ranked { ghost var allowance: ORDINAL }
}''', 2, 'allowance')
print('Produced allowances: 6 checks, ' + ('all passed' if not failures else 'FAILED: ' + ', '.join(failures)))
sys.exit(bool(failures))
