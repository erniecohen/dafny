const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const cp = require('node:child_process');

function identity(pid) {
  const r = cp.spawnSync('ps', ['-p', String(pid), '-o', 'lstart=,comm='], {encoding: 'utf8'});
  return r.status === 0 ? r.stdout.trim() : '';
}
function worker() {
  let pid = process.ppid;
  while (pid > 1) {
    const id = identity(pid);
    if (/Runner\.Worker/.test(id)) return {pid, identity: id};
    const r = cp.spawnSync('ps', ['-p', String(pid), '-o', 'ppid='], {encoding: 'utf8'});
    pid = Number(r.stdout.trim());
  }
  throw new Error('Expected a Runner.Worker ancestor');
}
function slots(value = process.env.INPUT_SLOTS) {
  if (!/^[1-9][0-9]*$/.test(value) || !Number.isSafeInteger(Number(value))) {
    throw new Error('Admission slots must be a positive safe integer');
  }
  return Number(value);
}
function memoryPolicy(env = process.env) {
  const policy = {};
  for (const name of ['reserve_mb', 'default_peak_mb', 'peak_min_samples', 'peak_history_days',
    'probe_timeout_s', 'slot_poll_s', 'memory_backoff_s']) {
    const input = env[`INPUT_${name.toUpperCase().replaceAll('_', '-')}`];
    let value;
    try { value = JSON.parse(input); } catch { throw new Error(`${name} is required and must be valid JSON`); }
    const positive = v => typeof v === 'number' && Number.isFinite(v) && v > 0;
    if (name === 'memory_backoff_s' ? !Array.isArray(value) || !value.length || !value.every(positive)
      : !positive(value) || (name === 'peak_min_samples' && !Number.isSafeInteger(value))) {
      throw new Error(`${name} must contain positive finite values (integer sample count)`);
    }
    policy[name] = value;
  }
  return policy;
}
function callerPeak(env = process.env) {
  const input = env['INPUT_EXPECTED-PEAK-MB'];
  if (input === undefined || input.trim() === '') return '';
  let value;
  try { value = JSON.parse(input); } catch { throw new Error('expected-peak-mb must be positive and finite'); }
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    throw new Error('expected-peak-mb must be positive and finite');
  }
  return JSON.stringify(value);
}
function prepareWorkspace(workspace, runnerWorkspace) {
  // Only this runner's repository directory may be cleared. The cached action and
  // RUNNER_TEMP live outside it. Remove stale Git metadata as well as build outputs:
  // checkout's working-tree cleanup cannot repair colliding remote refs.
  const root = fs.realpathSync(workspace);
  if (path.dirname(root) !== fs.realpathSync(runnerWorkspace)) {
    throw new Error('Workspace must be an immediate child of RUNNER_WORKSPACE');
  }
  for (const entry of fs.readdirSync(root)) {
    fs.rmSync(path.join(root, entry), {recursive: true, force: true});
  }
}
function admissionArgs(cache, ready, owner, capacity, env = process.env) {
  return [path.join(__dirname, 'admit.py'), path.join(cache, 'active.lock'), ready,
    String(owner.pid), owner.identity, String(capacity), JSON.stringify(memoryPolicy(env)),
    env.INPUT_KIND || `ci:${env.GITHUB_WORKFLOW || ''}`, env.GITHUB_REPOSITORY || '', callerPeak(env)];
}
function configureBuildEnvironment() {
  for (const [key, value] of Object.entries({MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false'})) {
    process.env[key] = value;
    fs.appendFileSync(process.env.GITHUB_ENV, `${key}=${value}\n`);
  }
}
async function main() {
  if (process.env.STATE_keeper) {
    // The action is cached outside the checkout by Actions, so cleaning untracked outputs
    // does not remove this post action. Never remove tracked files or another job's checkout.
    try {
      // Best effort even when setup never installed an SDK. Keep the reservation
      // until shutdown finishes; a missing dotnet or a failed shutdown is harmless.
      cp.spawnSync('dotnet', ['build-server', 'shutdown'], {stdio: 'ignore', timeout: 30000});
      if (fs.existsSync(path.join(process.env.GITHUB_WORKSPACE, '.git'))) {
        const r = cp.spawnSync('git', ['clean', '-ffdx'], {cwd: process.env.GITHUB_WORKSPACE, stdio: 'ignore'});
        if (r.status !== 0) throw new Error('Checkout output cleanup failed');
      }
      const tmp = process.env.STATE_tmp;
      const base = fs.realpathSync(process.env.RUNNER_TEMP);
      if (tmp && fs.existsSync(tmp) && path.dirname(fs.realpathSync(tmp)) === base && path.basename(tmp).startsWith('fork-job.')) {
        fs.rmSync(tmp, {recursive: true, force: true});
      }
    } finally {
      // Only stop this job's keeper, and only if its process identity still matches.
      const pid = Number(process.env.STATE_keeper);
      if (identity(pid) === process.env.STATE_keeperIdentity) process.kill(pid, 'SIGTERM');
    }
    return;
  }
  if (process.platform !== 'darwin' || process.arch !== 'arm64') throw new Error('Requires macOS ARM64');
  const capacity = slots();
  memoryPolicy();
  callerPeak();
  const owner = worker();
  // Numbered advisory locks shared by all runners and both forks. The OS releases them even
  // after a hard cancellation. Waiting jobs are never discarded as concurrency-group
  // pending jobs would be. The keeper also exits when its Runner.Worker disappears.
  const cache = process.env.ADMISSION_LOCK_DIR ?? path.join(os.homedir(), 'Library', 'Caches', 'fork-actions-admission');
  fs.mkdirSync(cache, {recursive: true});
  const ready = path.join(process.env.TMPDIR, 'admitted');
  fs.rmSync(ready, {force: true});
  const keeper = cp.spawn('python3', admissionArgs(cache, ready, owner, capacity),
    {stdio: ['ignore', 'inherit', 'inherit']});
  let exited = false;
  keeper.on('exit', () => { exited = true; });
  keeper.on('error', e => { console.error(e.message); exited = true; });
  try {
    for (const [key, value] of Object.entries({keeper: keeper.pid,
      keeperIdentity: identity(keeper.pid), tmp: process.env.TMPDIR})) {
      fs.appendFileSync(process.env.GITHUB_STATE, `${key}=${value}\n`);
    }
    console.log('Waiting for host admission');
    while (!fs.existsSync(ready)) {
      if (exited) throw new Error('Admission keeper exited before acquiring the job slot');
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    fs.appendFileSync(process.env.GITHUB_STATE, `keeperIdentity=${identity(keeper.pid)}\n`);
    keeper.unref();
    configureBuildEnvironment();
    prepareWorkspace(process.env.GITHUB_WORKSPACE, process.env.RUNNER_WORKSPACE);
    console.log('Admitted self-hosted job; reservation held until post cleanup');
  } catch (error) {
    keeper.kill('SIGTERM');
    throw error;
  }
}
if (require.main === module) main().catch(e => { console.error(e.message); process.exitCode = 1; });
module.exports = {identity, worker, slots, memoryPolicy, callerPeak, prepareWorkspace, admissionArgs, configureBuildEnvironment, main};
