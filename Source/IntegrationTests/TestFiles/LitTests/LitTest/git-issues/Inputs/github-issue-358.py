"""Controls for https://github.com/erniecohen/dafny/issues/358: every command that loads libraries
under --modular-termination can choose its Z3 binary with --solver-path.

Builds a recorded library with the given Z3, then loads it under --modular-termination with each
command that takes --library: once with --solver-path naming the Z3 that verified the library, which
must admit it, and once with --solver-path naming another Z3 binary, which must refuse it.  Prints
one line per check with its exit code and its error lines, with digests and directories normalized,
and keeps every command and output in the output directory.

    github-issue-358.py DAFNY OUT Z3 OTHER-Z3
"""
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

executable = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
solver = str(Path(sys.argv[3]).resolve())
other_solver = str(Path(sys.argv[4]).resolve())
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
launcher = ['dotnet', str(executable)] if executable.suffix == '.dll' else [str(executable)]
common = ['--use-basename-for-filename', '--standard-libraries:false']
verifier = ['--show-snippets:false', '--cores', '1', '--resource-limit', '16000000', '--verification-time-limit', '0']
failures = []
counter = [0]

library = output / 'lib.dfy'
library.write_text('''module Lib {
  function Double(x: nat): nat { 2 * x }
}
''')
client = output / 'client.dfy'
client.write_text('''module Client {
  import Lib
  lemma Four() ensures Lib.Double(2) == 4 {}
}
''')
doo = str(output / 'lib.doo')
refusal = 'it was verified with a different Z3 binary'


def run(name, args, expected_exit, diagnostic=None, absent=None):
    counter[0] += 1
    cmd = launcher + args
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=300)
    text = process.stdout + process.stderr
    (output / ('%02d-%s.txt' % (counter[0], name.replace(' ', '-')))).write_text(json.dumps(cmd) + '\n' + text)
    lines = [line.strip() for line in text.splitlines() if 'Error' in line or 'error' in line]
    shown = [re.sub(r'\b[0-9a-f]{12,64}\b', '<digest>', line.replace(str(output) + '/', '')) for line in lines]
    # The version of the other solver differs between installations.
    shown = [re.sub(r'than this run uses \(Z3 [^,]*,', 'than this run uses (Z3 <version>,', line) for line in shown]
    print('%s: exit %d' % (name, process.returncode))
    for line in shown:
        print('  ' + line)
    if process.returncode != expected_exit:
        failures.append('%s: expected exit %d' % (name, expected_exit))
    if diagnostic is not None and diagnostic not in text:
        failures.append('%s: missing diagnostic %r' % (name, diagnostic))
    if absent is not None and absent in text:
        failures.append('%s: unexpected diagnostic %r' % (name, absent))
    if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
        failures.append(name + ': infrastructure or resource failure')


run('build', ['build', '-t:lib', '--modular-termination', str(library), '--output=' + doo, '--solver-path', solver] +
    common + verifier, 0)
if not Path(doo).exists():
    failures.append('build: library file missing')

# Each command that takes --library, with the arguments it needs besides the files and the options.  Before
# https://github.com/erniecohen/dafny/issues/358, resolve, doc, extract and format rejected --solver-path.
commands = [
    ('resolve', ['resolve'], []),
    ('audit', ['audit'], []),
    ('doc', ['doc', '--output', str(output / 'docs')], []),
    ('extract', ['extract', 'Client', str(output / 'extracted.bpl')], []),
    ('format', ['format', '--check'], []),
    ('verify', ['verify'], verifier),
    ('translate', ['translate', 'cs', '--output', str(output / 'translated')], verifier),
]
for name, head, extra in commands:
    args = head + ['--modular-termination', '--library', doo, str(client)] + common + extra
    run(name + ' same solver', args + ['--solver-path', solver], 0, absent='cannot load')
    run(name + ' other solver', args + ['--solver-path', other_solver], 1, refusal)

if failures:
    print('FAILURES:')
    for failure in failures:
        print('  ' + failure)
else:
    print('all checks passed')
