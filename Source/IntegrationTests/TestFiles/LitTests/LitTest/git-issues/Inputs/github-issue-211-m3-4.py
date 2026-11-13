"""M3-4 controls for https://github.com/erniecohen/dafny/issues/211: termination records in .doo libraries.

Builds libraries under --modular-termination, tampers with copies of them, and checks that every
control is admitted or rejected as expected.  Prints one line per check with its exit code and its
error lines, with digests and directories normalized, and keeps every command and output in the
output directory.
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
          '--solver-path', solver, '--cores', '1', '--resource-limit', '16000000',
          '--verification-time-limit', '0']
failures = []
counter = [0]


def source(name):
    return str(sources / ('github-issue-211-m3-4-' + name + '.dfy'))


def doo(name):
    return str(output / (name + '.doo'))


def run(name, args, expected_exit, diagnostic=None, common_args=common, dafny=None):
    counter[0] += 1
    cmd = (dafny or launcher) + args + common_args
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=300)
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
    if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
        failures.append(name + ': infrastructure or resource failure')
    return text


def build(name, src, *extra, expected_exit=0, diagnostic=None, writes=None, dafny=None):
    target = doo(name)
    run('build ' + name, ['build', '-t:lib', '--modular-termination', src, '--output=' + target] + list(extra),
        expected_exit, diagnostic, dafny=dafny)
    writes = expected_exit == 0 if writes is None else writes
    if writes != Path(target).exists():
        failures.append('build %s: library file %s' % (name, 'missing' if writes else 'written'))


def verify(name, libraries, client, expected_exit, diagnostic=None, extra=(), enabled=True, common_args=common):
    args = ['verify'] + (['--modular-termination'] if enabled else [])
    for library in libraries:
        args += ['--library', library]
    run(name, args + [source(client)] + list(extra), expected_exit, diagnostic, common_args=common_args)


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


def tamper(kind, library='lib', redigest=True):
    files = entries(doo(library))
    manifest = files['manifest.toml']
    if kind == 'program':
        files['program'] = files['program'].replace('r := r + 1;', 'r := r + 2;')
    elif kind == 'program-contract':
        files['program'] = files['program'].replace('ensures r == n', 'ensures r == n + 1')
        manifest = set_field(manifest, 'program_sha256', '"%s"' % sha(files['program']))
    elif kind == 'contracts':
        files['termination-contracts.json'] = files['termination-contracts.json'].replace('ensures r == n', 'ensures true')
    elif kind == 'contracts-digest':
        files['termination-contracts.json'] = files['termination-contracts.json'].replace('ensures r == n', 'ensures true')
        manifest = set_field(manifest, 'contracts_sha256', '"%s"' % sha(files['termination-contracts.json']))
    elif kind == 'missing-contracts':
        del files['termination-contracts.json']
    elif kind == 'missing-record':
        manifest = manifest[:manifest.index('[modular_termination]')]
    elif kind == 'build-id':
        manifest = set_field(manifest, 'fork_build_id', '"%s"' % ('0' * 64))
    elif kind == 'status':
        manifest = set_field(manifest, 'status', '"partial"')
    elif kind == 'schema':
        manifest = set_field(manifest, 'schema', '2')
    elif kind == 'option':
        manifest = set_field(manifest, 'general-newtypes', '"true"')
    elif kind == 'option-digest':
        manifest = set_field(manifest, 'general-newtypes', '"true"')
        manifest = set_field(manifest, 'semantic_options_sha256', '"%s"' % option_digest(manifest))
    elif kind == 'unknown-key':
        manifest = manifest.replace('[modular_termination]\n', '[modular_termination]\nunknown = 1\n', 1)
    elif kind in ('imports', 'exports'):
        # Drop the table without touching any digest.
        manifest = re.sub(r'(?ms)^\[\[modular_termination\.%s\]\]\n(?:(?!\[).*\n?)*' % kind, '', manifest)
    if redigest and 'record_sha256' in manifest:
        manifest = set_field(manifest, 'record_sha256', '"%s"' % record_digest(manifest))
    files['manifest.toml'] = manifest
    if files == entries(doo(library)):
        failures.append('tamper %s changed nothing' % kind)
    target = doo('tampered-%s-%s%s' % (library, kind, '' if redigest else '-raw'))
    with zipfile.ZipFile(target, 'w') as z:
        for name, text in files.items():
            z.writestr(name, text.encode('utf-8'))
    return target


# Producer: a completely verified library records its termination contracts.
build('lib', source('lib'))
record = entries(doo('lib'))
manifest = record['manifest.toml']
if 'doo_file_version = "2.0"' not in manifest or '[modular_termination]' not in manifest:
    failures.append('lib: manifest carries no termination record')
if option_digest(manifest) not in manifest:
    failures.append('lib: semantic option digest is not reproducible')
if record_digest(manifest) not in manifest:
    failures.append('lib: record digest is not reproducible')
if 'termination-contracts.json' not in record or 'r := r + 1' in record['termination-contracts.json']:
    failures.append('lib: contracts entry missing or carries method bodies')

# Off-mode producer: unchanged library format.
run('build off', ['build', '-t:lib', source('lib'), '--output=' + doo('off')], 0)
off = entries(doo('off'))
if sorted(off) != ['manifest.toml', 'program'] or 'modular_termination' in off['manifest.toml'] \
        or 'doo_file_version = "1.0"' not in off['manifest.toml']:
    failures.append('off: library format changed')
if off['program'] != record['program']:
    failures.append('off: retained program differs from the recorded one')

# On/off matrix.
verify('on client, on library', [doo('lib')], 'client-lib', 0)
verify('on client, off library', [doo('off')], 'client-lib', 1, 'was built without --modular-termination')
verify('off client, on library', [doo('lib')], 'client-lib', 1, 'checked only under that option', enabled=False)
verify('off client, off library', [doo('off')], 'client-lib', 0, enabled=False)
verify('false client', [doo('lib')], 'client-false', 4, 'assertion might not hold')

# Admission: tampered or missing records.  Each tampered record also gets a recomputed record digest, so
# that the check for the part it changes is reached; the record digest itself is checked below.
verify('tampered status, record digest kept', [tamper('status', redigest=False)], 'client-lib', 1,
       'its record does not match its digest')
for kind, diagnostic in [
    ('program', 'its program does not match the program its record certifies'),
    ('program-contract', 'its stored termination contracts are not those of its program (module Lib)'),
    ('contracts', 'its termination-contracts.json entry does not match its record'),
    ('contracts-digest', 'its stored termination contracts are not those of its program (module Lib)'),
    ('missing-contracts', 'its termination-contracts.json entry is missing'),
    ('missing-record', 'its manifest has no modular termination record'),
    ('build-id', 'it was verified by a different build of Dafny or of its prelude'),
    ('status', 'does not state a complete verification'),
    ('schema', 'unsupported format'),
    ('option', 'recorded semantic options do not match their digest'),
    ('option-digest', "it was verified with general-newtypes 'true', but this run uses 'false'"),
    ('unknown-key', 'its manifest cannot be read'),
]:
    verify('tampered ' + kind, [tamper(kind)], 'client-lib', 1, diagnostic)
verify('option mismatch', [doo('lib')], 'client-lib', 1, "it was verified with manual-triggers 'false', but this run uses 'true'",
       extra=['--manual-triggers'])

# Solver identity: a different Z3 binary than the one that verified the library.
# This needs a second Z3 binary, z3-4.12.1, next to the given one, as the review CI installs it.
other_solver = Path(solver).with_name('z3-4.12.1')
if not other_solver.exists():
    print('different solver: requires %s next to the given solver' % other_solver.name)
    failures.append('different solver: %s is not installed next to the given solver' % other_solver.name)
else:
    verify('different solver', [doo('lib')], 'client-lib', 1, 'it was verified with a different Z3 binary',
           common_args=[str(other_solver) if a == solver else a for a in common])

# Prelude: a library verified under a changed prelude is not admitted with the installed one.
changed = output / 'changed-prelude'
shutil.copytree(executable.parent, changed)
with open(changed / 'DafnyPrelude.bpl', 'a') as prelude:
    prelude.write('\naxiom (forall h: Heap :: {$IsGoodHeap(h)} !$IsGoodHeap(h));\n')
build('changed-prelude', source('false-lemma'), dafny=['dotnet', str(changed / executable.name)])
verify('changed prelude', [doo('changed-prelude')], 'client-bad', 1,
       'it was verified by a different build of Dafny or of its prelude')

# Unsupported library sources.
verify('source library', [source('lib')], 'client-lib', 1, 'a source library carries no checked termination record')
run('standard library', ['verify', '--modular-termination', '--library', doo('lib'), source('client-lib'),
                         '--standard-libraries'], 1, 'standard library',
    common_args=[a for a in common if a != '--standard-libraries:false'])

# Dependencies: a library records the contracts it was verified against.
build('dependent', source('dependent'), '--library', doo('lib'))
if '[[modular_termination.imports]]' not in entries(doo('dependent'))['manifest.toml']:
    failures.append('dependent: no import recorded')
build('lib-body', source('lib-body'))
build('lib-contract', source('lib-contract'))
verify('client of both', [doo('lib'), doo('dependent')], 'client', 0)
verify('body-only change', [doo('lib-body'), doo('dependent')], 'client', 0)
verify('contract change', [doo('lib-contract'), doo('dependent')], 'client', 1, 'a different termination contract of module Lib')
verify('missing dependency', [doo('dependent')], 'client', 1, 'no admitted .doo library loaded here provides that module')
# Dropping a table without recomputing the record digest: dependent's imports would otherwise no longer
# pin the contract of Lib it was verified against, so the changed contract of lib-contract would be admitted.
verify('dropped imports', [doo('lib-contract'), tamper('imports', 'dependent', redigest=False)], 'client', 1,
       'its record does not match its digest')
verify('dropped exports', [tamper('exports', redigest=False), doo('dependent')], 'client', 1,
       'its record does not match its digest')

# Refinement: a module may not inherit bodies of a library module, which its contracts do not cover.
build('refine-base', source('refine-base'))
build('refine', source('refine'), '--library', doo('refine-base'), expected_exit=2,
      diagnostic="module 'M2' refines module 'M1' of a library")
verify('refining client', [doo('refine-base')], 'refine', 2, "module 'M2' refines module 'M1' of a library")

# Producer: only a completely verified program gets a record.
build('no-verify', source('lib'), '--no-verify', expected_exit=3, diagnostic='was not completely verified')
# A build restricted to some procedures writes no library at all, and says why it has no record.  No
# --boogie argument is allowed under the option, so the restriction is refused as one.
build('proc-filter', source('lib'), '--boogie', '/proc:*Count*', expected_exit=2, writes=False,
      diagnostic="--boogie argument '/proc:*Count*' is not allowed under --modular-termination")
# An unsound Boogie argument: the loop verifies only when unrolled.  Before, the build got a complete
# record and a client passing the same argument proved false.
build('loop-unroll', source('loop-unroll'), '--boogie', '/loopUnroll:1', expected_exit=3,
      diagnostic="--boogie argument '/loopUnroll:1' is not allowed under --modular-termination")
verify('client with --boogie', [doo('lib')], 'client', 1, extra=['--boogie', '/loopUnroll:1'],
       diagnostic="--boogie is set locally to [/loopUnroll:1], but the library was built with []")
build('verify-false', source('verify-false'), expected_exit=3, diagnostic='Declaration has {:verify false} attribute')
build('decreases-star', source('decreases-star'), expected_exit=3, diagnostic='uses decreases *')
build('only', source('only'), expected_exit=3, diagnostic='Member has explicit temporary @VerifyOnly attribute')
build('fails', source('fails'), expected_exit=4, diagnostic='assertion might not hold')

# Producer: unchecked premises leave the library without a record.
build('assume', source('assume'), expected_exit=3, diagnostic='Definition has assume statement in body')
build('axiom', source('axiom'), expected_exit=3, diagnostic='Definition has assume {:axiom} statement in body')
build('bodyless', source('bodyless'), expected_exit=3, diagnostic='Declaration has explicit {:axiom} attribute')
build('bodyless-plain', source('bodyless-plain'), expected_exit=3,
      diagnostic='Compiled declaration has no body and has an ensures clause')
build('bodyless-noensures', source('bodyless-noensures'), expected_exit=3,
      diagnostic="declares method 'M' without a body")
build('extern', source('extern'), expected_exit=3, diagnostic='Declaration with {:extern} has a ensures clause')
build('allow-axioms', source('lib'), '--allow-axioms', expected_exit=3, diagnostic='it was built with --allow-axioms')

# Producer: the assumptions in Dafny's own inventory (what 'dafny audit' reports).  Each of these needs
# --allow-warnings, which does not hide them.  Before the inventory was used, each got a complete record
# and a client of it proved false.
for name, diagnostic in [
    ('forall-nobody', 'Definition contains forall statement with no body'),
    ('while-nobody', 'Definition contains loop with no body'),
    ('for-nobody', 'Definition contains loop with no body'),
    ('static-trait', 'Ghost declaration has no body and has an ensures clause'),
    ('static-trait-function', 'Ghost declaration has no body and has an ensures clause'),
]:
    build(name, source(name), '--allow-warnings', expected_exit=3, diagnostic=diagnostic)

# Producer: the generated reveal lemma of an opaque function is not a premise.  Before, it refused the build.
build('opaque', source('opaque'))
verify('client of opaque', [doo('opaque')], 'client-opaque', 0)
build('opacity-opaque', source('opacity'), '--default-function-opacity', 'opaque')
build('opacity-auto', source('opacity'), '--default-function-opacity', 'autoRevealDependencies')

# Producer: {:auto_generated} and {:opaque_reveal} written by the user.  The serialization drops a member
# that has {:auto_generated}, and the inventory skipped it.  Before, both got a complete record and a client
# proved false; the second has no assumption at all.  The stored program is also verified again, which
# refuses the second form by itself.
build('forged-reveal', source('forged-reveal'), expected_exit=3,
      diagnostic='uses {:opaque_reveal}, which only Dafny may write under --modular-termination')
build('forged-generated', source('forged-generated'), expected_exit=3,
      diagnostic='uses {:auto_generated}, which only Dafny may write under --modular-termination')

# Iterators are not supported yet: the inventory reports the members Dafny generates for them as axioms.
build('iterator', source('iterator'), expected_exit=3,
      diagnostic="declares iterator 'Gen'; iterators are not yet supported under --modular-termination")

# The one exemption from the inventory: {:termination false} on a trait is checked under the option.
build('termination-false', str(sources / 'github-issue-211-m4b-good.dfy'))

print('M3-4 termination records: %d checks, %s' % (counter[0], 'all passed' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
