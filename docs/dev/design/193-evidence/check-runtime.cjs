// Design-only diagnostic: run against the unchanged runtime of the pinned tree.
// Usage: npm install --no-save bignumber.js@9.1.2
//        node check-runtime.cjs Source/DafnyRuntime/DafnyRuntimeJs/DafnyRuntime.js
const fs = require('fs');
const vm = require('vm');
const crypto = require('crypto');
const runtime = fs.readFileSync(process.argv[2], 'utf8');
console.log('runtime_sha256=' + crypto.createHash('sha256').update(runtime).digest('hex'));
console.log('bignumber_version=9.1.2 node_version=' + process.version);
vm.runInNewContext(runtime + `
for (const k of [8, 30, 31, 32, 53, 63]) {
  const x = new BigNumber(2).pow(k);
  for (const op of ['BitwiseAnd','BitwiseOr','BitwiseXor']) {
    const other = op === 'BitwiseXor' ? new BigNumber(0) : x;
    console.log(op + '\\tk=' + k + '\\tactual=' + _dafny[op](x, other).toFixed() + '\\texpected=' + x.toFixed());
  }
}
`, { require, console });
