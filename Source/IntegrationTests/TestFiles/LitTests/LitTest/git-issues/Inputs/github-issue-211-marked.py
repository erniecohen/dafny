"""Checks that marked members are rejected by the rules of --modular-termination
(https://github.com/erniecohen/dafny/issues/211).

Each input has marked members (a letter and two digits), each of which breaks the rules in one way.  The
driver verifies each input under --modular-termination with both resolvers and prints, for each marked
member, the errors of the termination rules reported in it.  A marked member with no such error has
escaped the rules, and fails the test.  An accepted input is a control: its members stay within the
rules, and its only errors are its 'assert false' controls.

Arguments: the Dafny executable, the solver, and the suite to run.
"""
import re
import subprocess
import sys
from pathlib import Path

executable = Path(sys.argv[1]).resolve()
solver = sys.argv[2]
prefix = ''
sources = Path(__file__).resolve().parent
launcher = ['dotnet', str(executable)] if executable.suffix == '.dll' else [str(executable)]
common = ['verify', '--modular-termination', '--warn-unchecked-termination=false', '--allow-warnings',
          '--use-basename-for-filename', '--show-snippets:false', '--standard-libraries:false',
          '--solver-path', solver, '--cores', '1', '--resource-limit', '16000000',
          '--verification-time-limit', '0']
resolvers = {
    'legacy resolver': ['--type-system-refresh:false'],
    'refresh resolver': ['--type-system-refresh:true', '--general-traits:legacy'],
}
modifiers = r'^\s*(?:(?:ghost|least|greatest|twostate|static|opaque|abstract)\s+)*'
marked = re.compile(modifiers + r'(?:function|predicate|method|lemma|const)\s+(?:\{[^}]*\}\s*)*([A-Z]\d\d)\b')
declaration = re.compile(modifiers + r'(?:function|predicate|method|lemma|const|constructor|class|trait|datatype|'
                         r'codatatype|newtype|type|iterator|import|module)\b|^\s*//|^\s*\}\s*$')
rule = re.compile(r"modular.termination|'decreases \{:(?:global|callback)\}'")
error = re.compile(r'^(.*)\((\d+),(\d+)\): Error: (.*)$')
failures = []


def check(name, expected_exit, only=None, accepted=False):
    path = sources / (prefix + name + '.dfy')
    # A marked member has the lines from its declaration to the next declaration, comment line or closing brace.
    members = {}
    names = []
    current = None
    for number, line in enumerate(path.read_text().splitlines(), 1):
        if declaration.match(line):
            match = marked.match(line)
            current = match.group(1) if match else None
            if current:
                names.append(current)
        if current:
            members[number] = current
    results = {}
    for resolver, flags in resolvers.items():
        if only is not None and resolver != only:
            continue
        process = subprocess.run(launcher + common + flags + [str(path)], capture_output=True, text=True, timeout=600)
        text = process.stdout + process.stderr
        found = {}
        lines = ['exit %d' % process.returncode]
        elsewhere = []
        for line in text.splitlines():
            match = error.match(line.strip())
            if match and match.group(1) == path.name and int(match.group(2)) in members and rule.search(match.group(4)):
                messages = found.setdefault(members[int(match.group(2))], [])
                if match.group(4) not in messages:
                    messages.append(match.group(4))
            elif 'Error' in line or (accepted and 'verified' in line):
                elsewhere.append(line.strip())
        for member in names:
            if member in found:
                lines += ['%s rejected: %s' % (member, message) for message in found[member]]
                if accepted:
                    failures.append('%s, %s: %s is rejected' % (name, resolver, member))
            elif not accepted:
                lines.append('%s ESCAPED' % member)
                failures.append('%s, %s: %s has no error' % (name, resolver, member))
        lines += elsewhere
        if process.returncode != expected_exit:
            failures.append('%s, %s: expected exit %d' % (name, resolver, expected_exit))
        if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
            failures.append('%s, %s: infrastructure or resource failure' % (name, resolver))
        results[resolver] = lines
    # The two resolvers are expected to agree; print them together when they do.
    if len(results) == 2 and len(set(map(tuple, results.values()))) == 1:
        results = {'both resolvers': next(iter(results.values()))}
    for resolver, lines in results.items():
        print('%s, %s: %s' % (name, resolver, lines[0]))
        for line in lines[1:]:
            print('  ' + line)


suite = sys.argv[3]
if suite == 'coverage':
    prefix = 'github-issue-211-gate-b-'
    # Reported by the verifier, where the call or the evaluation is made.
    check('expressions', 4)
    check('statements', 4)
    check('declarations', 4)
    check('defaults', 4)
    check('lambdas', 4)
    check('by-method', 4)
    # Reported by the resolver, which classifies what an operation may reach.
    check('values', 2)
    check('types', 2)
    check('types-refresh', 2, only='refresh resolver')
    check('functions', 2)
    # The control: the same contexts within the rules.
    check('accepted', 4, accepted=True)
elif suite == '456':
    prefix = 'github-issue-456-'
    check('rules', 4)
    check('rules-slot', 4)
    check('rules-imported', 4)
    check('rules-callback', 2)
    check('accepted', 4, accepted=True)
else:
    failures.append('unknown suite ' + suite)

print('%s: %s' % (suite, 'all marked members rejected' if not failures else 'FAILED'))
for failure in failures:
    print('FAILURE ' + failure)
sys.exit(1 if failures else 0)
