"""Independent checks of a --verification-cache directory, without Dafny.

    verification-cache-audit.py keys    <cache>
        Recompute every result record's key from the record itself and check the index.
    verification-cache-audit.py replay  <cache> <z3> [--sample N] [--seed S] [--keys FILE] [--report FILE]
        Re-prove cached tasks: send each record's exact solver input to a fresh <z3>
        process and require every recorded response, including (get-info :rlimit), again.
    verification-cache-audit.py compare <cache-a> <cache-b> [--report FILE]
        For every key present in both caches, require the same outcome, resource count
        and solver exchange.  Two independent cold runs of one tree must agree.
    verification-cache-audit.py stats   <pipeline-profile.jsonl>
        Print the result-cache counters of one Dafny invocation as JSON.

A record (Source/DafnyCore/Caching/TaskResultCache.cs) holds the complete prepared solver
input of its task, the frozen execution environment (options, ordered UsedTypes, solver,
runtime and environment identity), both with the effective resource limit replaced by a
placeholder, the time limit, the outcome, the resource counts, the resource limit the result
depends on (0 when it holds under every limit that covers its resource use), and the recorded
solver exchange.  Its index key is
    task = CacheDigest.Compute("prepared-task-result/v2", input, environment, time limit)
    CacheDigest.Compute("prepared-task-result-slot/v2", task, slot[, the limit it depends on])
(Source/DafnyCore/Caching/CacheDigest.cs).  `keys` shows that a key is a function of those
bytes; `replay` shows that the recorded result is what the solver answers for them, with the
placeholder replaced by the record's resource limit, or by no limit for a limit-independent
record.  Neither trusts Dafny's own lookup code.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import random
import struct
import subprocess
import sys

OUTCOMES = {0: 'Correct', 1: 'Errors', 2: 'OutOfResource'}
SEND, REQUEST, PING, PROBLEM = 0, 1, 2, 3
PING_REQUEST = '(get-info :name)'
LIMIT_PLACEHOLDER = '(set-option :rlimit <task resource limit>)'
SCHEMA = 2


def digest(domain, *components):
    """CacheDigest.Compute: SHA-256 over length-framed, domain-separated fields."""
    fields = [b'dafny-cache/v1', domain.encode()] + list(components)
    h = hashlib.sha256(struct.pack('>Q', len(fields)))
    for field in fields:
        h.update(struct.pack('>Q', len(field)))
        h.update(field)
    return h.hexdigest()


def read_data(path):
    with open(path / 'data', 'rb') as f:
        return f.read()


def records(cache):
    """Yield (index key, record dict, problems) for every result index entry."""
    root = Path(cache) / 'v1'
    for index in sorted((root / 'results').glob('*/*')):
        if not index.is_dir() or index.name.startswith('.'):
            continue
        key = index.name
        problems = []
        try:
            entry = json.loads(read_data(index))
            blob = read_data(root / 'blobs' / 'sha256' / entry['Blob'][:2] / entry['Blob'])
        except (OSError, ValueError, KeyError) as e:
            yield key, None, ['unreadable: %s' % e]
            continue
        if hashlib.sha256(blob).hexdigest() != entry['Blob'] or len(blob) != entry['Length']:
            problems.append('blob digest or length mismatch')
        if entry.get('Key') != key or entry.get('Table') != 'results' or entry.get('Schema') != 1:
            problems.append('index entry names another key, table or schema')
        try:
            record = json.loads(blob)
        except ValueError as e:
            yield key, None, problems + ['record is not JSON: %s' % e]
            continue
        disabled = root / 'disabled' / 'results' / key[:2] / key
        if disabled.exists():
            problems.append('quarantined')
        yield key, record, problems


def task_key(record):
    return digest('prepared-task-result/v2', base64.b64decode(record['Input']),
                  base64.b64decode(record['Environment']), struct.pack('>I', record['TimeLimit']))


def record_key(record):
    exact = struct.pack('>I', record['ResourceLimit']) if record['Slot'] in ('exact-limit', 'out-of-resource') else b''
    return digest('prepared-task-result-slot/v2', task_key(record).encode(), record['Slot'].encode(), exact)


def exchange(record):
    return json.loads(base64.b64decode(record['ReportData']))['Steps']


def problem_name(steps):
    return next((s['Command'] for s in steps if s['Kind'] == PROBLEM), '')


def keys(cache):
    count, bad = 0, []
    for key, record, problems in records(cache):
        count += 1
        if record is not None and record.get('Schema') != SCHEMA:
            problems = problems + ['record schema %r' % record.get('Schema')]
        elif record is not None and record_key(record) != key:
            problems = problems + ['key is not the digest of the record\'s input, environment and limits']
        if problems:
            bad.append((key, problems))
    for key, problems in bad:
        print('%s: %s' % (key, '; '.join(problems)))
    print('keys: %d records, %d with problems' % (count, len(bad)))
    return not bad


class Solver:
    """A fresh solver process driven as Boogie's SMTLibProcess drives it."""

    def __init__(self, z3, env=None):
        self.p = subprocess.Popen([z3, '-smt2', '-in'], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.DEVNULL, text=True, bufsize=1, env=env)
        self.line, self.pos = '', 0

    def send(self, command):
        self.p.stdin.write(command + '\n')

    def _readline(self):
        """One output line without its terminator, as Process.OutputDataReceived delivers it; None at end."""
        line = self.p.stdout.readline()
        if line == '':
            return None
        return line[:-1] if line.endswith('\n') else line

    def _peek(self):
        while True:
            while self.pos < len(self.line) and self.line[self.pos].isspace():
                self.pos += 1
            if self.pos < len(self.line) and self.line[self.pos] != ';':
                return self.line[self.pos]
            self.p.stdin.flush()
            self.line, self.pos = self._readline(), 0
            if self.line is None:
                self.line = ''
                return None

    def _id(self):
        begin = self._peek()
        quoted = begin in ('"', '|')
        if quoted:
            self.pos += 1
        out = []
        while True:
            if self.pos >= len(self.line):
                if not quoted:
                    break
                while True:
                    out.append('\n')
                    self.line, self.pos = self._readline(), 0
                    if self.line != '':
                        break
                if self.line is None:
                    self.line = ''
                    break
                continue
            c = self.line[self.pos]
            self.pos += 1
            if quoted and c == begin:
                break
            if not quoted and (c.isspace() or c in '()'):
                self.pos -= 1
                break
            if quoted and c == '\\' and self.pos < len(self.line) and self.line[self.pos] == '"':
                out.append('"')
                self.pos += 1
                continue
            out.append(c)
        return ''.join(out)

    def sexpr(self):
        """SExprParser.ParseSExprs(top: true), as {Name, Arguments}."""
        c = self._peek()
        if c is None:
            return None
        if c != '(':
            return {'Name': self._id(), 'Arguments': []}
        self.pos += 1
        name = '' if self._peek() == '(' else self._id()
        args = []
        while self._peek() not in (')', None):
            args.append(self.sexpr())
        if self._peek() == ')':
            self.pos += 1
        return {'Name': name, 'Arguments': args}

    def response(self):
        """SMTLibProcess.GetProverResponse: skips progress and benign unsupported lines."""
        while True:
            r = self.sexpr()
            if r is None or r['Name'] not in ('progress', 'unsupported'):
                return r

    def request(self, command):
        """SMTLibProcess.SendRequest: the last response before the pong."""
        self.send(command)
        self.send(PING_REQUEST)
        previous = None
        while True:
            r = self.response()
            if r is None:
                raise RuntimeError('solver died')
            if r['Name'] == ':name':
                return previous
            previous = r

    def ping(self):
        self.send(PING_REQUEST)
        while True:
            r = self.response()
            if r is None:
                raise RuntimeError('solver died')
            if r['Name'] == ':name':
                return

    def close(self):
        try:
            self.p.stdin.close()
        except OSError:
            pass
        self.p.kill()
        self.p.wait()


