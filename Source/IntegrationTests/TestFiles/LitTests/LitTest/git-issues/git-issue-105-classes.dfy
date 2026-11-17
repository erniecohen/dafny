// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of classes: the examples of
// sections 6.1 and 6.3 of docs/dev/issue105-member-invariants-plan.md, the vacuity controls V2 and V6
// and the 'Bad' test of its section 6.4, and the rules of section 3.4.  An invariant is checked once,
// at the end of the constructor's first phase ('new;', explicit or implicit), and assumed of every
// value of the class type, including objects that are not yet allocated.  A raw reference, the
// constructor's 'this' before 'new;', reaches 'C', 'object', a collection or an arrow only where the
// invariant is proved.

module Examples {
  class Buffer {
    const data: array<int>
    const cap: nat
    const half: nat := cap / 2                         // a const with an initializer may be read too
    var len: nat                                       // mutable: not the invariant's business

    invariant data.Length == cap && 0 < cap && half < cap

    constructor (n: nat)
      requires 0 < n
    {
      data := new int[n];
      cap := n;
      len := 0;
      new;                                             // invariant checked here
      Reset();                                         // 'this' is a Buffer
    }
    method Reset() modifies this { len := 0; }
    method Get(i: nat) returns (x: int)
      requires i < cap
    { x := data[i]; }                                  // i < data.Length, from the invariant
  }

  class Pos {
    const k: int
    invariant k > 0
    constructor (k: int) { this.k := k; }              // error: at the implicit 'new;'
  }
}

module Controls {
  class Pos {
    const k: int
    invariant k > 0
    constructor (k: int) requires k > 0 ensures this.k == k { this.k := k; }
  }

  // V2: an object with an invariant can be made, so the invariant gives no contradiction
  method V2() {
    var p := new Pos(1);
    assert false;                                      // error
  }

  // V6: an unsatisfiable invariant is reported at 'new;', and assuming it of a parameter is
  // consistent only because no such object can be made
  class F {
    const x: int
    invariant false
    constructor () { new; }                            // error
  }
  method V6(f: F) { assert false; }

  // Bad: 'new;' assumes only the raw type before the check; if it assumed the finished type, this
  // would verify
  class Bad {
    const k: int
    invariant k > 0
    constructor () { k := 0; new; }                    // error
  }
}

module Rules {
  class Cell {
    const k: int
    invariant k > 0
    constructor (k: int) requires k > 0 ensures this.k == k { this.k := k; }
    function Inverse(): int { 100 / k }
  }

  class Holder {
    var c: Cell?
    const d: Cell
    constructor (d: Cell) { this.d := d; c := null; }
  }

  // assumed of parameters, fields, collection elements and possibly-null references
  method Assumed(c: Cell, h: Holder, s: set<Cell>, q: seq<Cell?>) returns (r: Cell)
    requires h.c != null && |q| > 0
    ensures r.k > 0
  {
    assert c.k > 0 && h.d.k > 0 && h.c.k > 0;
    assert forall x <- s :: x.k > 0;
    assert q[0] != null ==> q[0].k > 0;
    r := c;
  }

  // assumed of objects that are not allocated, too
  lemma Unallocated()
    ensures forall x: Cell :: x.k > 0
  {
  }

  function UsedInFunction(c: Cell): int { 100 / c.k }

  method DynamicTest(o: object) returns (n: int) {
    n := 0;
    if o is Cell {
      var c := o as Cell;
      assert c.k > 0;
      n := c.k;
    }
  }

  // after 'new;', 'this' is a TwoPhase
  class TwoPhase {
    const k: int
    var v: int
    invariant k > 0
    constructor () {
      k := 1;
      new;
      v := this.Inverse();
      Use(this);
    }
    function Inverse(): int { 100 / k }
  }
  method Use(t: TwoPhase) { }

  // a constructor without an explicit 'new;' checks the invariant at its end
  class Implicit {
    const k: int
    invariant k > 0
    constructor (c: Implicit) { k := c.k; }
  }

  method RawToCell(r: raw Cell) {
    var c: Cell := r;                                  // error
  }
  method RawToCellQ(r: raw Cell?) {
    var c: Cell? := r;                                 // error
  }
  method RawQToCell(r: raw Cell?) requires r != null ==> r.k > 0 {
    var c: Cell := r;                                  // error: r might be null
  }
  method RawToCellChecked(r: raw Cell, r': raw Cell?)
    requires r.k > 0 && (r' != null ==> r'.k > 0)
  {
    var c: Cell := r;
    var c': Cell? := r';
    var d := r as Cell;
  }
  method CellToRaw(c: Cell, c': Cell?) returns (r: raw Cell, r': raw Cell?) {
    r, r' := c, c';
  }

  // frames carry no obligation
  method RawFrame(r: raw Cell) modifies r { }
  function RawReads(r: raw Cell): int reads r { r.k }

  // type tests of raw references are ghost, and check the invariant
  method TestRaw(r: raw Cell) {
    assert r is Cell;                                  // error
  }
  method TestRawQ(r: raw Cell?) {
    assert r is Cell?;                                 // error
  }
  method TestRawNonNull(r: raw Cell?) {
    assert r != null ==> r is Cell;                    // error
  }
  method TestRawChecked(r: raw Cell) requires r.k > 0 {
    assert r is Cell;
  }
  // a raw Cell is an 'object' only if it is a Cell
  method TestRawObject(r: raw Cell) {
    assert r is object;                                // error
  }
  method TestRawObjectChecked(r: raw Cell) {
    if r is object {
      assert r.k > 0;
    }
  }
}

// A raw reference reaches 'object', a collection, a type parameter's instance or an arrow only as a
// finished one
module Escape {
  class Cell {
    const k: int
    invariant k > 0
    constructor (k: int) requires k > 0 { this.k := k; }
  }
  datatype Option<+X> = None | Some(x: X)
  method Id<X>(x: X) returns (y: X) { y := x; }

  method ToObject(r: raw Cell) {
    var o: object := r;                                // error
  }
  method ToObjectQ(r: raw Cell?) {
    var o: object? := r;                               // error
  }
  method ToSet(r: raw Cell) {
    var s: set<object> := {r};                         // error
  }
  method ToSeq(r: raw Cell) {
    var q: seq<object?> := [r];                        // error
  }
  method ToMap(r: raw Cell) {
    var m: map<int, object> := map[0 := r];            // error
  }
  method ToDatatype(r: raw Cell) {
    var t: Option<object> := Some(r);                  // error
  }
  method ThroughTypeParameter(r: raw Cell) {
    var x := Id(r);                                    // fine: x is a raw Cell
    var o := Id<object>(r);                            // error
  }
  method ThroughArrow(r: raw Cell) {
    var f: () -> object := () => r;                    // error
  }
  method Checked(r: raw Cell)
    requires r.k > 0
  {
    var o: object := r;
    var s: set<object> := {r};
    var t: Option<object> := Some(r);
    var u := Id<object>(r);
  }
}
