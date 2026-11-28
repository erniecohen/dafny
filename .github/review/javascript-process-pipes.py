"""JavaScript execution drains stdout and stderr concurrently and exactly once."""
import json
import os
from pathlib import Path
import signal
import subprocess
import sys


def run(args, timeout=60):
    process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, start_new_session=True)
    def interrupted(signum, frame):
        raise RuntimeError(f'interrupted by signal {signum}')
    previous = {s: signal.signal(s, interrupted) for s in (signal.SIGTERM, signal.SIGINT)}
    try:
        stdout, stderr = process.communicate(timeout=timeout)
        return process.returncode, stdout, stderr
    finally:
        for sig in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(process.pid, sig)
            except ProcessLookupError:
                break
        process.wait(timeout=10)
        for sig, handler in previous.items():
            signal.signal(sig, handler)


def main():
    dll, source, directory = (Path(s).resolve() for s in sys.argv[1:])
    directory.mkdir(parents=True, exist_ok=True)
    stdout_text = 'BEGIN-STDOUT\n' + 'o' * (2 * 1024 * 1024) + '\nEND-STDOUT\n'
    stderr_text = 'BEGIN-STDERR\n' + 'e' * (2 * 1024 * 1024) + '\nEND-STDERR\n'
    for json_output in (False, True):
        for exit_code in (0, 7):
            name = f'{json_output}-{exit_code}'
            script = directory / (name + '.js')
            # Pending writes on both pipes must drain before the child can exit.
            script.write_text('process.stdout.write(' + json.dumps(stdout_text) + ');\n' +
                'process.stderr.write(' + json.dumps(stderr_text) + ');\n' +
                f'process.exitCode = {exit_code};\n')
            command = ['dotnet', str(dll), 'run', str(source), '--no-verify', '--target', 'js',
                       '--input', str(script), '--output', str(directory / ('program-' + name)),
                       '--standard-libraries:false', '--cores', '2']
            if json_output:
                command.append('--json-output')
            code, stdout, stderr = run(command)
            (directory / (name + '-stdout.log')).write_text(stdout)
            (directory / (name + '-stderr.log')).write_text(stderr)
            assert code == (0 if exit_code == 0 else 3), (name, code)
            assert stderr == stderr_text, (name, 'stderr changed', len(stderr))
            if json_output:
                messages = [json.loads(line) for line in stdout.splitlines()]
                output = [m for m in messages if 'BEGIN-STDOUT' in m.get('value', '')]
                assert len(output) == 1 and output[0] == {'type': 'status', 'value': stdout_text}, name
            else:
                assert stdout.count(stdout_text) == 1, (name, 'stdout changed')
                assert stdout.count('BEGIN-STDOUT') == stdout.count('END-STDOUT') == 1, name
    print('JavaScript stdout/stderr backpressure, JSON publication and exit controls passed')


if __name__ == '__main__':
    main()
