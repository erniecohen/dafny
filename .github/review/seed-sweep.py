#!/usr/bin/env python3
"""Compare per-member proof-cost distributions across independent naming seeds."""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys


SCHEMA = 1


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def quantile(values, fraction):
    """Linear interpolation at (n-1)*fraction (the usual type-7 quantile)."""
    if not values:
        return None
    ordered = sorted(values)
    index = (len(ordered) - 1) * fraction
    low = math.floor(index)
    return ordered[low] + (ordered[math.ceil(index)] - ordered[low]) * (index - low)


def distribution(samples, thresholds):
    successful = [s['ru'] for s in samples if s['success']]
    return {'n': len(samples), 'successes': len(successful),
            'outcomes': dict(sorted(Counter(o for s in samples for o in s['outcomes']).items())),
            'successful_ru': {'q25': quantile(successful, .25), 'median': quantile(successful, .5),
                              'q75': quantile(successful, .75), 'q90': quantile(successful, .9)},
            'successful_over': {str(t): sum(v > t for v in successful) for t in thresholds},
            'exhausted': len(samples) - len(successful)}


def rank_test(base, candidate):
    """Unpaired Mann-Whitney; tie-corrected normal approximation, two-sided.

    Resource exhaustion ranks after every success and ties with exhaustion.
    It is a within-budget failure, never an observed completed proof cost.
    The effect is P(candidate worse) + .5*P(tie), over all cross-lane pairs.
    """
    a = [s['ru'] if s['success'] else math.inf for s in base]
    b = [s['ru'] if s['success'] else math.inf for s in candidate]
    n, m = len(a), len(b)
    counts = Counter(a + b)
    rank, ranks = 1, {}
    for value, count in sorted(counts.items()):
        ranks[value] = rank + (count - 1) / 2
        rank += count
    u = sum(ranks[v] for v in b) - m * (m + 1) / 2
    total = n + m
    variance = n * m / 12 * (total + 1 - sum(t**3 - t for t in counts.values()) / (total * (total - 1)))
    z = max(0, abs(u - n * m / 2) - .5) / math.sqrt(variance) if variance > 0 else 0
    return {'u_candidate': u, 'probability_candidate_worse': u / (n * m),
            'p': math.erfc(z / math.sqrt(2))}


def compare(runs, seeds, thresholds, alpha):
    expected = {(lane, seed) for lane in ['base', 'candidate'] for seed in seeds}
    if len(runs) != len(expected) or {(r['lane'], r['seed']) for r in runs} != expected:
        raise ValueError('Incomplete or duplicate run population')
    population = set(runs[0]['members'])
    if not population or any(set(r['members']) != population for r in runs):
        raise ValueError('Member population changed or is empty; no intersection comparison is allowed')
    members = {}
    for name in sorted(population):
        samples = {lane: [r['members'][name] for r in runs if r['lane'] == lane]
                   for lane in ['base', 'candidate']}
        members[name] = {lane: distribution(value, thresholds) for lane, value in samples.items()}
        members[name]['rank_test'] = rank_test(samples['base'], samples['candidate'])
    # Holm adjustment across all selected members. Never cherry-pick a significant member.
    adjusted = 0
    for index, name in enumerate(sorted(members, key=lambda k: members[k]['rank_test']['p'])):
        row = members[name]
        test = row['rank_test']
        adjusted = max(adjusted, min(1, test['p'] * (len(members) - index)))
        test['p_holm'] = adjusted
        row['verdict'] = 'indistinguishable'
        row['reason'] = 'No distribution shift detected; this does not establish equivalence'
        if len(seeds) < 48:
            row['reason'] = 'Exploratory only: fewer than 48 seeds per lane'
        elif adjusted <= alpha and test['probability_candidate_worse'] != .5:
            row['verdict'] = 'worse' if test['probability_candidate_worse'] > .5 else 'better'
            row['reason'] = 'Within-budget rank distribution shifted (Holm-adjusted two-sided test)'
    verdicts = {row['verdict'] for row in members.values()} - {'indistinguishable'}
    # Regressions take priority: improvements elsewhere never cancel them out.
    verdict = 'worse' if 'worse' in verdicts else ('better' if verdicts else 'indistinguishable')
    return {'schema': SCHEMA, 'status': 'complete', 'verdict': verdict, 'alpha': alpha,
            'seeds': seeds, 'exploratory': len(seeds) < 48, 'members': members}