def replay_one(z3, record, env=None):
    """Re-prove one record; return None if every response is reproduced, else the first difference."""
    solver = Solver(z3, env)
    limit = '(set-option :rlimit %d)' % record['ResourceLimit']
    try:
        for index, step in enumerate(exchange(record)):
            if step['Kind'] == SEND:
                solver.send(limit if step['Command'] == LIMIT_PLACEHOLDER else step['Command'])
            elif step['Kind'] == PING:
                solver.ping()
            elif step['Kind'] == REQUEST:
                actual = solver.request(step['Command'])
                if actual != step['Response']:
                    return 'step %d %s: recorded %s, solver %s' % (
                        index, step['Command'], json.dumps(step['Response'])[:200], json.dumps(actual)[:200])
        return None
    finally:
        solver.close()


def replay(cache, z3, sample=None, seed=0, keys_file=None, report=None, env=None):
    candidates = [(k, r) for k, r, problems in records(cache) if r is not None and not problems and r.get('Schema') == SCHEMA]
    if keys_file:
        wanted = set(Path(keys_file).read_text().split())
        candidates = [(k, r) for k, r in candidates if k in wanted]
    if sample is not None and sample < len(candidates):
        candidates = random.Random(seed).sample(candidates, sample)
    rows, failures = [], 0
    for key, record in candidates:
        difference = 'key mismatch' if record_key(record) != key else replay_one(z3, record, env)
        failures += difference is not None
        rows.append({'key': key, 'task': problem_name(exchange(record)),
                     'outcome': OUTCOMES.get(record['Outcome'], record['Outcome']),
                     'resourceCount': record['ResourceCount'], 'difference': difference})
        print('%s %-60s %s %s' % (key[:16], rows[-1]['task'][:60], rows[-1]['outcome'],
                                   'reproduced' if difference is None else 'DIFFERS: ' + difference), flush=True)
    if report:
        Path(report).write_text(json.dumps(rows, indent=1) + '\n')
    print('replay: %d of %d cached tasks re-proved, %d differ' % (len(rows) - failures, len(rows), failures))
    return failures == 0


