// RUN: %resolve --print "%t.dfy" "%s" > "%t"
// RUN: %resolve "%t.dfy" >> "%t"
// RUN: %resolve --print-mode NoGhostOrIncludes --print "%t-noghost.dfy" "%s" >> "%t"
// RUN: %resolve "%t-noghost.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// --print used to write a refining datatype that declares no constructors as `datatype D = { ... }`,
// which does not parse (https://github.com/erniecohen/dafny/issues/415). It now writes it as it was
// written, `datatype D ... { ... }`, so the printed program reads back, and the refinement still
// inherits the constructors: Get, Head and Twice below use them.

abstract module A {
  datatype D = D(x: int)
  datatype E = E0 | E1(y: int)
  codatatype Stream = Cons(head: int, tail: Stream)
}

module B refines A {
  datatype D ... {
    function Get(): int { x }
  }
  datatype E ...
  codatatype Stream ... {
    function Head(): int { head }
  }

  method Twice(d: D, s: Stream) returns (r: int) {
    r := d.Get() + D(1).x + s.Head();
  }
}
