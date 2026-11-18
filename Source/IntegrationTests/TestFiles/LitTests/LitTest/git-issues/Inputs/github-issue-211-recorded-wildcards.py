"""Merged-file wildcard serialization must produce a complete, non-vacuous certificate."""
from pathlib import Path
import shutil
import subprocess
import sys

binary, solver, directory = sys.argv[1:]
binary, solver = str(Path(binary).resolve()), str(Path(solver).resolve())
out = Path(directory).resolve()
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)
first, second, client = [out / name for name in ['first.dfy', 'second.dfy', 'client.dfy']]
first.write_text('module A { function {:termination_closed} F(): (int -> int) { _ => 0 } }')
second.write_text('module B { datatype T = Zero function {:termination_closed} G(t: T): int { match t case _ => 1 } }')
for refresh in [False, True]:
    name = 'refresh' if refresh else 'legacy'
    library = out / (name + '.doo')
    base = ['dotnet', binary, '--standard-libraries:false',
        '--solver-path', solver, '--type-system-refresh:' + str(refresh).lower(),
        '--cores', '1', '--resource-limit', '1000000', '--use-basename-for-filename']
    def run(case, command, args):
        result = subprocess.run(base[:2] + [command] + base[2:] + args, capture_output=True, text=True)
        text = result.stdout + result.stderr
        (out / (name + '-' + case + '.log')).write_text(text)
        return result.returncode, text
    code, text = run('producer', 'build', ['--target:lib', '--output:' + str(library), str(first), str(second)])
    assert code == 0 and library.exists(), text
    print(name + ': merged wildcard library completes')
    source = 'module Client { import A import B method Use() { assert A.F()(2) == 0; assert B.G(B.Zero) == 1; // CONTROL\n } }'
    client.write_text(source)
    code, text = run('client', 'verify', ['--library:' + str(library), str(client)])
    assert code == 0, text
    print(name + ': client verifies')
    client.write_text(source.replace('// CONTROL', 'assert false;'))
    code, text = run('false', 'verify', ['--library:' + str(library), str(client)])
    assert code == 4 and text.count('Error: assertion might not hold') == 1 and '1 error' in text, text
    assert 'out of resource' not in text and 'timed out' not in text, text
    print(name + ': reachable false is the only failure')
