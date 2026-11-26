#!/usr/bin/env python3
"""Paired F4-M live-heap observations (issue 241).

Compare forced-GC profiles of the same program, with and without body release.
This measures a process-level difference, not root-exclusive A_body or an RSS
reduction. The 10% decision is relative to the baseline process's peak RSS in
this instrumented run. No-op-solver runs decide only the no-op workload.
"""
import argparse
import importlib.util
import json
import math
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def observation(events):
    manifests = [e for e in events if e.get('ev') == 'manifest']
    summaries = [e for e in events if e.get('ev') == 'summary']
    require(len(manifests) == len(summaries) == 1, 'missing or duplicate manifest/summary')
    require(manifests[0].get('heap') is True, 'forced-GC heap mode was not active')
    summary = summaries[0]
    require(summary.get('tasksSubmitted', 0) > 0, 'no verification tasks were submitted')
    require(summary.get('tasksUnfinished') == 0 and
            summary.get('tasksCompleted') == summary['tasksSubmitted'], 'unfinished verification tasks')
    points = [e for e in events if e.get('ev') == 'heap']
    require(len(points) > 2 and [e.get('where') for e in points[:2]] == ['parsed', 'resolved'],
            'missing parsed, resolved or reported-module heap points')
    require(all(e.get('where') == 'module' and e.get('module') for e in points[2:]),
            'unexpected heap point after resolution')
    require([e.get('index') for e in points] == list(range(1, len(points) + 1)),
            'missing or reordered heap point')
    modules = [e['module'] for e in points[2:]]
    require(len(set(modules)) == len(modules), 'duplicate reported module')
    # A truncated file can have a summary yet lack the last heap point. Translation
    # must cover exactly the modules whose results were reported, including the last.
    translated = [e.get('module') for e in events if e.get('ev') == 'span' and e.get('stage') == 'translate']
    require(sorted(translated) == sorted(modules), 'translation/report coverage differs')
    require(summary.get('translations') == len(translated) and summary.get('preparations', 0) > 0,
            'missing translation/preparation totals')
    for e in points:
        require(type(e.get('liveBytes')) is int and e['liveBytes'] > 0, 'missing or invalid live heap size')
    return points, summary


def pair(baseline, released):
    before, a = observation(baseline)
    after, b = observation(released)
    keys = lambda points: [(e['where'], e.get('module', '')) for e in points]
    require(keys(before) == keys(after), 'reported-module order differs')
    for key in ('translations', 'preparations', 'tasksSubmitted', 'tasksCompleted'):
        require(a.get(key) == b.get(key), f'{key} differs')
    releases = [e for e in released if e.get('ev') == 'release' and e.get('bodies', 0) > 0]
    require(releases, 'no bodies were released; comparison is vacuous')
    require(not any(e.get('ev') == 'release' and e.get('bodies', 0) > 0 for e in baseline),
            'baseline also released bodies')
    require({e.get('module') for e in releases} <= {e['module'] for e in after[2:]},
            'release for an unreported module')
    return before, after, a, b, releases


def compare(baseline, released, baseline_peak_rss=None):
    before, after, a, b, releases = pair(baseline, released)
    # PeakWorkingSet64 is unavailable (zero) on some runtimes; the runner's
    # wait4 rusage is then the authoritative process peak, in bytes.
    peak = baseline_peak_rss if baseline_peak_rss is not None else a.get('peakWorkingSetBytes')
    require(isinstance(peak, int) and not isinstance(peak, bool) and peak > 0, 'missing baseline peak RSS')
    points = [dict(where=x['where'], module=x.get('module', ''), baselineBytes=x['liveBytes'],
                   releasedBytes=y['liveBytes'], savedBytes=x['liveBytes'] - y['liveBytes'],
                   savedShareOfBaselinePeak=(x['liveBytes'] - y['liveBytes']) / peak)
              for x, y in zip(before, after)]
    # Use the final common reporting point: at a larger window, earlier points
    # may include different sets of modules still in flight. Never sum these deltas.
    share = points[-1]['savedShareOfBaselinePeak']
    require(math.isfinite(share), 'invalid saved share')
    return dict(schema=1, metric='final-live-heap-difference/baseline-peak-rss',
                baselinePeakRssBytes=peak, releasedPeakRssBytes=b.get('peakWorkingSetBytes') or None,
                releasedBodies=sum(e['bodies'] for e in releases), threshold=0.1,
                decision='go' if share >= 0.1 else 'no-go', points=points)


