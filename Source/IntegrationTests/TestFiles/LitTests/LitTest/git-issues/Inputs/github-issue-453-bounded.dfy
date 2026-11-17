// Bounded values (https://github.com/erniecohen/dafny/issues/266) stored in the fields of an object, under
// --modular-termination (https://github.com/erniecohen/dafny/issues/453).  Applying a bounded value needs its
// bound below that of the code applying it, and its function runs under its bound, so a bounded function whose
// parameter is the object's trait cannot close a knot, and it is stored.  The raw functions in the result type of
// a bounded function are applied without a bound, so the search goes on there: a function on Box, which holds
// the object, is rejected.

module Accepted {
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int
      decreases {:global} 1
  }
  class C extends T {
    const cb: bounded<nat, T -> int>
    constructor (h: bounded<nat, T -> int>) { cb := h; }
    function f(): int { 0 }
  }
}

module Rejected {
  datatype Box = Box(c: C)
  datatype Wrap = Wrap(g: Box -> int)
  class C {
    const cb: bounded<nat, () -> Wrap>
    constructor (h: bounded<nat, () -> Wrap>) { cb := h; }  // error: Wrap holds a raw function on Box
  }
}
