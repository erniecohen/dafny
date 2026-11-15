// Direct runtime checks and a reproducible public-runner microbenchmark.
// Usage: node .github/review/246-js-bitwise.cjs RUNTIME [--bench]
const assert = require('assert');
const fs = require('fs');
const vm = require('vm');
const BigNumber = require('bignumber.js');
const context = { require, console, process };
vm.runInNewContext(fs.readFileSync(process.argv[2], 'utf8') + '\nthis.runtime = _dafny;', context);
const runtime = context.runtime;
const bn = x => new BigNumber(x.toString());
const operations = {
  BitwiseAnd: (a, b) => a & b,
  BitwiseOr: (a, b) => a | b,
  BitwiseXor: (a, b) => a ^ b
};
let seed = 0x246c0de;
function random32() {
  seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5;
  return seed >>> 0;
}
function randomBits(width) {
  let n = 0n;
  for (let i = 0; i < width; i += 32) n = (n << 32n) | BigInt(random32());
  return n & ((1n << BigInt(width)) - 1n);
}
function equal(actual, expected, label) {
  assert.strictEqual(actual.toFixed(), expected.toString(), label);
  assert(actual.isInteger() && !actual.isNegative(), label + ': unsigned integer');
}
function pair(a, b) {
  for (const [name, op] of Object.entries(operations)) {
    equal(runtime[name](bn(a), bn(b)), op(a, b), `${name}(${a}, ${b})`);
  }
}
if (process.argv.includes('--bench')) {
  // Convert operands before timing, but retain all conversions inside each helper.
  // Mixed dense/sparse inputs, unequal lengths and zero; widths used by compiled tests.
  const iterations = 20000;
  let sink;
  console.log(`Node ${process.version}; bignumber.js ${require('bignumber.js/package.json').version}`);
  console.log('| Width | Helper | ns/op (median of 5) |');
  console.log('|---:|---|---:|');
  for (const width of [32, 33, 53, 64, 65, 256]) {
    const mask = (1n << BigInt(width)) - 1n;
    const inputs = Array.from({length: 128}, (_, i) => [
      bn(i % 3 === 0 ? mask : randomBits(width)),
      bn(i % 5 === 0 ? 0n : randomBits(i % 2 ? width : Math.floor(width / 2)))
    ]);
    for (const name of Object.keys(operations)) {
      const fn = runtime[name];
      for (let i = 0; i < 5000; i++) sink = fn(...inputs[i & 127]);
      const times = [];
      for (let run = 0; run < 5; run++) {
        const start = process.hrtime.bigint();
        for (let i = 0; i < iterations; i++) sink = fn(...inputs[i & 127]);
        times.push(Number(process.hrtime.bigint() - start) / iterations);
      }
      times.sort((a, b) => a - b);
      console.log(`| ${width} | ${name} | ${times[2].toFixed(1)} |`);
    }
  }
  assert(sink !== undefined);
} else {
  const values = [0n, 1n];
  for (const bit of [30, 31, 32, 52, 53, 63, 64]) {
    const p = 1n << BigInt(bit);
    values.push(p - 1n, p, p + 1n, p | 0xaaaaaaaan);
  }
  // Include sparse bits beyond the old 53-bit limbs and unequal operand lengths.
  values.push((1n << 159n) | (1n << 106n) | (1n << 53n) | 1n, (1n << 257n) - 1n);
  function identities(x) {
    equal(runtime.BitwiseAnd(bn(x), bn(x)), x, 'x & x');
    equal(runtime.BitwiseOr(bn(x), bn(0n)), x, 'x | 0');
    equal(runtime.BitwiseXor(bn(x), bn(0n)), x, 'x ^ 0');
    equal(runtime.BitwiseXor(bn(x), bn(x)), 0n, 'x ^ x');
  }
  for (const a of values) {
    identities(a);
    for (const b of values) pair(a, b);
  }
  for (let i = 0; i < 10000; i++) {
    const a = randomBits(random32() % 513);
    const b = randomBits(random32() % 513);
    pair(a, b);
    identities(a);
  }
  // Related-helper audit: exact arithmetic, no signed host-limb operations.
  for (const width of [0, 1, 30, 31, 32, 33, 52, 53, 63, 64, 65, 106, 159, 257]) {
    const w = BigInt(width), mask = (1n << w) - 1n;
    for (const x of [0n, mask, randomBits(width)]) {
      equal(runtime.BitwiseNot(bn(x), width), mask ^ x, 'NOT');
      for (const n of new Set([0, Math.floor(width / 2), Math.max(0, width - 1), width])) {
        const k = BigInt(n);
        equal(runtime.ShiftLeft(bn(x), n), x << k, 'shift left');
        equal(runtime.ShiftRight(bn(x), n), x >> k, 'shift right');
        equal(runtime.RotateLeft(bn(x), n, width), ((x << k) & mask) | (x >> (w - k)), 'rotate left');
        equal(runtime.RotateRight(bn(x), n, width), (x >> k) | ((x << (w - k)) & mask), 'rotate right');
      }
    }
  }
  console.log('Wide JavaScript helpers: boundary cases, 10000 fixed-seed pairs, identities, shifts, rotations and NOT passed');
}
