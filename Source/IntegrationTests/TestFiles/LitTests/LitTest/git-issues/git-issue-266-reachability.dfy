// RUN: %verify --modular-termination --solver-path %review-z3 "%s" > "%t"
// RUN: %verify --modular-termination --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --modular-termination --solver-path %review-z3 "%S/Inputs/git-issue-266-reachability-false.dfy" >> "%t"
// RUN: %exits-with 4 %verify --modular-termination --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-266-reachability-false.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --modular-termination "%S/Inputs/git-issue-266-reachability-raw.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --modular-termination --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-266-reachability-raw.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Bounded callables retain checked formation evidence through the stored-callback search
// (https://github.com/erniecohen/dafny/issues/266). Cover both the argument that reaches the owner
// through a datatype or another object's constant, and the bounded value stored in those wrappers.
// Every application spends a strictly smaller bound. The raw-arrow counterparts must still be
// rejected; the companion false controls must fail after each positive construction and application.

module DatatypeArgument {
  datatype Box = Box(c: C)
  class C {
    const cb: bounded<nat, Box -> int>
    constructor (h: bounded<nat, Box -> int>)
      ensures cb == h
    { cb := h; }
    function Use(): int
      requires cb.bound < 1
      decreases {:global} 1
    { cb(Box(this)) }
  }
  method Positive() {
    var h := bounded.certify(0, (b: Box) => 42);
    var c := new C(h);
    assert c.Use() == 42;
  }
}
module ConstantArgument {
  class D {
    const c: C
    constructor (c: C) ensures this.c == c { this.c := c; }
  }
  class C {
    const cb: bounded<nat, D -> int>
    constructor (h: bounded<nat, D -> int>)
      ensures cb == h
    { cb := h; }
    function Use(d: D): int
      requires cb.bound < 1
      decreases {:global} 1
    { cb(d) }
  }
  method Positive() {
    var h := bounded.certify(0, (d: D) => 42);
    var c := new C(h);
    var d := new D(c);
    assert c.Use(d) == 42;
  }
}
module DatatypeStorage {
  datatype Wrap = Wrap(cb: bounded<nat, C -> int>)
  class C {
    const wrapped: Wrap
    constructor (w: Wrap)
      ensures wrapped == w
    { wrapped := w; }
    function Use(): int
      requires wrapped.cb.bound < 1
      decreases {:global} 1
    { wrapped.cb(this) }
  }
  method Positive() {
    var h := bounded.certify(0, (c: C) => 42);
    var c := new C(Wrap(h));
    assert c.Use() == 42;
  }
}
module ConstantStorage {
  class D {
    const cb: bounded<nat, C -> int>
    constructor (h: bounded<nat, C -> int>)
      ensures cb == h
    { cb := h; }
  }
  class C {
    const holder: D
    constructor (d: D)
      ensures holder == d
    { holder := d; }
    function Use(): int
      requires holder.cb.bound < 1
      decreases {:global} 1
    { holder.cb(this) }
  }
  method Positive() {
    var h := bounded.certify(0, (c: C) => 42);
    var d := new D(h);
    var c := new C(d);
    assert c.Use() == 42;
  }
}
