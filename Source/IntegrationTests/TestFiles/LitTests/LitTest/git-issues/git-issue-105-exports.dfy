// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) across export sets.  An invariant
// is part of the definition of its type, as a datatype's constructors are: a client to which the type
// is revealed sees the invariant and assumes it of every value, and a client to which the type is only
// provided sees an abstract type, and none of it.  A witness serves every client.

module Intervals {
  export Reveals reveals Interval provides Interval.Width
  export Provides provides Interval, Interval.Width

  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
    function Width(): int { hi - lo }
  }
}

module IntervalRevealed {
  import Intervals`Reveals

  method M(i: Intervals.Interval) returns (w: nat) {
    assert i.lo <= i.hi;
    var k := Intervals.Interval(1, 2);
    var j: Intervals.Interval := k;
    w := i.hi - i.lo;
  }
}

module IntervalProvided {
  import Intervals`Provides

  method M(i: Intervals.Interval) returns (w: int) {
    w := i.Width();
  }
}

module Witnesses {
  export reveals D, G
  export Provides provides D, G

  datatype D = D(x: int) {
    invariant x > 0
    witness D(1)
  }
  datatype G = G(y: int) {
    invariant y > 1
    ghost witness G(2)
  }
}

module WitnessRevealed {
  import Witnesses

  method M() returns (b: bool) {
    var arr := new Witnesses.D[3];
    assert arr[0].x > 0;
    var g: Witnesses.G;
    b := arr[1].x > 0;
  }
}

module WitnessProvided {
  import Witnesses`Provides

  method M() {
    var arr := new Witnesses.D[2];
    var d := arr[0];
  }
}

module Cells {
  export Provides provides Cell, Cell.Inverse, Cell.k, Make
  export Reveals reveals Cell provides Cell.Inverse, Cell.k, Make

  class Cell {
    const k: int
    invariant k > 0
    constructor (k: int) requires k > 0 { this.k := k; }
    function Inverse(): int { 100 / k }
  }

  method Make() returns (c: Cell) { c := new Cell(1); }
}

module CellRevealed {
  import Cells`Reveals

  method M(c: Cells.Cell, r: raw Cells.Cell) returns (n: int) {
    var e := new Cells.Cell(2);
    n := c.Inverse() + e.Inverse();
    var r2: raw Cells.Cell := c;
    assert c.k > 0;
    if r.k > 0 {
      var c2: Cells.Cell := r;
    }
    var o: object := c;
  }
}

module CellProvided {
  import Cells`Provides

  method M(c: Cells.Cell, d: Cells.Cell) returns (n: int) {
    var e := Cells.Make();
    n := c.Inverse() + e.Inverse() + d.Inverse();
    var s := {c, e};
  }
  method N(c: Cells.Cell) {
    assert c.k > 0;                                    // error: the invariant is not visible here
  }
}