def sibling(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check_verification_logs(baseline, released):
    def rows(path):
        data = json.loads(Path(path).read_text())
        result = [(r['name'], vc['vcNum'], vc['assertions'], vc['outcome'], vc['resourceCount'])
                  for r in data['verificationResults'] for vc in r['vcResults']]
        require(result and any(row[-1] > 0 for row in result), 'no solver resource observations')
        return result
    require(rows(baseline) == rows(released), 'per-VC obligations, outcomes or resource counts differ')


def run(args):
    """Sequential pairs; never mix these forced-GC/no-op runs with timing runs."""
    frontend = sibling('frontend-profile')
    translation = sibling('translation-workers')
    root = Path(args.out).resolve()
    root.mkdir(parents=True, exist_ok=False)
    require(args.reps > 0 and args.cores > 0, 'reps and cores must be positive')
    for rep in range(1, args.reps + 1):
        for window in args.windows:
            require(window > 0, 'windows must be positive')
            observations = []
            for release in (False, True):
                directory = root / f'k{window}-r{rep}' / ('released' if release else 'baseline')
                directory.mkdir(parents=True)
                profile = directory / 'profile.jsonl'
                extra = ['--pipeline-profile-heap', '--verification-module-window', str(window),
                         '--bprint', str(directory / 'program.bpl')]
                if release:
                    extra += ['--release-verified-modules']
                cmd, env = frontend.command_line(str(Path(args.dafny).resolve()), 'verify', 'ws-default-on',
                                                  str(Path(args.project).resolve()), [], profile, args.cores, extra)
                result, output = frontend.run_one(cmd, env, '.')
                (directory / 'output.txt').write_bytes(output)
                (directory / 'run.json').write_text(json.dumps(result, indent=2) + '\n')
                require(result['exit'] in (0, 4), f"verification did not finish: exit {result['exit']}")
                events = frontend.load_events(profile)
                points, _ = observation(events)
                boogie = translation.modules(str(directory / 'program'))
                require(len(boogie) == len(points) - 2, 'incomplete Boogie capture')
                observations.append((events, result, output.replace(str(directory).encode(), b'<run>'), boogie))
            baseline, released = observations
            require(baseline[1]['exit'] == released[1]['exit'], 'exit codes differ')
            require(baseline[2] == released[2], 'diagnostics differ')
            require(baseline[3] == released[3], 'Boogie text differs')
            result = compare(baseline[0], released[0], round(baseline[1]['maxrss_mb'] * 1024 * 1024))
            result.update(window=window, rep=rep, workload='forced-GC/no-op-solver',
                          releasedPeakRssBytes=round(released[1]['maxrss_mb'] * 1024 * 1024),
                          boogieModules=len(baseline[3]), outputEqual=True, boogieEqual=True)
            path = root / f'k{window}-r{rep}' / 'comparison.json'
            path.write_text(json.dumps(result, indent=2) + '\n')
            print(f'K={window}, repetition {rep}: {result["decision"]} for this instrumented workload', flush=True)
    return 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    for command in ('compare', 'check'):
        sub = commands.add_parser(command)
        sub.add_argument('baseline')
        sub.add_argument('released')
        if command == 'compare':
            sub.add_argument('--out', required=True)
            sub.add_argument('--baseline-peak-rss-bytes', type=int)
        else:
            sub.add_argument('--verification-logs', nargs=2)
    sub = commands.add_parser('run')
    sub.add_argument('--dafny', required=True)
    sub.add_argument('--project', required=True)
    sub.add_argument('--out', required=True)
    sub.add_argument('--windows', type=int, nargs='+', default=[1, 4])
    sub.add_argument('--reps', type=int, default=1)
    sub.add_argument('--cores', type=int, default=2)
    args = parser.parse_args(argv)
    try:
        if args.command == 'run':
            return run(args)
        read = lambda path: [json.loads(line) for line in Path(path).read_text().splitlines() if line.strip()]
        if args.command == 'compare':
            result = compare(read(args.baseline), read(args.released), args.baseline_peak_rss_bytes)
            Path(args.out).write_text(json.dumps(result, indent=2) + '\n')
        else:
            pair(read(args.baseline), read(args.released))
            if args.verification_logs:
                check_verification_logs(*args.verification_logs)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f'incomplete retention evidence: {error}\n')
    print('Paired heap observations complete; this is a live-heap difference, not root attribution.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
