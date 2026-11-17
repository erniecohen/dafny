// Divergence kernels and rule violations for milestone 3 of https://github.com/erniecohen/dafny/issues/266.  Each
// fails at the obligation that rules it out, and at no other.

// K3: a value installed in a mutable field, which a later state closes into a loop.  A check of the lambda in the
// heap where it is formed alone would see the old, harmless value; the body is checked in every later heap, so
// both its bound and its precondition fail.
module K3 {
  class Cell {
    var cb: bounded<nat, () ~> int>
    constructor () { cb := bounded.certify(0, () => 0); }
  }
  method Main() decreases {:global} 5 {
    var c := new Cell();
    var g := bounded.certify(1, () reads * => c.cb()); // error: bound of c.cb; error: precondition of c.cb
    c.cb := g;
    var result := g();
  }
}

// K3, isolated: the lambda calls "high" only after a later mutation of "box", so the failure is the future bound
// 3 <= 0 at formation.  The trace would be high(3), f(2), g(0), high(3), forever.
module K3Ghost {
  class Box {
    var k: bool
    constructor () { k := false; }
  }
  trait {:termination false} {:termination_closed} T extends object {
    ghost predicate ready() reads * decreases {:global} 1
    ghost function f(): int requires ready() reads * decreases {:global} 2
  }
  ghost function high(t: T): int reads * decreases {:global} 3 {
    if t.ready() then t.f() else 0
  }
  type Zero = h: bounded<nat, T ~> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: T) => 0)
  class C extends T {
    ghost const cb: Zero
    constructor (ghost h: Zero) { cb := h; }
    ghost predicate ready() reads * { cb.requires(this) }
    ghost function f(): int requires ready() reads * { var ignored := cb(this); 0 }
  }
  method Main() {
    var box := new Box();
    ghost var g := bounded.certify(0, (t: T) reads * => if box.k then high(t) else 0); // error: 3 is not below 0
    var c := new C(g);
    box.k := true;
    ghost var result := high(c);
  }
}

// K3 through a collection: the value is kept in a mutable sequence field.
module K3Seq {
  class Box {
    var k: bool
    constructor () { k := false; }
  }
  trait {:termination false} {:termination_closed} T extends object {
    ghost predicate ready() reads * decreases {:global} 1
    ghost function f(): int requires ready() reads * decreases {:global} 2
  }
  ghost function high(t: T): int reads * decreases {:global} 3 {
    if t.ready() then t.f() else 0
  }
  type Zero = h: bounded<nat, T ~> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: T) => 0)
  class C extends T {
    ghost var fs: seq<Zero>
    constructor () { fs := []; }
    ghost predicate ready() reads * { |fs| > 0 && fs[0].requires(this) }
    ghost function f(): int requires ready() reads * { fs[0](this) }
  }
  method Main() {
    var box := new Box();
    var c := new C();
    ghost var g := bounded.certify(0, (t: T) reads * => if box.k then high(t) else 0); // error: 3 is not below 0
    c.fs := [g];
    box.k := true;
    ghost var result := high(c);
  }
}

// K6 through the heap: a bound read from a field is a snapshot.
module K6Heap {
  class Cell {
    var k: nat
    constructor () { k := 2; }
  }
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int decreases {:global} 1
  }
  function High(t: T): int decreases {:global} 2 { t.f() }
  method M(cell: Cell) modifies cell {
    cell.k := 2;
    var h := bounded.certify(cell.k, High);
    cell.k := 0;
    assert h.bound == 0; // error: the bound is still 2
  }
}

// Applying a value in a state before it was formed: its function need not be allocated there.
module Old {
  class Cell {
    var n: int
    constructor () { n := 0; }
  }
  method M(c: Cell) modifies c decreases {:global} 1 {
    c.n := 1;
    var g := bounded.certify(0, () reads c => c.n);
    ghost var r := old(g()); // error: g is not allocated in the old state
  }
}

// Callback parameters: their bounds are evaluated on entry, and a lambda that applies or passes one runs under its
// own bound, evaluated where it is formed.
module Callback {
  class Cell {
    var k: nat
    constructor () { k := 0; }
  }
  method TooLow(f: int -> int) returns (c: bounded<nat, () -> int>)
    decreases {:global} 9
    decreases {:callback f} 2
  {
    c := bounded.certify(2, () => f(1)); // error: 2 is not below 2
  }
  method AfterMutation(cell: Cell, f: int -> int) returns (c: bounded<nat, () -> int>)
    modifies cell
    decreases {:global} 9
    decreases {:callback f} cell.k
  {
    cell.k := 0;
    c := bounded.certify(1, () => f(1)); // error: the bound of f on entry need not be below 1
  }
  method StoreAfterMutation(cell: Cell, f: int -> int) returns (c: bounded<nat, int -> int>)
    modifies cell
    decreases {:global} 9
    decreases {:callback f} cell.k
  {
    cell.k := 5;
    c := bounded.certify(cell.k, f); // error: the bound of f on entry need not be at most 5
  }
  function Pass(g: int -> int, x: int): int
    decreases {:global} 4
    decreases {:callback g} 3
  {
    g(x)
  }
  method ForwardHigh(f: int -> int) returns (c: bounded<nat, int -> int>)
    decreases {:global} 9
    decreases {:callback f} 4
  {
    c := bounded.certify(5, (x: int) => Pass(f, x)); // error: 4 exceeds the bound 3 of Pass's parameter
  }
}
