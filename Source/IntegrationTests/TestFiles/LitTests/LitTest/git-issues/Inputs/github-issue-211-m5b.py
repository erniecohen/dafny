"""M5b controls for https://github.com/erniecohen/dafny/issues/211: the standard-library trust list.

Builds libraries from the real src/Std/Axioms.dfy and src/Std/RealCompleteness.dfy, and from edited
copies of them, under --modular-termination with and without --modular-termination-trust, and checks
that every control is admitted or refused as expected.  Prints one line per check with its exit code,
its error lines and its trust-list summary line, with digests and directories normalized, and keeps
every command and output in the output directory.

    github-issue-211-m5b.py DAFNY OUT Z3 STD
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
std = Path(sys.argv[4]).resolve()
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
launcher = ['dotnet', str(executable)] if executable.suffix == '.dll' else [str(executable)]
# The semantic options of the standard library's project, under which the trust list's digests hold.
std_options = ['--type-system-refresh', '--general-traits=datatype', '--general-newtypes',
               '--reads-clauses-on-methods', '--enforce-determinism', '--track-print-effects']
common = ['--use-basename-for-filename', '--show-snippets:false', '--standard-libraries:false',
          '--solver-path', solver, '--cores', '1', '--resource-limit', '16000000',
          '--verification-time-limit', '0'] + std_options
# dafny audit resolves only, so it takes neither the verifier's options nor --show-snippets.  It
# takes --solver-path: admitting a library under --modular-termination checks the Z3 binary that
# verified it, and without the option audit would find another one.
audit_common = ['--use-basename-for-filename', '--standard-libraries:false', '--solver-path', solver] + std_options
trust = '--modular-termination-trust:standard-library'
failures = []
counter = [0]

axioms = (std / 'Axioms.dfy').read_text()
real_completeness = std / 'RealCompleteness.dfy'


def write(name, text):
    path = output / (name + '.dfy')
    path.write_text(text)
    return str(path)


def doo(name):
    return str(output / (name + '.doo'))


def strip_paths(line):
    """A line with the test directories removed and every other absolute path shortened to <dir>/name."""
    for directory in [str(output), str(std), str(executable.parent)]:
        line = line.replace(directory + '/', '').replace(directory, '<dir>')
    return re.sub(r'(?<![\w.])/[^\s\'",:()]*/([^\s\'",:()/]+)', r'<dir>/\1', line)


def run(name, args, expected_exit, diagnostic=None, options=None):
    counter[0] += 1
    cmd = launcher + args + (common if options is None else options)
    process = subprocess.run(cmd, cwd=output, capture_output=True, text=True, timeout=600)
    text = process.stdout + process.stderr
    (output / ('%02d-%s.txt' % (counter[0], name.replace(' ', '-')))).write_text(json.dumps(cmd) + '\n' + text)
    lines = [line.strip() for line in text.splitlines()
             if 'Error' in line or 'trusted premise' in line or line.startswith('Under --modular-termination')]
    shown = [re.sub(r'\b[0-9a-f]{12,64}\b', '<digest>', line.replace(str(output) + '/', '')) for line in lines]
    print('%s: exit %d' % (name, process.returncode))
    for line in shown:
        print('  ' + line)
    failed = process.returncode != expected_exit or (diagnostic is not None and diagnostic not in text)
    if process.returncode != expected_exit:
        failures.append('%s: expected exit %d' % (name, expected_exit))
    if diagnostic is not None and diagnostic not in text:
        failures.append('%s: missing diagnostic %r' % (name, diagnostic))
    if failed:
        # The first lines of the output, without directories, so that a failure explains itself.
        for line in text.splitlines()[:40]:
            print('  | ' + strip_paths(line))
    if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
        failures.append(name + ': infrastructure or resource failure')
    return text


def build(name, sources, *extra, expected_exit=0, diagnostic=None):
    target = doo(name)
    run('build ' + name, ['build', '-t:lib', '--modular-termination'] + list(sources) + ['--output=' + target] +
        list(extra), expected_exit, diagnostic)
    if (expected_exit == 0) != Path(target).exists():
        failures.append('build %s: library file %s' % (name, 'missing' if expected_exit == 0 else 'written'))


def verify(name, libraries, client, expected_exit, diagnostic=None, extra=()):
    args = ['verify', '--modular-termination']
    for library in libraries:
        args += ['--library', library]
    return run(name, args + [client] + list(extra), expected_exit, diagnostic)


def entries(path):
    with zipfile.ZipFile(path) as z:
        return {name: z.read(name).decode('utf-8-sig') for name in z.namelist()}


def sha(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def escape(value):
    return str(value).replace('\\', '\\\\').replace('\t', '\\t').replace('\n', '\\n')


def m5a_record_digest(manifest):
    """The record digest of milestone M5a, which has no premise lines."""
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
    return lines


def record_digest(manifest):
    data = tomllib.loads(manifest)
    record = data['modular_termination']
    lines = m5a_record_digest(manifest)
    if 'trust_list' in record:
        lines += [('trust_list', record['trust_list']), ('trust_list_sha256', record['trust_list_sha256']),
                  ('premises_sha256', record['premises_sha256'])]
        for p in record.get('premises', []):
            lines.append(('premise', p['id'], p['kind'], p['statement_sha256'], p['context_sha256']))
    return sha(''.join('\t'.join(escape(v) for v in line) + '\n' for line in lines))


def edited(name, old, new, text=axioms):
    if old not in text:
        failures.append('%s: the text to edit is not in Axioms.dfy' % name)
    return write(name, text.replace(old, new, 1))


CLIENT = write('client', """module Client {
  import Std.RealCompleteness
  import Std.Axioms.FunctionExtensionality

  lemma SameFunction(f: int -> int)
    ensures f == (x => f(x))
  {
    FunctionExtensionality.OnInt(f, x => f(x));
  }
}
""")

PREMISE_FREE = write('premise-free', """module PremiseFree {
  function Double(n: nat): nat { 2 * n }
  lemma DoubleIsEven(n: nat) ensures Double(n) % 2 == 0 {}
}
""")

# Producer: the real Axioms and RealCompleteness, with the trust list.  The record lists the seven
# premises of Std.Axioms, and the build says that its results assume them.
build('trusted', [str(std / 'Axioms.dfy'), str(real_completeness)], trust,
      diagnostic='these results assume 7 trusted premises of the standard-library trust list, in this library')
manifest = entries(doo('trusted'))['manifest.toml']
record = tomllib.loads(manifest)['modular_termination']
premises = [(p['id'], p['kind']) for p in record.get('premises', [])]
print('trusted record: schema %s, trust list %s, premises %s' % (record.get('schema'), record.get('trust_list'), premises))
if record.get('schema') != 2 or len(premises) != 7 or record_digest(manifest) != record.get('record_sha256'):
    failures.append('trusted: unexpected record')

# Producer: the same library without the trust list, and the negative controls with it.
build('untrusted', [str(std / 'Axioms.dfy'), str(real_completeness)], expected_exit=3,
      diagnostic='Declaration has explicit {:axiom} attribute')
build('edited-statement', [edited('axioms-statement', 'requires f() == g()', 'requires g() == f()'),
                           str(real_completeness)], trust, expected_exit=3,
      diagnostic="the trusted premise 'Std.Axioms.FunctionExtensionality.Nullary' does not match its entry in the "
                 "standard-library trust list: its statement digest is")
build('edited-context', [edited('axioms-context', 'forall x {:trigger x in s} :: x in s ==> x <= b',
                                'forall x {:trigger x in s} :: x in s ==> x <= b + 0.0'),
                         str(real_completeness)], trust, expected_exit=3,
      diagnostic='its context digest is')
build('unlisted-axiom', [str(std / 'Axioms.dfy'), str(real_completeness), write('unlisted', """module Unlisted {
  lemma {:axiom} Extra(x: int)
    ensures x * x >= 0
}
""")], trust, expected_exit=3, diagnostic='Declaration has explicit {:axiom} attribute')
build('assume', [str(std / 'Axioms.dfy'), str(real_completeness), write('assume', """module Assume {
  lemma Extra(x: int)
    ensures x * x >= 0
  {
    assume {:axiom} x * x >= 0;
  }
}
""")], trust, expected_exit=3, diagnostic='Definition has assume {:axiom} statement in body')

# Consumer: a client admits the trusted library only when it opts in to the same trust list, and its
# summary and audit then name the premises.
verify('client opted in', [doo('trusted')], CLIENT, 0,
       'Under --modular-termination these results assume 7 trusted premises of the standard-library trust list, from 1 library',
       extra=[trust])
verify('client not opted in', [doo('trusted')], CLIENT, 1,
       'it relies on the trusted premise Std.Axioms.FunctionExtensionality.Nullary of the standard-library trust list '
       'and 6 more; use --modular-termination-trust:standard-library to admit it')
verify('client trust none', [doo('trusted')], CLIENT, 1, 'use --modular-termination-trust:standard-library',
       extra=['--modular-termination-trust:none'])
run('client audit', ['audit', '--modular-termination', trust, '--library', doo('trusted'), CLIENT], 0,
    'the library relies on the trusted premise Std.Axioms.Reals.Complete (lemma) of the standard-library trust list',
    options=audit_common)


def tampered(name, change):
    files = entries(doo('trusted'))
    text = change(files['manifest.toml'])
    text = re.sub(r'(?m)^record_sha256 = .*$', 'record_sha256 = "%s"' % record_digest(text), text, count=1)
    files['manifest.toml'] = text
    target = doo('tampered-' + name)
    with zipfile.ZipFile(target, 'w') as z:
        for entry, content in files.items():
            z.writestr(entry, content.encode('utf-8'))
    return target


def change_statement(text):
    data = tomllib.loads(text)['modular_termination']
    first = data['premises'][0]['statement_sha256']
    text = text.replace(first, '0' * 64, 1)
    ids = re.findall(r'(?m)^id = "(.*)"$', text)
    kinds = re.findall(r'(?m)^kind = "(.*)"$', text)
    statements = re.findall(r'(?m)^statement_sha256 = "(.*)"$', text)
    contexts = re.findall(r'(?m)^context_sha256 = "(.*)"$', text)
    digest = sha(''.join('%s\t%s\t%s\t%s\n' % p for p in zip(ids, kinds, statements, contexts)))
    return re.sub(r'(?m)^premises_sha256 = .*$', 'premises_sha256 = "%s"' % digest, text, count=1)


verify('tampered premise', [tampered('premise', change_statement)], CLIENT, 1,
       'its premise Std.Axioms.FunctionExtensionality.Nullary is not an entry of the standard-library trust list',
       extra=[trust])
verify('dropped premise list', [tampered('premises', lambda t: re.sub(r'(?m)^premises_sha256 = .*$',
                                                                      'premises_sha256 = "%s"' % ('0' * 64), t, count=1))],
       CLIENT, 1, 'its premise list does not match its digest', extra=[trust])

# Identity: without premises, the trust list changes nothing.  A premise-free library built without the
# option, with --modular-termination-trust:none and with the standard-library list has the same bytes in
# every entry, its record is schema 1 without trust-list fields, and its digest is the one of M5a.
build('plain', [PREMISE_FREE])
build('trust-none', [PREMISE_FREE], '--modular-termination-trust:none')
build('trust-std', [PREMISE_FREE], trust)
plain = entries(doo('plain'))
for other in ['trust-none', 'trust-std']:
    if entries(doo(other)) != plain:
        failures.append('%s: library differs from the one built without the option' % other)
plain_record = tomllib.loads(plain['manifest.toml'])['modular_termination']
m5a = sha(''.join('\t'.join(escape(v) for v in line) + '\n' for line in m5a_record_digest(plain['manifest.toml'])))
identical = plain_record.get('schema') == 1 and not re.search(r'(?m)^(trust_list|premises)', plain['manifest.toml']) \
    and plain_record.get('record_sha256') == m5a
print('premise-free libraries identical with none, standard-library and no option, schema 1, M5a digest: %s' % identical)
if not identical:
    failures.append('premise-free record is not the one of M5a')
verify('premise-free client', [doo('plain')], write('plain-client', """module PlainClient {
  import PremiseFree
  lemma Use() ensures PremiseFree.Double(3) == 6 {}
}
"""), 0, extra=[trust])

print('M5b trust list: %d checks, %s' % (counter[0], 'all passed' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
