"""Exercise admission with real advisory locks, including owner loss and termination."""
import importlib.util
import os
from unittest.mock import patch
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

ACTION = Path(__file__).resolve().parents[1] / 'actions/self-hosted-job'
spec = importlib.util.spec_from_file_location('admission', ACTION / 'admit.py')
admission = importlib.util.module_from_spec(spec)
spec.loader.exec_module(admission)
POLICY = dict(reserve_mb=100, default_peak_mb=1000, peak_min_samples=3, peak_history_days=30,
              probe_timeout_s=2, slot_poll_s=.05, memory_backoff_s=[.05, .1, .2])


def keeper_command(lock, ready, pid, owner, slots='2'):
    # Fake only system readings; keep real locks, lifetime and cleanup.
    import json
    script = ('import runpy, sys; sys.path.insert(0, sys.argv.pop(1)); '
              'import admit; admit.free_percent=lambda timeout: 100; '
              'admit.physical_mb=lambda timeout: 1000000; admit.pressure_level=lambda timeout: 1; '
              'import signal; signal.signal(signal.SIGTERM, admit.stop); '
              'admit.hold(sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4], '
              'int(sys.argv[5]), __import__("json").loads(sys.argv[6]), repository="example/repo")')
    return [sys.executable, '-c', script, str(ACTION), str(lock), str(ready), str(pid), owner, slots,
            json.dumps(POLICY)]



def setUpModule():
    global environment
    environment = patch.dict(os.environ, {k: v for k, v in os.environ.items()
                                         if not k.startswith('ADMISSION_')}, clear=True)
    environment.start()


def tearDownModule():
    environment.stop()


class AdmissionTests(unittest.TestCase):
    def wait_ready(self, path):
        for _ in range(100):
            if path.exists():
                return
            time.sleep(.05)
        self.fail('Admission did not complete')

    def test_serializes_and_recovers_after_keeper_termination(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            keepers = []
            try:
                time.sleep(.2)  # macOS Python launchers can re-exec before reaching the sleep.
                identity = admission.identity(owner.pid)
                for n in range(2):
                    keepers.append(subprocess.Popen(keeper_command(root / 'lock', root / str(n), owner.pid, identity, '1')))
                    if n == 0:
                        self.wait_ready(root / '0')
                time.sleep(.2)
                self.assertFalse((root / '1').exists())
                keepers[0].terminate()
                keepers[0].wait(timeout=5)
                self.wait_ready(root / '1')
                owner.terminate()
                owner.wait(timeout=5)
                self.assertEqual(keepers[1].wait(timeout=5), 0)
            finally:
                for child in [*keepers, owner]:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)

    def test_wrong_owner_identity_never_admits(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            child = subprocess.run(keeper_command(root / 'lock', root / 'ready', os.getpid(), 'wrong identity'), timeout=5)
            self.assertEqual(child.returncode, 0)
            self.assertFalse((root / 'ready').exists())

    def test_configured_two_slots_hold_third_job_until_release(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            keepers = []
            try:
                for n in range(3):
                    keepers.append(subprocess.Popen(keeper_command(root / 'lock', root / str(n), os.getpid(), admission.identity(os.getpid()))))
                    if n < 2:
                        self.wait_ready(root / str(n))
                time.sleep(1.2)
                self.assertFalse((root / '2').exists())
                self.assertTrue(all(child.poll() is None for child in keepers))
                keepers[0].terminate(); keepers[0].wait(timeout=5)
                self.wait_ready(root / '2')
                self.assertIsNone(keepers[1].poll())
            finally:
                for child in keepers:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)

    def test_invalid_capacity_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'positive'):
            admission.hold('unused', 'unused', 0, '', 0, POLICY)

