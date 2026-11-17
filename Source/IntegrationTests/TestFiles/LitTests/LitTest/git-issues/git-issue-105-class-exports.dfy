// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): 'raw' for a class across
// modules.  Where a class with invariants is revealed, a client sees 'raw C' and 'raw C?'.  Where it is
// only provided, it is an abstract type whose invariants are hidden, so 'raw' does not yet apply to it
// (in the client, and in the export set's own signature), but its members may be used.

module A {
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

module RevealedClient {
  import A`Reveals

  method M(c: A.Cell, r: raw A.Cell, r': raw A.Cell?) returns (n: int, r2: raw A.Cell) {
    n := c.Inverse() + r.k;
    r2 := c;
  }
}

module ProvidedClient {
  import A`Provides

  method M(c: A.Cell) returns (n: int) {
    var e := A.Make();
    n := c.Inverse() + e.Inverse() + c.k;
  }
  method N(r: raw A.Cell) { }                          // error: not yet supported
}

module ExportsRaw {
  export provides Cell, Raw                            // error: 'Raw' mentions 'raw Cell'

  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }

  function Raw(c: Cell): raw Cell { c }
}
