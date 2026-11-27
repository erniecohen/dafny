// Real CommonJS exports, shared by require's cache and generated declarations.
globalThis.issue466RequiredLoads = (globalThis.issue466RequiredLoads || 0) + 1;
const foreign = {
  __default: class {},
  Actual: class {
    constructor() { this.number = globalThis.issue466RequiredInt(19); }
    Read() { return globalThis.issue466RequiredInt(13); }
    Increment(x) { return x.plus(1); }
  },
  Handle: class {
    static Make() { return new this(); }
    Read() { return globalThis.issue466RequiredInt(23); }
  },
  NativeEcho(x) { return x; },
  NativeCount(n) {
    if (require(__filename) !== foreign || globalThis.issue466RequiredLoads !== 1) {
      throw new Error('Required module identity or load count changed');
    }
    // Generated code must extend this captured object, rather than a copy.
    return peer.NativeCount(n);
  }
};
module.exports = foreign;

// Publish before entering a native CommonJS cycle. Callbacks run after loading.
const peer = require('./issue466_required_peer.js');
