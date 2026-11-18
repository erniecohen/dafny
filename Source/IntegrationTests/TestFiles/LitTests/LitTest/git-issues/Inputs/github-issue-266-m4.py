"""M4 controls for https://github.com/erniecohen/dafny/issues/266: portable contracts of bounded values in .doo libraries.

Builds libraries with order types, bounded values and argument-dependent bounds under --modular-termination, verifies
clients against them, replaces private bodies and public contracts, tampers with copies of them, and checks that every
control is admitted or rejected as expected.  Prints one line per check with its exit code and its error lines, with
digests and directories normalized, and keeps every command and output in the output directory.
"""
import hashlib
import json
import re
import shutil
import subprocess
import sys
import tomllib
import zipfile
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
          '--type-system-refresh', '--general-traits=datatype', '--decreases-orders',
          '--solver-path', solver, '--cores', '1', '--verification-time-limit', '0']
limit = ['--resource-limit', '16000000']
orders = str(sources / 'github-issue-211-multiset-orders.dfy')
failures = []
counter = [0]


def source(name):
    return str(sources / ('github-issue-266-m4-' + name + '.dfy'))


def doo(name):
    return str(output / (name + '.doo'))


def run(name, args, expected_exit, diagnostic=None, resource=limit):
    counter[0] += 1
    cmd = launcher + args + common + resource
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=600)
    text = process.stdout + process.stderr
    (output / ('%02d-%s.txt' % (counter[0], name))).write_text(json.dumps(cmd) + '\n' + text)
    lines = [line.strip() for line in text.splitlines() if 'Error' in line]
    shown = [re.sub(r'\b[0-9a-f]{12,64}\b', '<digest>', line.replace(str(output) + '/', '')) for line in lines]
    print('%s: exit %d' % (name, process.returncode))
    for line in shown:
        print('  ' + line)
    if process.returncode != expected_exit:
        failures.append('%s: expected exit %d' % (name, expected_exit))
    if diagnostic is not None and diagnostic not in text:
        failures.append('%s: missing diagnostic %r' % (name, diagnostic))
    if resource == limit and re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
        failures.append(name + ': infrastructure or resource failure')
    return text


def build(name, srcs, *extra, expected_exit=0, diagnostic=None, resource=limit):
    target = doo(name)
    run('build ' + name, ['build', '-t:lib'] + srcs + ['--output=' + target] + list(extra),
        expected_exit, diagnostic, resource)
    if (expected_exit == 0) != Path(target).exists():
        failures.append('build %s: library file %s' % (name, 'missing' if expected_exit == 0 else 'written'))


def verify(name, libraries, client, expected_exit, diagnostic=None):
    args = ['verify']
    for library in libraries:
        args += ['--library', library]
    run(name, args + [client], expected_exit, diagnostic)


def variant(name, base, *replacements):
    """A copy of the source "base" with each (old, new) replacement made exactly once, in the output directory."""
    text = Path(source(base)).read_text()
    for old, new in replacements:
        assert text.count(old) == 1, (name, old)
        text = text.replace(old, new)
    path = output / ('github-issue-266-m4-' + name + '.dfy')
    path.write_text(text)
    return str(path)


def entries(path):
    with zipfile.ZipFile(path) as z:
        return {name: z.read(name).decode('utf-8-sig') for name in z.namelist()}


