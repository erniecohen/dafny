// Preloaded module objects use the same public paths as ordinary emission.
let Foreign = { Tools: {
  Increment(x) { return x.plus(1); },
  Identity(x) { return x; },
  Echo(x) { return x; },
  ExternalEval() { return new BigNumber(17); }
} };
// Ordinary function qualifiers use an underscore; static methods use a dot.
Foreign.Tools_Identity = Foreign.Tools.Identity;
Foreign.Tools_Echo = Foreign.Tools.Echo;
Foreign.Tools_ExternalEval = Foreign.Tools.ExternalEval;

// The runtime is initialized before native methods use these integers.
globalThis.issue466RequiredInt = n => new BigNumber(n);
