// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --member-invariants --default-function-opacity autoRevealDependencies "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) under --default-function-opacity
// autoRevealDependencies.  Where a callable depends on a type with invariants, the functions that the invariants
// call are revealed, as those that a subset type's constraint calls are where a callable depends on the subset type.
// Each method or witness below that verifies does so only because of that.  The invariants themselves are never
// opaque (git-issue-105-opacity).

module Uses {
  predicate Even(x: int) { x % 2 == 0 }
  predicate Small(x: int) { x < 100 }

  datatype D = D(n: int) {
    invariant Even(n)
    witness D(4) // a witness member
  }
  method MakeD() returns (d: D) {
    d := D(4);
  }
  method UseD(d: D) {
    assert d.n % 2 == 0;
  }
  method MakeOdd() returns (d: D) {
    d := D(3); // error: revealed, the body is false
  }
  method {:autoRevealDependencies false} NotRevealed() returns (d: D) {
    d := D(4); // error: nothing is revealed here
  }

  class C {
    const k: int
    invariant Even(k)
    constructor () {
      k := 2;
    }
  }
  method UseC(c: C) {
    assert c.k % 2 == 0;
  }
  method UsePossiblyNull(c: C?)
    requires c != null
    ensures c.k % 2 == 0
  {
  }

  trait T extends object {
    const t: int
    invariant Small(t)
  }
  class K extends T {
    constructor () {
      t := 2;
    }
  }
  method UseK(x: K) {
    assert x.t < 100;
  }
}

// A newtype's header constraint is a conjunct of the invariants, and its header witness is sugar for a witness
// member.
module Newtypes {
  predicate Even(x: int) { x % 2 == 0 }
  predicate Small(x: int) { x < 100 }
  newtype Header = x: int | Even(x) witness 4 {
    invariant Small(this as int)
  }
  newtype Member = x: int | Even(x) {
    invariant Small(this as int)
    witness 4
  }
  method MakeMember() returns (m: Member) {
    m := 6;
  }
  method UseMember(m: Member) {
    assert (m as int) % 2 == 0 && (m as int) < 100;
  }
}

// An invariant that calls a function of another module.
module Library {
  predicate Even(x: int) { x % 2 == 0 }
  opaque predicate Hidden(x: int) { x % 2 == 0 }
}
module Imports {
  import L = Library
  datatype D = D(n: int) {
    invariant L.Even(n)
  }
  method MakeD() returns (d: D) {
    d := D(4);
  }
  datatype H = H(n: int) {
    invariant L.Hidden(n)
  }
  method MakeH() returns (h: H) {
    h := H(4); // error: an opaque predicate is revealed only explicitly
  }
  method MakeHRevealed() returns (h: H) {
    reveal L.Hidden();
    h := H(4);
  }
}

// A callable in another module that depends on the type reveals nothing, as for a subset type.
module Client {
  import U = Uses
  method MakeD() returns (d: U.D) {
    d := U.D(4); // error: nothing is revealed here
  }
}
