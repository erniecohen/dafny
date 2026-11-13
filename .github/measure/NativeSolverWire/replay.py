"""Replay an exact captured task, optionally with its native physical history."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('solver', type=Path)
parser.add_argument('observation', type=Path)
parser.add_argument('problem')
parser.add_argument('--history', action='store_true')
parser.add_argument('--history-from', help='start history at this earlier task checkpoint')
parser.add_argument('--runs', type=int, default=1)
args = parser.parse_args()
assert args.runs > 0
matches = []
for index in args.observation.glob('solver-*.jsonl'):
    for line in index.read_text().splitlines():
        record = json.loads(line)
        if record.get('kind') == 'task-wire' and record['problem'] == args.problem:
            matches.append((index, record))
assert len(matches) == 1, 'select an observation containing exactly one matching task'
index, task = matches[0]
with index.with_suffix('.smt2').open('rb') as stream:
    physical = stream.read(task['end'])
assert len(physical) == task['end']
commands = physical[task['start']:task['end']]
assert len(commands) == task['length']
assert hashlib.sha256(commands).hexdigest() == task['sha256']
assert hashlib.sha256(physical[:task['start']]).hexdigest() == task['prefixSha256']
history_start = 0
prior_tasks = task['ordinal'] - 1 if args.history else 0
assert not args.history_from or args.history, '--history-from requires --history'
if args.history_from:
    earlier = [json.loads(line) for line in index.read_text().splitlines()]
    earlier = [r for r in earlier if r.get('kind') == 'task-wire'
               and r['problem'] == args.history_from and r['ordinal'] <= task['ordinal']]
    assert len(earlier) == 1, 'history must start at one preceding task on the same solver'
    history_start = earlier[0]['start']
    prior_tasks = task['ordinal'] - earlier[0]['ordinal']
payload = physical[history_start:] if args.history else commands
observations = []
for sample in range(args.runs):
    result = subprocess.run([str(args.solver.resolve()), '-smt2', '-in'], input=payload,
                            capture_output=True, timeout=1800)
    assert result.returncode == 0, result.returncode
    assert b'(error' not in result.stdout, result.stdout[-1000:]
    resources = [int(v) for v in re.findall(rb'\(:rlimit (\d+)\)', result.stdout)]
    assert resources, 'the captured task must request resource statistics'
    outcomes = re.findall(rb'(?m)^(sat|unsat|unknown)\r?$', result.stdout)
    assert outcomes, 'the captured task must have a check response'
    observations.append({'sample': sample, 'targetRU': resources[-1],
                         'targetSmtOutcome': outcomes[-1].decode(),
                         'resourceResponses': len(resources),
                         'stdoutSha256': hashlib.sha256(result.stdout).hexdigest(),
                         'stderrSha256': hashlib.sha256(result.stderr).hexdigest(),
                         'warningLines': result.stderr.count(b'\n')})
print(json.dumps({'history': args.history, 'priorTasks': prior_tasks, 'historyFrom': args.history_from,
                  'taskSha256': task['sha256'], 'taskBytes': task['length'],
                  'inputSha256': hashlib.sha256(payload).hexdigest(),
                  'observations': observations}, indent=2))