def sha(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def set_field(text, name, value):
    updated, count = re.subn(r'(?m)^%s = .*$' % re.escape(name), '%s = %s' % (name, value), text, count=1)
    assert count == 1, name
    return updated


def option_digest(manifest):
    table = manifest[manifest.index('[modular_termination.semantic_options]'):].split('\n', 1)[1]
    table = table.split('\n[', 1)[0]
    options = dict(re.findall(r'(?m)^(\S+) = "(.*)"$', table))
    return sha(''.join('%s=%s\n' % (json.dumps(k), json.dumps(options[k])) for k in sorted(options)))


def escape(value):
    return str(value).replace('\\', '\\\\').replace('\t', '\\t').replace('\n', '\\n')


def record_digest(manifest):
    """The record's own digest, computed as Dafny computes it (ModularTerminationRecords.RecordSha256)."""
    data = tomllib.loads(manifest)
    record = data['modular_termination']
    lines = [('doo_file_version', data['doo_file_version'])]
    for key in ['schema', 'enabled', 'status', 'contract_schema', 'fork_build_id', 'solver_version', 'solver_sha256',
                'program_sha256', 'semantic_options_sha256', 'contracts_sha256']:
        value = record.get(key, '')
        lines.append((key, ('true' if value else 'false') if isinstance(value, bool) else value))
    for option in sorted(record.get('semantic_options', {})):
        lines.append(('option', option, record['semantic_options'][option]))
    for kind in ['exports', 'imports']:
        for entry in record.get(kind, []):
            lines.append((kind[:-1], entry['module'], entry['contract_sha256']))
    return sha(''.join('\t'.join(escape(v) for v in line) + '\n' for line in lines))


def tamper(library, kind, entry, old, new, redigest=True):
    """A copy of "library" with "old" replaced by "new" in "entry", and, with "redigest", every digest recomputed."""
    files = entries(doo(library))
    manifest = files['manifest.toml']
    assert files[entry].count(old) >= 1, (kind, old)
    files[entry] = files[entry].replace(old, new)
    if redigest:
        field = 'program_sha256' if entry == 'program' else 'contracts_sha256'
        manifest = set_field(manifest, field, '"%s"' % sha(files[entry]))
        manifest = set_field(manifest, 'record_sha256', '"%s"' % record_digest(manifest))
    files['manifest.toml'] = manifest
    target = doo('tampered-%s-%s' % (library, kind))
    with zipfile.ZipFile(target, 'w') as z:
        for name, text in files.items():
            z.writestr(name, text.encode('utf-8'))
    return target


# Producers: libraries with an order type, bounded values, and argument-dependent bounds.
build('lib', [source('lib'), orders])
build('args', [source('args'), orders])
for name in ['lib', 'args']:
    record = entries(doo(name))
    if '[modular_termination]' not in record['manifest.toml'] or record_digest(record['manifest.toml']) not in record['manifest.toml']:
        failures.append(name + ': no reproducible termination record')
    # The orders library is loaded and verified by every program that uses it, so a library does not store it.
    if re.search(r'(?m)^ *module (Std|Orders)\b', record['program']):
        failures.append(name + ': the stored program carries the orders library')
contracts = entries(doo('lib'))['termination-contracts.json']
for public in ['{:order Bags()} Level', 'const {:termination_closed} invoke: bounded<Level, int -> int>',
               'ensures o.invoke.bound == multiset{Twice} + f.invoke.bound']:
    if public not in contracts:
        failures.append('lib: contracts do not carry %r' % public)
for private in ['2 * Apply(f, x)', 'certify(']:
    if private in contracts:
        failures.append('lib: contracts carry the private %r' % private)
if 'bounded<ListObject -> Level, ListObject -> bool>' not in entries(doo('args'))['termination-contracts.json']:
    failures.append('args: contracts do not carry the argument-dependent bounded type')

# Clients: the library's bounded values, at the bounds of its contracts, and controls.
verify('client of lib', [doo('lib')], source('client-lib'), 0)
verify('client controls', [doo('lib')], source('client-verify'), 4, 'the bound of the bounded value must be below')
verify('client of args', [doo('args')], source('client-args'), 0)
verify('client controls of args', [doo('args')], source('client-args-verify'), 4, 'the bound of the bounded value must be below')

# Dependencies: private replacement keeps the contracts that a dependent was verified against; a change of a bound or
# of an order does not.
build('dependent', [source('dependent')], '--library', doo('lib'))
verify('client of both', [doo('lib'), doo('dependent')], source('client'), 0)
twice = '(x: int) => (Lower(Twice, Invoke, f.invoke.bound); 2 * Apply(f, x))'
build('lib-body', [variant('lib-body', 'lib', ('(x: int) => x + 1', '(x: int) => 1 + x'),
                           (twice, '(x: int) => (Lower(Twice, Invoke, f.invoke.bound); Apply(f, x) + Apply(f, x))')),
                   orders])
if entries(doo('lib-body'))['termination-contracts.json'] != contracts:
    failures.append('lib-body: a private replacement changed the contracts')
verify('private replacement', [doo('lib-body'), doo('dependent')], source('client'), 0)
build('lib-bound', [variant('lib-bound', 'lib', ('ensures o.invoke.bound == Empty', 'ensures o.invoke.bound == multiset{Invoke}'),
                            ('certify(Empty,', 'certify(multiset{Invoke},')), orders])
verify('bound change', [doo('lib-bound'), doo('dependent')], source('client'), 1,
       'a different termination contract of module FuncImpl')
build('lib-order', [variant('lib-order', 'lib', ('{:order Bags()} Level', '{:order MultisetOrders.Bags()} Level')), orders])
verify('order change', [doo('lib-order'), doo('dependent')], source('client'), 1,
       'a different termination contract of module Levels')

# A new realization of a library slot receives fresh body and override checks. No completion from
# the library may certify the client's implementation or a new formation inside that implementation.
build('slots', [source('slots')])
verify('fresh slot realization', [doo('slots')], source('slot-client'), 0)
verify('fresh slot false control', [doo('slots')],
       variant('slot-false', 'slot-client', ('// CONTROL', 'assert false;')), 4, 'assertion might not hold')
verify('fresh slot bound', [doo('slots')],
       variant('slot-bound', 'slot-client',
               ('x: int): int {', 'x: int): int decreases {:global} c.bound + 2 {')),
       4, 'decreases')
verify('fresh slot formation', [doo('slots')],
       variant('slot-formation', 'slot-client',
               ('c(x)', 'var f := bounded<nat, int -> int>.certify(0, (y: int) => c(y)); f(x)')),
       4, 'the bound of the bounded value must be below the bound under which the enclosing lambda runs')

# Source refinements and an abstract import binding form new resolved realizations too.
verify('source realization binding', [], source('realization'), 0)
verify('source realization false control', [],
       variant('realization-false', 'realization', ('// CONTROL', 'assert false;')), 4, 'assertion might not hold')
verify('source realization formation', [],
       variant('realization-formation', 'realization',
               ('c(x)', 'var f := bounded<nat, int -> int>.certify(0, (y: int) => c(y)); f(x)')),
       4, 'the bound of the bounded value must be below the bound under which the enclosing lambda runs')

# Admission: tampered contracts and programs, with every digest recomputed, and a program without.
for library, kind, entry, old, new, diagnostic in [
    ('lib', 'order-contract', 'termination-contracts.json', '{:order Bags()}', '{:order PairBags()}',
     'its stored termination contracts are not those of its program (module Levels)'),
    ('lib', 'order-program', 'program', '{:order Bags()}', '{:order PairBags()}',
     'its stored termination contracts are not those of its program (module Levels)'),
    ('lib', 'bounded-program', 'program', 'invoke: bounded<Level, int -> int>', 'invoke: bounded<nat, int -> int>',
     'its stored termination contracts are not those of its program (module FuncApi)'),
    ('lib', 'bound-program', 'program', 'ensures o.invoke.bound == Empty', 'ensures o.invoke.bound == multiset{Invoke}',
     'its stored termination contracts are not those of its program (module FuncImpl)'),
    ('args', 'arrow-contract', 'termination-contracts.json', 'intersects: bounded<ListObject -> Level,', 'intersects: bounded<Level,',
     'its stored termination contracts are not those of its program (module ListApi)'),
]:
    verify('tampered %s %s' % (library, kind), [tamper(library, kind, entry, old, new)], source('client-' + library), 1, diagnostic)
verify('tampered certify', [tamper('lib', 'certify', 'program', 'certify(Empty,', 'certify(multiset{Invoke},', redigest=False)],
       source('client-lib'), 1, 'its program does not match the program its record certifies')

# Refinement: a module may not inherit the bodies of a library module, which its contracts do not cover.
verify('refining client', [doo('lib')], source('refine'), 2, "module 'FuncApi2' refines module 'FuncApi' of a library")
# The same refinement of the source module verifies: the resolver keeps the bounded applications of the refined module
# unresolved syntax, which refinement clones (it used to rewrite them in place, and the clone did not resolve).
run('refining source', ['verify', source('lib'), source('refine'), orders], 0)

# Producer: a formation that does not verify, verification that is skipped, or that runs out of resources, gets no
# record and no library.
build('flat', [variant('flat', 'lib', ('certify(multiset{Twice} + f.invoke.bound,', 'certify(f.invoke.bound,')), orders],
      expected_exit=4, diagnostic=None)
build('no-verify', [source('lib'), orders], '--no-verify', expected_exit=3, diagnostic='was not completely verified')
build('out-of-resource', [source('lib'), orders], expected_exit=4, diagnostic='out of resource',
      resource=['--resource-limit', '2000'])

print('M4 portable contracts: %d checks, %s' % (counter[0], 'all passed' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