class CapacityInputTests(unittest.TestCase):
    def test_required_and_configured_capacity_and_invalid_inputs(self):
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = """
const assert = require('node:assert/strict');
const {slots} = require(process.argv[1]);
delete process.env.INPUT_SLOTS;
assert.throws(() => slots(), /positive safe integer/);
process.env.INPUT_SLOTS = '3';
assert.equal(slots(), 3);
assert.equal(slots('1'), 1);
for (const bad of ['0', '-1', '2.5', 'NaN', '3garbage', '9007199254740992']) {
  assert.throws(() => slots(bad), /positive safe integer/);
}
"""
        subprocess.run([node, '-e', script, str(ACTION / 'index.cjs')], check=True, timeout=5)

    def test_required_policy_and_optional_peak_pass_through(self):
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = """
const assert = require('node:assert/strict');
const {memoryPolicy, callerPeak, admissionArgs} = require(process.argv[1]);
const env = {'INPUT_RESERVE-MB': '100', 'INPUT_DEFAULT-PEAK-MB': '1000', 'INPUT_PEAK-MIN-SAMPLES': '3',
  'INPUT_PEAK-HISTORY-DAYS': '30', 'INPUT_PROBE-TIMEOUT-S': '2', 'INPUT_SLOT-POLL-S': '1',
  'INPUT_MEMORY-BACKOFF-S': '[1,2,3]', INPUT_KIND: 'ci:Exact key', GITHUB_REPOSITORY: 'example/repo'};
const args = admissionArgs('/cache', '/ready', {pid: 123, identity: 'worker'}, 3, env);
assert.deepEqual(args.slice(1, 6), ['/cache/active.lock', '/ready', '123', 'worker', '3']);
assert.deepEqual(JSON.parse(args[6]), memoryPolicy(env));
assert.deepEqual(args.slice(7), ['ci:Exact key', 'example/repo', '']);
assert.equal(callerPeak({}), '');
assert.equal(callerPeak({'INPUT_EXPECTED-PEAK-MB': '2048.5'}), '2048.5');
assert.equal(admissionArgs('/cache', '/ready', {pid: 1, identity: 'w'}, 1,
  {...env, 'INPUT_EXPECTED-PEAK-MB': '2048.5'})[9], '2048.5');
for (const bad of ['[0]', '[true]', '[null]', '[1,-1]', '[1e999]', '["2"]']) {
  assert.throws(() => memoryPolicy({...env, 'INPUT_MEMORY-BACKOFF-S': bad}));
}
for (const key of Object.keys(env).filter(k => k.startsWith('INPUT_') && k !== 'INPUT_KIND')) {
  const missing = {...env}; delete missing[key]; assert.throws(() => memoryPolicy(missing));
  for (const bad of ['0', '-1', 'true', 'null', '"2"', '1e999', '[]']) {
    assert.throws(() => memoryPolicy({...env, [key]: bad}), key + bad);
  }
}
assert.throws(() => memoryPolicy({...env, 'INPUT_PEAK-MIN-SAMPLES': '1.5'}));
for (const bad of ['0', '-1', 'true', 'null', '"2"', '1e999']) {
  assert.throws(() => callerPeak({'INPUT_EXPECTED-PEAK-MB': bad}));
}
assert.equal(admissionArgs('/cache', '/ready', {pid: 1, identity: 'w'}, 1,
  {...env, INPUT_KIND: '', GITHUB_WORKFLOW: 'Review'})[7], 'ci:Review');
"""
        subprocess.run([node, '-e', script, str(ACTION / 'index.cjs')], check=True, timeout=5)