def read_members(path, exit_code):
    data = json.loads(path.read_text())
    result = {}
    for member in data['verificationResults']:
        name, batches = member['name'], member['vcResults']
        if not isinstance(name, str) or not name or name in result or not batches:
            raise ValueError('Duplicate/empty member or empty batch list')
        outcomes, resources, vcs = [], [], set()
        for batch in batches:
            vc, ru, outcome = batch['vcNum'], batch['resourceCount'], batch['outcome']
            if type(vc) is not int or vc in vcs or type(ru) is not int or ru < 0:
                raise ValueError('Invalid/duplicate VC or resource count')
            # Counterexamples, wall timeouts, memory failures and unknown outcomes
            # must not become apparent cost wins. Keep their raw logs, but no verdict.
            if outcome not in ['Valid', 'OutOfResource'] or batch.get('timingReplayed'):
                raise ValueError('Unusable solver outcome or cached result: ' + str(outcome))
            outcomes.append(outcome)
            resources.append(ru)
            vcs.add(vc)
        success = all(o == 'Valid' for o in outcomes)
        if member['outcome'] != ('Correct' if success else 'OutOfResource') or member.get('timingReplayed'):
            raise ValueError('Inconsistent member outcome or cached result')
        result[name] = {'success': success, 'ru': sum(resources), 'outcomes': outcomes,
                        'batches': len(batches)}
    if not result:
        raise ValueError('No verified members (check the target and filter)')
    expected_exit = 0 if all(r['success'] for r in result.values()) else 4
    if exit_code != expected_exit:
        raise ValueError(f'Exit {exit_code} disagrees with proof results (expected {expected_exit})')
    return result


def execute(command, cwd, directory, timeout):
    """Run sequentially; kill the owned process group on safety timeout/interrupt."""
    directory.mkdir(parents=True)
    (directory / 'command.json').write_text(json.dumps({'cwd': str(cwd), 'command': command}, indent=2) + '\n')
    with (directory / 'output.txt').open('w') as output:
        process = subprocess.Popen(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                   start_new_session=True)
        try:
            code = process.wait(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt):
            # POSIX is required, as for the other review runners.
            import os
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
            (directory / 'exit.json').write_text(json.dumps({'exit': process.returncode, 'interrupted': True}))
            raise
    (directory / 'exit.json').write_text(json.dumps({'exit': code}) + '\n')
    return code


def launcher(value, dotnet):
    path = Path(value).expanduser()
    if not path.is_file():
        found = shutil.which(value)
        if not found:
            raise ValueError('Binary not found: ' + value)
        path = Path(found)
    path = path.resolve()
    return ([dotnet, str(path)] if path.suffix == '.dll' else [str(path)]), path


def identity(binary, command, cwd):
    version = subprocess.run(command + ['--version'], cwd=cwd, capture_output=True, text=True, check=True,
                             timeout=30)
    # A managed entry point alone does not identify its dependent product assemblies.
    files = {p.name: digest(p) for p in sorted(binary.parent.iterdir())
             if p.is_file() and (p == binary or p.suffix in ['.dll', '.json', '.bpl', '.doo'])}
    return {'path': str(binary), 'version': version.stdout.strip(), 'files_sha256': files}


def source_identity(cwd, targets):
    tracked = subprocess.run(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'],
                             cwd=cwd, capture_output=True)
    if tracked.returncode == 0:
        paths = {cwd / p for p in tracked.stdout.decode().split('\0') if p.endswith(('.dfy', '.toml', '.doo'))}
    if tracked.returncode != 0 or not paths:
        # Standalone projects need an input boundary too, even without Git.
        paths = {p for p in cwd.rglob('*') if p.is_file() and p.suffix in ['.dfy', '.toml', '.doo']}
    pending = [cwd / t for t in targets]
    seen = set()
    while pending:
        path = pending.pop().resolve()
        if path in seen:
            continue
        seen.add(path)
        if not path.is_file():
            raise ValueError('Target/include not found: ' + str(path))
        paths.add(path)
        if path.suffix == '.dfy':
            pending.extend(path.parent / p for p in re.findall(r'^\s*include\s+"([^"]+)"', path.read_text(), re.M))
    return {str(p): digest(p) for p in sorted(paths) if p.is_file()}


