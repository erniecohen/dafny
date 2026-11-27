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

ForeignB.NativeCell = class {
  constructor(rtd, seed, tag) {
    if (arguments.length !== 3) { throw new Error("native constructor arity"); }
    console.log("native cell " + tag);
    this.value = seed;
    this.tag = tag;
    this.defaultValue = rtd.Default;
  }
  Read() { return this.value; }
  Tag() { return this.tag; }
  InitialValue() { return this.defaultValue; }
};
ForeignB.NativeZero = class {
  constructor() {
    if (arguments.length !== 0) { throw new Error("ghost argument was emitted"); }
    console.log("native zero");
  }
  Read() { return new BigNumber(53); }
};

ForeignB.NativeInt = class {
  constructor(value) {
    if (arguments.length !== 1) { throw new Error("native integer constructor arity"); }
    console.log("native int " + value);
    this.value = value;
  }
  Read() { return this.value; }
};

let ForeignRefined = {
  __default: class {},
  NativeCell: class extends ForeignB.NativeCell {
    constructor(rtd, seed, tag) {
      super(rtd, seed, tag);
      console.log("refined cell " + tag);
    }
  },
  Actual: class extends ForeignB.Actual { Read() { return new BigNumber(79); } },
  Handle: class extends ForeignB.Handle { Read() { return new BigNumber(83); } },
  NativeInt: ForeignB.NativeInt,
  NativeZero: ForeignB.NativeZero
};

let ForeignFinal = {
  __default: class {},
  NativeCell: class extends ForeignB.NativeCell {
    constructor(rtd, seed, tag) {
      super(rtd, seed, tag);
      console.log("final cell " + tag);
    }
  },
  Actual: class extends ForeignB.Actual { Read() { return new BigNumber(89); } },
  Handle: class extends ForeignB.Handle { Read() { return new BigNumber(97); } },
  NativeInt: ForeignB.NativeInt,
  NativeZero: ForeignB.NativeZero
};

ForeignB.NativePartial = class {
  constructor(rtd) {
    if (arguments.length !== 1) { throw new Error("partial descriptor arity"); }
    this.number = new BigNumber(131);
    this.foreignDefault = rtd.Default;
  }
  Read() { return new BigNumber(141); }
};

ForeignRefined.NativePartial = class {
  constructor(rtd) {
    if (arguments.length !== 1) { throw new Error("partial descriptor arity"); }
    this.number = new BigNumber(137);
    this.foreignDefault = rtd.Default;
  }
  Read() { return new BigNumber(147); }
};

ForeignFinal.NativePartial = class {
  constructor(rtd) {
    if (arguments.length !== 1) { throw new Error("partial descriptor arity"); }
    this.number = new BigNumber(139);
    this.foreignDefault = rtd.Default;
  }
  Read() { return new BigNumber(149); }
};
