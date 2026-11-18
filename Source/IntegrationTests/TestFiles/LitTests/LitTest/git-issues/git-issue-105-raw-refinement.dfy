// Member invariants (https://github.com/erniecohen/dafny/issues/105): 'raw' and refinement.  Where a
// datatype has no invariants, 'raw D' is D.  A refining module that adds invariants makes D a subset
// of 'raw D' there, and the raw members of each module follow its own invariants.
// RUN: %resolve --member-invariants --print-mode NoIncludes --rprint:- "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module Base {
  datatype D = D(x: int, y: int) {
    predicate Pos() { x > 0 }
    predicate Ordered() { x <= y }
    function Sum(): int { x + y }
  }

  method M(d: D, r: raw D) returns (e: D, f: raw D) {
    e := r;
    f := D(d.x, d.y);
  }
}

// Adds an invariant: Pos becomes a raw member here.
abstract module Middle refines Base {
  datatype D ... {
    invariant Pos()
  }
}

// Adds another: Ordered becomes a raw member too, and Pos stays one.
module Final refines Middle {
  datatype D ... {
    invariant Ordered()
  }

  method N(d: D) returns (r: raw D) {
    r := d.(x := d.y);
  }
}

// An abstract type refined by a datatype with invariants.
abstract module AbstractBase {
  type T
  function Make(): T
  method Use(t: T) returns (u: T) { u := t; }
}

module Concrete refines AbstractBase {
  datatype T = T(n: nat) {
    invariant n != 7
  }
  function Make(): T { T(0) }
  method Raw() returns (r: raw T) { r := T(7); }
}