def command_for(args, command, lane, seed, directory):
    return (command + ['verify'] + args.target + args.arg + getattr(args, lane + '_arg') +
            (['--filter-symbol', args.filter_symbol] if args.filter_symbol else []) +
            ['--solver-path', str(args.solver), '--cores', str(args.cores),
             '--resource-limit', str(args.resource_limit), '--verification-time-limit', '0',
             '--boogie', f'/randomSeed:{seed}', '--log-format', f'json;LogFileName={directory / "results.json"}'])


def validate_options(options):
    # The runner owns these controls. Do not allow an override hidden in a
    # combined --boogie value, a slash-style legacy flag or a single-dash alias.
    reserved = r'(randomSeed|random_seed|randomize|iterations|log-format|verification-cache|verify:|noVerify|filter-symbol|cores|resource-limit|verification-time-limit|timeLimit|rlimit|proverOpt|solver-path|solver-option|solver-plugin|vcsCores)'
    if any(re.search(reserved, option, re.I) for option in options):
        raise ValueError('Extra arguments override a sweep control, solver option, or cache setting')


def markdown(report):
    lines = ['# Seed sweep', '', f'Verdict: **{report["verdict"]}**. Status: {report["status"]}.', '',
             'Unpaired, tie-corrected Mann–Whitney test; Holm correction across members. ',
             'Exhaustions rank after successes. RU quantiles describe successes only; no equivalence claim.', '']
    if report['status'] != 'complete':
        return '\n'.join(lines + [report['error'], ''])
    if report['exploratory']:
        lines += ['Exploratory: fewer than 48 seeds per lane; directional verdicts disabled.', '']
    lines += ['| Member | Lane | Successes | RU Q25 / median / Q75 / Q90 (successes) | Over thresholds (successes) | Exhausted | Verdict / adjusted p |',
              '|---|---|---:|---|---|---:|---|']
    for name, row in report['members'].items():
        for lane in ['base', 'candidate']:
            d = row[lane]
            q = ' / '.join(str(d['successful_ru'][k]) for k in ['q25', 'median', 'q75', 'q90'])
            thresholds = ', '.join(f'>{t}: {n}' for t, n in d['successful_over'].items())
            escaped = name.replace('|', '\\|').replace('\n', ' ')
            lines.append(f'| {escaped} | {lane} | {d["successes"]}/{d["n"]} | {q} | {thresholds} | {d["exhausted"]} | {row["verdict"]} / {row["rank_test"]["p_holm"]:.4g} |')
    return '\n'.join(lines) + '\n'


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base', required=True, help='Base executable or DafnyDriver.dll')
    p.add_argument('--candidate', required=True, help='Candidate executable or DafnyDriver.dll')
    p.add_argument('--target', action='append', required=True, help='File or project; repeat for multiple inputs')
    p.add_argument('--filter-symbol', help='Dafny member filter, applied to both lanes')
    p.add_argument('--base-cwd', type=Path, default=Path.cwd())
    p.add_argument('--candidate-cwd', type=Path, default=Path.cwd())
    p.add_argument('--solver', type=Path, required=True)
    p.add_argument('--dotnet', default='dotnet')
    p.add_argument('--seeds', type=int, default=48, help='Number of consecutive nonzero seeds (default: 48)')
    p.add_argument('--start-seed', type=int, default=1)
    p.add_argument('--cores', type=int, choices=[1, 2, 3], default=1)
    p.add_argument('--resource-limit', type=int, required=True, help='Per-batch RU ceiling; member attributes still apply')
    p.add_argument('--timeout', type=float, default=600, help='Per-process safety cap in seconds; invalidates a timed-out comparison')
    p.add_argument('--threshold', type=int, action='append', help='Successful member RU threshold; repeat (default: 5M, 20M)')
    p.add_argument('--alpha', type=float, default=.05)
    p.add_argument('--arg', action='append', default=[], help='Shared Dafny argument, one token per occurrence; use --arg=TOKEN')
    p.add_argument('--base-arg', action='append', default=[], help='Base-only argument; use --base-arg=TOKEN')
    p.add_argument('--candidate-arg', action='append', default=[], help='Candidate-only argument; use --candidate-arg=TOKEN')
    p.add_argument('--out', type=Path, required=True, help='New output directory (never overwritten)')
    return p


