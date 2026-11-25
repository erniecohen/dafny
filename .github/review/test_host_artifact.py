"""Local action runner and real filesystem/process controls; no Actions service needed."""
import importlib.util
from contextlib import redirect_stdout
import io
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

ACTION = Path(os.environ.get('HOST_ARTIFACT_TEST_ACTION',
                            Path(__file__).resolve().parents[1] / 'actions/host-artifact'))
spec = importlib.util.spec_from_file_location('host_artifact', ACTION / 'host-artifact.py')
transport = importlib.util.module_from_spec(spec)
spec.loader.exec_module(transport)


class HostArtifactTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.cache = self.root / 'cache'
        self.source = self.root / 'payload.txt'
        self.source.write_text('first')
        self.output = self.root / 'outputs'
        self.env = dict(os.environ, HOST_ARTIFACT_DIR=str(self.cache),
                        GITHUB_REPOSITORY='example/project', GITHUB_RUN_ID='100',
                        GITHUB_RUN_ATTEMPT='1', GITHUB_OUTPUT=str(self.output),
                        ARTIFACT_ACTION_PATH=str(ACTION))
        self.run = self.cache / 'example/project/100'

    def invoke(self, mode, name='build', path=None, **env):
        environment = dict(self.env, ARTIFACT_MODE=mode, ARTIFACT_NAME=name,
                           ARTIFACT_PATH=str(path or (self.source if mode == 'put' else self.root / 'restored')))
        environment.update(env)
        environment = {key: value for key, value in environment.items() if value is not None}
        # Execute the actual composite shell entry point, with its expression
        # inputs supplied as environment variables just as Actions supplies them.
        command = re.search(r'^      run: (.+)$', (ACTION / 'action.yml').read_text(), re.M).group(1)
        return subprocess.run(['bash', '-e', '-o', 'pipefail', '-c', command], env=environment,
                              capture_output=True, text=True, timeout=15)

    def ok(self, result):
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_put_get_file(self):
        self.ok(self.invoke('put'))
        self.ok(self.invoke('get'))
        self.assertEqual((self.root / 'restored/payload.txt').read_text(), 'first')
        self.assertTrue((self.run / '1/build.tar').is_file())
        self.assertIn('backend=host', self.output.read_text())

    def test_run_retention_records_maximum_across_attempts(self):
        self.ok(self.invoke('put', ARTIFACT_RETENTION_DAYS='14'))
        record = self.run / '.retention-days'
        self.assertTrue(record.is_file())
        self.assertEqual(record.read_text().strip(), '14')
        self.ok(self.invoke('put', GITHUB_RUN_ATTEMPT='2', ARTIFACT_RETENTION_DAYS='7'))
        self.ok(self.invoke('put', GITHUB_RUN_ATTEMPT='3', ARTIFACT_RETENTION_DAYS=''))
        self.assertEqual(record.read_text().strip(), '14')
        old = time.time() - 8 * 86400
        os.utime(self.run, (old, old))
        self.ok(self.invoke('put', GITHUB_RUN_ID='200', ARTIFACT_RETENTION_DAYS='1'))
        self.assertTrue(self.run.exists())
        expired = time.time() - 15 * 86400
        os.utime(self.run, (expired, expired))
        self.ok(self.invoke('put', GITHUB_RUN_ID='200'))
        self.assertFalse(self.run.exists())

    def test_archive_is_published_readable_with_restrictive_umask(self):
        previous = os.umask(0o077)
        try:
            self.ok(self.invoke('put'))
        finally:
            os.umask(previous)
        self.assertEqual((self.run / '1/build.tar').stat().st_mode & 0o777, 0o644)

    def test_prune_failure_warns_after_successful_publish(self):
        for failure in (OSError('cannot prune'), ValueError('invalid retention metadata')):
            with self.subTest(failure=failure), patch.dict(os.environ, self.env):
                output = io.StringIO()
                error = None
                with redirect_stdout(output), patch.object(transport, 'prune', side_effect=failure):
                    try:
                        transport.transfer('put', 'build', str(self.source), str(self.cache))
                    except (OSError, ValueError) as exception:
                        error = exception
                self.assertIsNone(error, 'Pruning must not fail a published put')
                self.assertIn('::warning::', output.getvalue())
                self.ok(self.invoke('get'))

    def test_mixed_pool_error_and_rollout_note(self):
        result = self.invoke('get')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('mixed pool', result.stderr)
        readme = (ACTION / 'README.md').read_text()
        self.assertIn('same apply', readme)
        self.assertIn('every runner', readme)

    def test_pack_does_not_hold_repository_lock(self):
        actual = transport.subprocess.run

        def inspect(*args, **kwargs):
            with (self.run.parent / '.lock').open('a') as lock:
                try:
                    transport.fcntl.flock(lock, transport.fcntl.LOCK_EX | transport.fcntl.LOCK_NB)
                except BlockingIOError:
                    self.fail('Packing held the repository lock')
            return actual(*args, **kwargs)

        with patch.dict(os.environ, self.env), patch.object(transport.subprocess, 'run', side_effect=inspect):
            transport.transfer('put', 'build', str(self.source), str(self.cache))

    def test_directory_layout_modes_and_hidden_files(self):
        source = self.root / 'directory'
        (source / 'nested').mkdir(parents=True)
        (source / 'nested/tool').write_text('binary')
        (source / 'nested/tool').chmod(0o755)
        (source / '.metadata').write_text('metadata')
        self.ok(self.invoke('put', path=source, ARTIFACT_INCLUDE_HIDDEN_FILES='true'))
        self.ok(self.invoke('get'))
        self.assertTrue((self.root / 'restored/nested/tool').is_file())
        self.assertEqual((self.root / 'restored/nested/tool').read_text(), 'binary')
        self.assertEqual((self.root / 'restored/nested/tool').stat().st_mode & 0o777, 0o755)
        self.assertEqual((self.root / 'restored/.metadata').read_text(), 'metadata')
        self.assertFalse((self.root / 'restored/directory').exists())

    def test_hidden_files_are_excluded_by_default(self):
        source = self.root / 'directory'
        (source / 'nested/.hidden').mkdir(parents=True)
        (source / 'nested/visible').write_text('visible')
        (source / 'nested/.hidden/inside').write_text('hidden')
        (source / '.metadata').write_text('hidden')
        self.ok(self.invoke('put', path=source))
        self.ok(self.invoke('get'))
        self.assertTrue((self.root / 'restored/nested/visible').is_file())
        self.assertFalse((self.root / 'restored/.metadata').exists())
        self.assertFalse((self.root / 'restored/nested/.hidden').exists())

    def test_get_can_share_a_read_lock(self):
        self.ok(self.invoke('put'))
        driver = '''
import importlib.util, os, sys
spec = importlib.util.spec_from_file_location('transfer', sys.argv[1])
t = importlib.util.module_from_spec(spec); spec.loader.exec_module(t)
t.transfer('get', 'build', sys.argv[2], os.environ['HOST_ARTIFACT_DIR'])
'''
        with (self.run.parent / '.lock').open('a') as lock:
            transport.fcntl.flock(lock, transport.fcntl.LOCK_SH)
            try:
                result = subprocess.run([sys.executable, '-c', driver, str(ACTION / 'host-artifact.py'),
                                         str(self.root / 'restored')], env=self.env,
                                        capture_output=True, text=True, timeout=2)
            except subprocess.TimeoutExpired:
                self.fail('Get blocked behind a shared reader lock')
            self.ok(result)
        self.assertTrue((self.root / 'restored/payload.txt').is_file())

    def test_get_searches_earlier_attempts_per_name(self):
        self.ok(self.invoke('put'))
        self.source.write_text('other')
        self.ok(self.invoke('put', name='other', GITHUB_RUN_ATTEMPT='3'))
        self.ok(self.invoke('get', GITHUB_RUN_ATTEMPT='4'))
        self.assertEqual((self.root / 'restored/payload.txt').read_text(), 'first')

    def test_get_uses_newest_available_attempt(self):
        self.ok(self.invoke('put'))
        self.source.write_text('newest')
        self.ok(self.invoke('put', GITHUB_RUN_ATTEMPT='2'))
        self.ok(self.invoke('get', GITHUB_RUN_ATTEMPT='3'))
        self.assertEqual((self.root / 'restored/payload.txt').read_text(), 'newest')

    def test_get_does_not_read_future_attempt(self):
        self.ok(self.invoke('put', GITHUB_RUN_ATTEMPT='2'))
        self.assertNotEqual(self.invoke('get').returncode, 0)

    def test_missing_name_fails_without_fallback(self):
        result = self.invoke('get', name='absent')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Host artifact "absent" is missing', result.stderr)
        self.assertNotIn('using GitHub artifacts', result.stdout)

    def test_isolates_repository_run_and_name(self):
        self.ok(self.invoke('put'))
        for env in ({'GITHUB_REPOSITORY': 'elsewhere/project'}, {'GITHUB_RUN_ID': '101'}):
            with self.subTest(env=env):
                self.assertNotEqual(self.invoke('get', **env).returncode, 0)
        self.assertNotEqual(self.invoke('get', name='different').returncode, 0)

    def test_invalid_keys_and_mode_fail(self):
        for mode, name, env in [('bad', 'build', {}), ('put', '../escape', {}),
                                ('put', 'build', {'GITHUB_RUN_ID': '../escape'}),
                                ('put', 'build', {'GITHUB_REPOSITORY': '../escape'})]:
            with self.subTest(mode=mode, name=name, env=env):
                self.assertNotEqual(self.invoke(mode, name=name, **env).returncode, 0)

    def test_missing_source_fails(self):
        result = self.invoke('put', path=self.root / 'absent')
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.run / '1/build.tar').exists())

    def test_put_publishes_only_after_pack_finishes(self):
        self.ok(self.invoke('put'))
        final = self.run / '1/build.tar'
        original = final.read_bytes()
        actual_run = transport.subprocess.run

        def inspect(command, **kwargs):
            self.assertNotEqual(Path(command[2]), final)
            self.assertEqual(final.read_bytes(), original)
            return actual_run(command, **kwargs)

        self.source.write_text('replacement')
        with patch.dict(os.environ, self.env), patch.object(transport.subprocess, 'run', side_effect=inspect):
            transport.transfer('put', 'build', str(self.source), str(self.cache))
        self.ok(self.invoke('get'))
        self.assertEqual((self.root / 'restored/payload.txt').read_text(), 'replacement')

    def test_failed_pack_preserves_archive_and_cleans_temporary(self):
        self.ok(self.invoke('put'))
        original = (self.run / '1/build.tar').read_bytes()
        with patch.dict(os.environ, self.env), patch.object(transport.subprocess, 'run',
                side_effect=subprocess.CalledProcessError(1, 'tar')):
            with self.assertRaises(subprocess.CalledProcessError):
                transport.transfer('put', 'build', str(self.source), str(self.cache))
        self.assertEqual((self.run / '1/build.tar').read_bytes(), original)
        self.assertEqual(sorted(p.name for p in (self.run / '1').iterdir()), ['build.tar'])
        self.assertEqual(list(self.run.parent.glob('.put-*')), [])

    def test_pruning_respects_age_current_run_and_repository(self):
        self.ok(self.invoke('put'))
        repository = self.run.parent
        for name, age in [('90', 4), ('91', 2), ('notes', 4), ('100', 4)]:
            directory = repository / name
            directory.mkdir(exist_ok=True)
            stamp = time.time() - age * 86400
            os.utime(directory, (stamp, stamp))
        other = self.cache / 'example/other/90'
        other.mkdir(parents=True)
        old = time.time() - 4 * 86400
        os.utime(other, (old, old))
        self.ok(self.invoke('put', name='second'))
        self.assertFalse((repository / '90').exists())
        for directory in [repository / '91', repository / 'notes', self.run, other]:
            self.assertTrue(directory.is_dir(), directory)
        self.assertGreater(self.run.stat().st_mtime, time.time() - 60)

    def wait_for(self, path, process):
        deadline = time.monotonic() + 5
        while not path.exists() and process.poll() is None and time.monotonic() < deadline:
            time.sleep(.01)
        self.assertTrue(path.exists(), 'Child did not reach synchronization point')

    def test_pruning_waits_for_entire_get(self):
        self.ok(self.invoke('put'))
        old = time.time() - 4 * 86400
        os.utime(self.run, (old, old))
        reader_ready = self.root / 'reader-ready'
        release = self.root / 'release'
        writer_ready = self.root / 'writer-ready'
        driver = '''
import importlib.util, os, pathlib, sys, time
spec = importlib.util.spec_from_file_location('transfer', sys.argv[1])
t = importlib.util.module_from_spec(spec); spec.loader.exec_module(t)
mode, ready, release, path = sys.argv[2:]
if mode == 'get':
    actual = t.subprocess.run
    def run(*args, **kwargs):
        pathlib.Path(ready).touch()
        deadline = time.monotonic() + 8
        while not pathlib.Path(release).exists():
            if time.monotonic() > deadline: raise RuntimeError('Synchronization timeout')
            time.sleep(.01)
        return actual(*args, **kwargs)
    t.subprocess.run = run
else:
    actual = t.fcntl.flock
    def flock(*args):
        pathlib.Path(ready).touch()
        return actual(*args)
    t.fcntl.flock = flock
t.transfer(mode, 'build', path, os.environ['HOST_ARTIFACT_DIR'])
'''
        reader = subprocess.Popen([sys.executable, '-c', driver, str(ACTION / 'host-artifact.py'),
                                   'get', str(reader_ready), str(release), str(self.root / 'restored')],
                                  env=self.env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        writer = None
        try:
            self.wait_for(reader_ready, reader)
            writer = subprocess.Popen([sys.executable, '-c', driver, str(ACTION / 'host-artifact.py'),
                                       'put', str(writer_ready), str(release), str(self.source)],
                                      env=dict(self.env, GITHUB_RUN_ID='200'),
                                      stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            self.wait_for(writer_ready, writer)
            with self.assertRaises(subprocess.TimeoutExpired):
                writer.wait(timeout=.2)
            self.assertTrue(self.run.is_dir())
            release.touch()
            self.assertEqual(reader.wait(timeout=5), 0)
            self.assertEqual(writer.wait(timeout=5), 0)
            self.assertEqual((self.root / 'restored/payload.txt').read_text(), 'first')
            self.assertFalse(self.run.exists())
        finally:
            release.touch()
            for process in (reader, writer):
                if process is not None:
                    if process.poll() is None:
                        process.kill()
                    process.communicate(timeout=5)

    def test_unset_or_empty_fallback(self):
        for mode, root in [('put', ''), ('get', ''), ('put', None), ('get', None)]:
            with self.subTest(mode=mode, root=root):
                self.output.unlink(missing_ok=True)
                result = self.invoke(mode, HOST_ARTIFACT_DIR=root)
                self.ok(result)
                self.assertEqual(self.output.read_text(), 'backend=github\n')
                self.assertIn(f'using GitHub artifacts ({mode})', result.stdout)
                self.assertFalse(self.cache.exists())
        # Check the actual fallback steps and guards without contacting GitHub.
        action = (ACTION / 'action.yml').read_text()
        for mode, operation in [('put', 'upload'), ('get', 'download')]:
            self.assertIn(f"    - uses: actions/{operation}-artifact@v4\n"
                          f"      if: steps.transport.outputs.backend == 'github' && inputs.mode == '{mode}'", action)
        self.assertEqual(action.count('        name: ${{ inputs.name }}'), 2)
        self.assertEqual(action.count('        path: ${{ inputs.path }}'), 2)
        self.assertIn('        if-no-files-found: error', action)
        self.assertIn('        retention-days: ${{ inputs.retention-days }}', action)
        self.assertIn('        include-hidden-files: ${{ inputs.include-hidden-files }}', action)
        self.assertRegex(action, r"include-hidden-files:\n    description: [^\n]+\n    default: 'false'")
        self.assertRegex(action, r'retention-days:\n    description: [^\n]+\n  include-hidden-files:')


if __name__ == '__main__':
    unittest.main()
