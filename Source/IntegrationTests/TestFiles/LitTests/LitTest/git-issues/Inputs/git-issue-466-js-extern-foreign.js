// Preloaded module objects use the same public paths as ordinary emission.
let Foreign = { Tools: {
  Increment(x) { return x.plus(1); },
  Identity(x) { return x; },
  Echo(x) { return x; },
  ExternalEval() { return new BigNumber(17); }
} };
let ForeignB = {
  __default: class {},
  Actual: class {
    constructor() { this.number = new BigNumber(19); }
    Read() { return new BigNumber(13); }
    Increment(x) { return x.plus(1); }
  },
  Handle: class {
    static Make() { return new this(); }
    Read() { return new BigNumber(23); }
  }
};
let NativeOnly = { Zero() { return new BigNumber(0); } };

// Ordinary function qualifiers use an underscore; static methods use a dot.
Foreign.Tools_Identity = Foreign.Tools.Identity;
Foreign.Tools_Echo = Foreign.Tools.Echo;
Foreign.Tools_ExternalEval = Foreign.Tools.ExternalEval;