def main(argv=None):
    p = parser()
    args = p.parse_args(argv)
    if (args.seeds < 2 or args.start_seed < 1 or args.start_seed + args.seeds - 1 > 2147483647 or
            args.resource_limit <= 0 or args.timeout <= 0 or not math.isfinite(args.timeout) or
            not 0 < args.alpha < 1 or any(t < 0 for t in args.threshold or [])):
        p.error('Use >=2 nonzero seeds, a positive finite limit/timeout, nonnegative thresholds, and 0<alpha<1')
    args.out = args.out.resolve()
    args.solver = args.solver.expanduser().resolve()
    args.base_cwd, args.candidate_cwd = args.base_cwd.resolve(), args.candidate_cwd.resolve()
    runs, metadata = [], {}
    try:
        validate_options(args.arg + args.base_arg + args.candidate_arg)
        commands, binaries = {}, {}
        for lane in ['base', 'candidate']:
            commands[lane], binaries[lane] = launcher(getattr(args, lane), args.dotnet)
        args.out.mkdir(parents=True, exist_ok=False)
        metadata = {'options': {k: str(v) if isinstance(v, Path) else v for k, v in vars(args).items()},
                    'solver': identity(args.solver, [str(args.solver)], Path.cwd()), 'lanes': {}}
        for lane in ['base', 'candidate']:
            cwd = getattr(args, lane + '_cwd')
            metadata['lanes'][lane] = {'binary': identity(binaries[lane], commands[lane], cwd),
                                       'sources_sha256': source_identity(cwd, args.target)}
        (args.out / 'manifest.json').write_text(json.dumps(metadata, indent=2) + '\n')
        seeds = list(range(args.start_seed, args.start_seed + args.seeds))
        for index, seed in enumerate(seeds):
            # Alternate lane order, but never run verifiers concurrently.
            for lane in (['base', 'candidate'] if index % 2 == 0 else ['candidate', 'base']):
                directory = args.out / lane / str(seed)
                command = command_for(args, commands[lane], lane, seed, directory)
                code = execute(command, getattr(args, lane + '_cwd'), directory, args.timeout)
                run = {'lane': lane, 'seed': seed, 'exit': code,
                       'members': read_members(directory / 'results.json', code)}
                runs.append(run)
                (directory / 'sample.json').write_text(json.dumps(run, indent=2) + '\n')
                print(f'{lane} seed {seed}: {sum(m["success"] for m in run["members"].values())}/{len(run["members"])} members successful', flush=True)
        # Detect edits/builds during a sweep, rather than comparing mixed inputs.
        for lane in ['base', 'candidate']:
            cwd = getattr(args, lane + '_cwd')
            if (source_identity(cwd, args.target) != metadata['lanes'][lane]['sources_sha256'] or
                    identity(binaries[lane], commands[lane], cwd) != metadata['lanes'][lane]['binary']):
                raise ValueError('Sources or binaries changed during the sweep')
        if identity(args.solver, [str(args.solver)], Path.cwd()) != metadata['solver']:
            raise ValueError('Solver changed during the sweep')
        report = compare(runs, seeds, args.threshold if args.threshold is not None else [5000000, 20000000], args.alpha)
    except (ValueError, KeyError, TypeError, OSError, subprocess.SubprocessError) as error:
        # Do not write into an existing output, even on failure.
        if not metadata:
            print(str(error), file=sys.stderr)
            return 2
        report = {'schema': SCHEMA, 'status': 'invalid', 'verdict': 'invalid', 'error': str(error)}
    report['runs'] = runs
    (args.out / 'summary.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n')
    summary = markdown(report)
    (args.out / 'summary.md').write_text(summary)
    print(summary)
    return 2 if report['status'] != 'complete' else (1 if report['verdict'] == 'worse' else 0)


if __name__ == '__main__':
    sys.exit(main())
