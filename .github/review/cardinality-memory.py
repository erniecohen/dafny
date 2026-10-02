#!/usr/bin/env python3
"""Process-tree RSS sampler; the measured command runs unchanged."""
import argparse
import datetime
import json
import math
import os
import pathlib
import platform
import signal
import subprocess
import sys
import time


def process_tree_rss(root_pid):
    result = subprocess.run(['ps', '-e', '-o', 'pid=', '-o', 'ppid=', '-o', 'rss='],
                            text=True, capture_output=True, check=True, timeout=5)
    processes = {}
    for line in result.stdout.splitlines():
        values = line.split()
        if len(values) == 3:
            pid, ppid, rss = map(int, values)
            processes[pid] = (ppid, rss)
    children = {}
    for pid, (parent, _) in processes.items():
        children.setdefault(parent, []).append(pid)
    pending, members = [root_pid], set()
    while pending:
        pid = pending.pop()
        if pid in members:
            continue
        members.add(pid)
        pending.extend(children.get(pid, []))
    present = sorted(pid for pid in members if pid in processes)
    return sum(processes[pid][1] for pid in present), present


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    parser.add_argument('--interval-ms', type=float, default=50)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command or args.interval_ms <= 0:
        parser.error('a command and a positive sample interval are required')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    child = subprocess.Popen(command, start_new_session=True)
    received_signals = []
    previous_handlers = {}

    def forward(signum, _frame):
        received_signals.append(signum)
        if child.poll() is None:
            try:
                os.killpg(child.pid, signum)
            except ProcessLookupError:
                pass

    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        previous_handlers[signum] = signal.signal(signum, forward)
    peak, samples, peak_members, errors, intervals = 0, 0, [], [], []
    previous_sample = None
    try:
        while child.poll() is None:
            sample_started = time.monotonic()
            if previous_sample is not None:
                intervals.append(sample_started - previous_sample)
            previous_sample = sample_started
            try:
                rss, members = process_tree_rss(child.pid)
                samples += 1
                if rss > peak:
                    peak, peak_members = rss, members
            except (OSError, ValueError, subprocess.SubprocessError) as error:
                errors.append(str(error))
            remaining = args.interval_ms / 1000 - (time.monotonic() - sample_started)
            if remaining > 0:
                time.sleep(remaining)
        returncode = child.wait()
    finally:
        for signum, handler in previous_handlers.items():
            signal.signal(signum, handler)
    measurement = {
        'schema': 1,
        'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'hostname': platform.node(), 'platform': platform.platform(),
        'command': command, 'returncode': returncode,
        'requested_interval_ms': args.interval_ms,
        'samples': samples, 'sample_errors': errors,
        'max_sample_interval_ms': max(intervals, default=0) * 1000,
        'mean_sample_interval_ms': (sum(intervals) / len(intervals) * 1000) if intervals else 0,
        'peak_tree_rss_kib': peak,
        'peak_tree_rss_mib': peak / 1024,
        'peak_tree_rss_mb': peak * 1024 / 1000000,
        'peak_tree_rss_mb_ceiling': math.ceil(peak * 1024 / 1000000),
        'peak_tree_process_ids': peak_members,
        'elapsed_seconds': time.monotonic() - started,
        'received_signals': received_signals,
        'limitations': 'Sampled sum of parent-and-descendant RSS; shared pages may be counted more than once; peaks between samples and already orphaned descendants are not included.'
    }
    args.output.write_text(json.dumps(measurement, indent=2) + '\n')
    return returncode if returncode >= 0 else 128 - returncode


if __name__ == '__main__':
    sys.exit(main())
