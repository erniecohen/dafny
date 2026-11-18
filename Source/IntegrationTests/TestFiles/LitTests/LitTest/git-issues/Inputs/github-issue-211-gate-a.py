"""Library gate for https://github.com/erniecohen/dafny/issues/211: a library verified alone is not invalidated
by a later extension.

Builds the library under --modular-termination with each resolver, then verifies clients that extend its
traits against the built library.  Each rejected client would prove false if its extension were accepted.  One client is accepted on an unchecked
premise that it states itself and that the default warning reports.
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


def run(name, args, expected_exit, diagnostic=None, verified=None, warnings=False):
    counter[0] += 1
    cmd = launcher + args + [flag for flag in common if not (warnings and flag.startswith('--warn-unchecked-termination'))]
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=600)
    text = process.stdout + process.stderr
    log = output / ('%02d-%s.txt' % (counter[0], re.sub(r'[^a-z0-9]+', '-', name)))
    log.write_text(json.dumps(cmd) + '\n' + text)
    print('%s: exit %d' % (name, process.returncode))
    for line in text.splitlines():
        if 'Error' in line or 'verified' in line or (warnings and 'Warning' in line):
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

    def client(name, client_source, expected_exit, diagnostic=None, verified=None, lib=library, enabled=True,
               warnings=False, legacy=False):
        run('%s: %s' % (resolver, name),
            ['verify'] + []
            + (['--legacy-no-modular-termination'] if legacy else []) + flags
            + ['--library', lib, source(client_source)],
            expected_exit, diagnostic, verified, warnings)

    # The library is verified alone, and gets a termination record.
    run(resolver + ': build library', ['build', '-t:lib'] + flags
        + [source('lib'), '--output=' + library], 0, verified='3 verified, 0 errors')

    # An extension within the rules is accepted; its only error is its control.
    client('extension within the rules', 'ok', 4, 'ok.dfy(14,4): Error: assertion might not hold', '2 verified, 1 error')

    # Extensions that would invalidate the library.
    client('implementation above the bound of its slot', 'raise', 4, "bound must not exceed the caller's")
    client('bound raised by a trait of the client', 'via', 4, 'bound must be below or equal to that in the trait')
    client('open trait of the client without a bound', 'via-open', 4, "bound must not exceed the caller's")
    client('implementation of a member with no bound that is not a leaf', 'unbounded', 2,
           "which has no 'decreases {:global}' bound, so it must be a leaf or declare a bound, but it is not a leaf")
    client('implementation of a member with no bound that declares one', 'bounded', 4,
           'bounded.dfy(11,10): Error: decreases clause might not decrease', '2 verified, 1 error')
    client('implementation of a member with no bound that is a leaf', 'leaf', 4,
           'leaf.dfy(15,4): Error: assertion might not hold', '2 verified, 1 error')
    client('module refines the library module', 'refine', 2, "module 'Ext' refines module 'Lib' of a library")

    # The legacy premise.  @AssumeCrossModuleTermination keeps its meaning under the option: the calls dispatched
    # to the class are not checked for termination, so this client verifies, 'assert false' included.  It is an
    # assumption of the client, which the default warning reports; the check runs with the warning on, and the
    # warning fails the run.
    client('legacy premise, reported by the default warning', 'assume', 2,
           'has @AssumeCrossModuleTermination and no {:axiom} annotation', '3 verified, 0 errors', warnings=True)

    # A library that carries no checked record is not admitted under the option, and a library with a record
    # is not admitted without it.
    client('source library', 'ok', 2, 'not verified under the rules of termination across modules', lib=source('lib'))
    run(resolver + ': build library without the option', ['build', '-t:lib'] + flags
        + [source('lib'), '--output=' + plain], 0, verified='3 verified, 0 errors')
    client('library built without the option', 'ok', 4, 'assertion might not hold', lib=plain)
    client('library with a record, option off', 'ok', 4, 'assertion might not hold', enabled=False)
    client('recorded library, legacy opt-out', 'ok', 1, 'checked only under that option', enabled=False, legacy=True)

    # Without the option, and with a library built without it, the rules still hold by default: the client
    # that implements the slot above its bound is rejected.
    client('without the option, by default', 'raise', 4, "bound must not exceed the caller's", lib=plain,
           enabled=False)

    # The hole that the rules close: under --legacy-no-modular-termination, the rules of 4.11, with a library
    # built the same way, the same client verifies, 'assert false' included.
    legacy_lib = str(output / ('legacy-%s.doo' % resolver))
    run(resolver + ': build library under legacy-off', ['build', '-t:lib', '--legacy-no-modular-termination'] + flags
        + [source('lib'), '--output=' + legacy_lib], 0, verified='3 verified, 0 errors')
    client('legacy-off (the hole that the rules close)', 'raise', 0, verified='3 verified, 0 errors',
           lib=legacy_lib, enabled=False, legacy=True)

    # A least lemma of the library under a bound.  The library is built alone, and its client is verified
    # against it (https://github.com/erniecohen/dafny/issues/461: the generated prefix lemma's {:axiom} is not
    # an assumption of the library).  The same pair verified as one program gives the same errors.
    extreme = str(output / ('extreme-%s.doo' % resolver))
    run(resolver + ': build library with a least predicate', ['build', '-t:lib'] + flags
        + [source('lib-extreme'), '--output=' + extreme], 0, verified='4 verified, 0 errors')
    client('least lemma of the library called from implementations of its slot', 'prefix', 4,
           'prefix.dfy(34,6): Error: assertion might not hold', '7 verified, 3 errors', lib=extreme)
    run(resolver + ': least lemma of the library, as one program',
        ['verify'] + flags + [source('lib-extreme'), source('prefix')], 4,
        'prefix.dfy(34,6): Error: assertion might not hold', '11 verified, 3 errors')

print('library gate: %d checks, %s' % (counter[0], 'all passed' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