class CleanupTests(unittest.TestCase):
    def test_post_removes_only_job_outputs_and_stops_its_keeper(self):
        import os
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner for the post action')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            # Never stop build servers belonging to the test caller.
            (root / 'dotnet').write_text('#!/bin/sh\nexit 0\n')
            (root / 'dotnet').chmod(0o755)
            repo = root / 'repo'; repo.mkdir()
            tmp = root / 'fork-job.example'; tmp.mkdir()
            unrelated = root / 'unrelated'; unrelated.mkdir()
            subprocess.run(['git', 'init', '-q', str(repo)], check=True)
            (repo / 'tracked').touch()
            subprocess.run(['git', '-C', str(repo), 'add', 'tracked'], check=True)
            (repo / 'output').touch()
            keeper = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            try:
                time.sleep(.2)
                env = dict(os.environ, STATE_keeper=str(keeper.pid), STATE_keeperIdentity=admission.identity(keeper.pid),
                           STATE_tmp=str(tmp), RUNNER_TEMP=str(root), GITHUB_WORKSPACE=str(repo),
                           PATH=str(root) + os.pathsep + os.environ['PATH'])
                subprocess.run([node, str(ACTION / 'index.cjs')], env=env, check=True, timeout=5)
                self.assertTrue((repo / 'tracked').exists())
                self.assertFalse((repo / 'output').exists())
                self.assertFalse(tmp.exists())
                self.assertTrue(unrelated.exists())
                self.assertNotEqual(keeper.wait(timeout=5), 0)
            finally:
                if keeper.poll() is None:
                    keeper.terminate()
                keeper.wait(timeout=5)


    def test_start_failure_and_workspace_failure_stop_only_own_keeper(self):
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = r"""
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {EventEmitter} = require('node:events');
const root = process.argv[2];
const source = fs.readFileSync(process.argv[1], 'utf8');
(async () => {
  for (const failure of ['start', 'workspace', 'state']) {
    const ready = path.join(root, 'admitted');
    fs.writeFileSync(ready, 'stale');
    let killed = 0;
    const child = new EventEmitter();
    child.pid = 456;
    child.kill = sig => { assert.equal(sig, 'SIGTERM'); killed++; };
    child.unref = () => {};
    const cp = {
      spawnSync: () => ({status: 0, stdout: 'Runner.Worker'}),
      spawn: (command, args, options) => {
        assert.equal(command, 'python3');
        assert.deepEqual(Array.from(options.stdio), ['ignore', 'inherit', 'inherit']);
        assert.equal(args[1], path.join(root, 'locks', 'active.lock'));
        assert.equal(fs.existsSync(ready), false);
        assert.deepEqual(Array.from(args.slice(7, 9)), ['ci:Exact key', 'example/repo']);
        if (failure === 'start') setImmediate(() => child.emit('error', new Error('cannot start')));
        else fs.writeFileSync(ready, 'published');
        return child;
      }
    };
    const sandbox = {
      module: {exports: {}}, __dirname: path.dirname(process.argv[1]),
      console: {log() {}, error() {}},
      setTimeout: callback => setImmediate(callback),
      process: {platform: 'darwin', arch: 'arm64', ppid: 123, env: {
        INPUT_SLOTS: '2', 'INPUT_RESERVE-MB': '100', 'INPUT_DEFAULT-PEAK-MB': '1000',
        'INPUT_PEAK-MIN-SAMPLES': '3', 'INPUT_PEAK-HISTORY-DAYS': '30', 'INPUT_PROBE-TIMEOUT-S': '2',
        'INPUT_SLOT-POLL-S': '1', 'INPUT_MEMORY-BACKOFF-S': '[1,2]',
        INPUT_KIND: 'ci:Exact key', GITHUB_REPOSITORY: 'example/repo',
        ADMISSION_LOCK_DIR: path.join(root, 'locks'), TMPDIR: root,
        GITHUB_STATE: failure === 'state' ? path.join(root, 'missing', 'state') : path.join(root, 'state'),
        GITHUB_ENV: path.join(root, 'env'),
        GITHUB_WORKSPACE: path.join(root, 'repo'), RUNNER_WORKSPACE: path.join(root, 'wrong-parent')
      }},
      require: name => name === 'node:child_process' ? cp : require(name)
    };
    vm.runInNewContext(source, sandbox);
    const pattern = failure === 'start' ? /keeper exited/ : failure === 'state' ? /ENOENT/ : /immediate child/;
    await assert.rejects(sandbox.module.exports.main(), pattern);
    assert.equal(killed, 1);
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'repo').mkdir()
            (root / 'wrong-parent').mkdir()
            subprocess.run([node, '-e', script, str(ACTION / 'index.cjs'), str(root)], check=True, timeout=5)


class WaitingCancellationTests(unittest.TestCase):
    def test_waiting_keeper_exits_when_its_owner_disappears(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            keepers = []
            try:
                time.sleep(.2)
                for n, pid in enumerate([os.getpid(), owner.pid]):
                    keepers.append(subprocess.Popen(keeper_command(root / 'lock', root / str(n), pid, admission.identity(pid), '1')))
                    if n == 0:
                        AdmissionTests.wait_ready(self, root / '0')
                time.sleep(.2)
                self.assertFalse((root / '1').exists())
                owner.terminate(); owner.wait(timeout=5)
                self.assertEqual(keepers[1].wait(timeout=5), 0)
                self.assertIsNone(keepers[0].poll())
            finally:
                for child in [*keepers, owner]:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)


if __name__ == '__main__':
    unittest.main()