def compare(a, b, report=None):
    left = {k: r for k, r, problems in records(a) if r is not None and not problems}
    right = {k: r for k, r, problems in records(b) if r is not None and not problems}
    common = sorted(set(left) & set(right))
    differences = []
    for key in common:
        x, y = left[key], right[key]
        for field in ('Outcome', 'ResourceCount', 'ReportData'):
            if x[field] != y[field]:
                differences.append({'key': key, 'task': problem_name(exchange(x)), 'field': field})
                break
    summary = {'left': len(left), 'right': len(right), 'common': len(common), 'differ': len(differences),
               'differences': differences}
    if report:
        Path(report).write_text(json.dumps(summary, indent=1) + '\n')
    for d in differences:
        print('%s %s differs in %s' % (d['key'][:16], d['task'], d['field']))
    print('compare: %d and %d records, %d in both, %d differ' % (len(left), len(right), len(common), len(differences)))
    return not differences


def stats(profile):
    events = [json.loads(line) for line in Path(profile).read_text().splitlines() if line.strip()]
    cache = [e for e in events if e.get('ev') == 'result-cache']
    tasks = [e for e in events if e.get('ev') == 'result-task']
    out = dict(cache[-1]) if cache else {}
    out['tasks'] = len(tasks)
    out['replayedTasks'] = sum(1 for t in tasks if t.get('replayed'))
    out['replayedKeys'] = sorted({t['executionKey'] for t in tasks if t.get('replayed') and t.get('executionKey')})
    return out


def main(argv):
    command, args = argv[1], argv[2:]
    options = {}
    positional = []
    i = 0
    while i < len(args):
        if args[i].startswith('--'):
            options[args[i]] = args[i + 1]
            i += 2
        else:
            positional.append(args[i])
            i += 1
    if command == 'keys':
        return 0 if keys(*positional) else 1
    if command == 'replay':
        sample = int(options['--sample']) if '--sample' in options else None
        return 0 if replay(positional[0], positional[1], sample, int(options.get('--seed', '0')),
                           options.get('--keys'), options.get('--report')) else 1
    if command == 'compare':
        return 0 if compare(positional[0], positional[1], options.get('--report')) else 1
    if command == 'stats':
        s = stats(positional[0])
        s.pop('replayedKeys')
        print(json.dumps(s, sort_keys=True))
        return 0
    print(__doc__, file=sys.stderr)
    return 2


if __name__ == '__main__':
    sys.exit(main(sys.argv))
