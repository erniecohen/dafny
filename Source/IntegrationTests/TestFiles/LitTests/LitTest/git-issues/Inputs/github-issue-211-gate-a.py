"""Library gate for https://github.com/erniecohen/dafny/issues/211: a library verified alone is not invalidated
by a later extension.

Builds the library under --modular-termination with each resolver, then verifies clients that extend its
traits against the built library.  Each rejected client would prove false if its extension were accepted.
Prints one line per check with its exit code and its error lines, and keeps every command and output in the
output directory.
"""
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

executable = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
solver = sys.argv[3]
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
sources = Path(__file__).resolve().parent
launcher = ['dotnet', str(executable)] if executable.suffix == '.dll' else [str(executable)]
common = ['--use-basename-for-filename', '--show-snippets:false', '--standard-libraries:false',
          '--warn-unchecked-termination=false', '--solver-path', solver, '--cores', '1',
          '--resource-limit', '16000000', '--verification-time-limit', '0']
resolvers = {
    'legacy': ['--type-system-refresh:false'],
    'refresh': ['--type-system-refresh:true', '--general-traits:legacy'],
}
failures = []
counter = [0]


def source(name):
    return str(sources / ('github-issue-211-gate-a-' + name + '.dfy'))


def run(name, args, expected_exit, diagnostic=None, verified=None):
    counter[0] += 1
    cmd = launcher + args + common
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=600)
    text = process.stdout + process.stderr
    log = output / ('%02d-%s.txt' % (counter[0], re.sub(r'[^a-z0-9]+', '-', name)))
    log.write_text(json.dumps(cmd) + '\n' + text)
    print('%s: exit %d' % (name, process.returncode))
    for line in text.splitlines():
        if 'Error' in line or 'verified' in line:
            print('  ' + line.strip().replace(str(output) + '/', ''))
    if process.returncode != expected_exit:
        failures.append('%s: expected exit %d' % (name, expected_exit))
    if diagnostic is not None and diagnostic not in text:
        failures.append('%s: missing diagnostic %r' % (name, diagnostic))
    if verified is not None and verified not in text:
        failures.append('%s: expected %r' % (name, verified))
    if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
        failures.append(name + ': infrastructure or resource failure')


for resolver, flags in resolvers.items():
    library = str(output / ('lib-%s.doo' % resolver))
    plain = str(output / ('plain-%s.doo' % resolver))

    def client(name, client_source, expected_exit, diagnostic=None, verified=None, lib=library, enabled=True):
        run('%s: %s' % (resolver, name),
            ['verify'] + (['--modular-termination'] if enabled else []) + flags + ['--library', lib, source(client_source)],
            expected_exit, diagnostic, verified)

    # The library is verified alone, and gets a termination record.
    run(resolver + ': build library', ['build', '-t:lib', '--modular-termination'] + flags
        + [source('lib'), '--output=' + library], 0, verified='3 verified, 0 errors')

    # An extension within the rules is accepted; its only error is its control.
    client('extension within the rules', 'ok', 4, 'ok.dfy(14,4): Error: assertion might not hold', '2 verified, 1 error')

    # Extensions that would invalidate the library.
    client('implementation above the bound of its slot', 'raise', 4, "bound must not exceed the caller's")
    client('bound raised by a trait of the client', 'via', 4, 'bound must be below or equal to that in the trait')
    client('open trait of the client without a bound', 'via-open', 4, "bound must not exceed the caller's")
    client('class extends a sealed trait', 'sealed', 2, "cannot extend sealed trait 'Lib.S' from another module")
    client('class extends a sealed trait, assuming termination', 'assume', 2,
           "cannot extend sealed trait 'Lib.S' from another module")
    client('module refines the library module', 'refine', 2, "module 'Ext' refines module 'Lib' of a library")

    # A library that carries no checked record is not admitted under the option, and a library with a record
    # is not admitted without it.
    client('source library', 'ok', 1, 'a source library carries no checked termination record', lib=source('lib'))
    run(resolver + ': build library without the option', ['build', '-t:lib'] + flags
        + [source('lib'), '--output=' + plain], 0, verified='3 verified, 0 errors')
    client('library built without the option', 'ok', 1, 'it has no modular termination record', lib=plain)
    client('library with a record, option off', 'ok', 1, 'checked only under that option', enabled=False)

    # What the option is for: without it, and with a library built without it, the client that implements the
    # slot above its bound verifies, 'assert false' included.
    client('without the option (the hole that the option closes)', 'raise', 0, verified='3 verified, 0 errors',
           lib=plain, enabled=False)

    # A least lemma of the library under a bound.  A library with a least predicate cannot be built under the
    # option yet (https://github.com/erniecohen/dafny/issues/461), so the library and its client are verified
    # as one program.
    run(resolver + ': build library with a least predicate', ['build', '-t:lib', '--modular-termination'] + flags
        + [source('lib-extreme'), '--output=' + str(output / ('extreme-%s.doo' % resolver))], 3,
        'has an assumption that')
    run(resolver + ': least lemma of the library called from implementations of its slot',
        ['verify', '--modular-termination'] + flags + [source('lib-extreme'), source('prefix')], 4,
        'prefix.dfy(34,6): Error: assertion might not hold', '11 verified, 3 errors')

print('library gate: %d checks, %s' % (counter[0], 'all passed' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
