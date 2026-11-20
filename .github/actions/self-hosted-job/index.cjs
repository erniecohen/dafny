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
function slots(value = process.env.INPUT_SLOTS || '2') {
  if (!/^[1-9][0-9]*$/.test(value) || !Number.isSafeInteger(Number(value))) {
    throw new Error('Admission slots must be a positive safe integer');
  }
  return Number(value);
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
async function main() {
  if (process.env.STATE_keeper) {
    // The action is cached outside the checkout by Actions, so cleaning untracked outputs
    // does not remove this post action. Never remove tracked files or another job's checkout.
    try {
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
  const owner = worker();
  const capacity = slots();
  // Numbered advisory locks shared by all runners and both forks. The OS releases them even
  // after a hard cancellation. Waiting jobs are never discarded as concurrency-group
  // pending jobs would be. The keeper also exits when its Runner.Worker disappears.
  const cache = path.join(os.homedir(), 'Library', 'Caches', 'fork-actions-admission');
  fs.mkdirSync(cache, {recursive: true});
  const ready = path.join(process.env.TMPDIR, 'admitted');
  const keeper = cp.spawn('python3', [path.join(__dirname, 'admit.py'),
    path.join(cache, 'active.lock'), ready, String(owner.pid), owner.identity, String(capacity)],
    {stdio: 'ignore'});
  for (const [key, value] of Object.entries({keeper: keeper.pid,
    keeperIdentity: identity(keeper.pid), tmp: process.env.TMPDIR})) {
    fs.appendFileSync(process.env.GITHUB_STATE, `${key}=${value}\n`);
  }
  let exited = false;
  keeper.on('exit', () => { exited = true; });
  keeper.on('error', e => { console.error(e.message); exited = true; });
  while (!fs.existsSync(ready)) {
    if (exited) throw new Error('Admission keeper exited before acquiring the job slot');
    console.log('Waiting for the shared self-hosted job slot');
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  fs.appendFileSync(process.env.GITHUB_STATE, `keeperIdentity=${identity(keeper.pid)}\n`);
  keeper.unref();
  prepareWorkspace(process.env.GITHUB_WORKSPACE, process.env.RUNNER_WORKSPACE);
  console.log(`Admitted one of ${capacity} jobs; excess fork jobs wait until post cleanup`);
}
if (require.main === module) main().catch(e => { console.error(e.message); process.exitCode = 1; });
module.exports = {identity, worker, slots, prepareWorkspace};
