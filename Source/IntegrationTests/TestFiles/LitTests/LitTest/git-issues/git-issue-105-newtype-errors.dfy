// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes: the resolution
// errors after the witness rules (git-issue-105-newtype-witness-errors.dfy), from section 3.5 of
// docs/dev/issue105-member-invariants-plan.md.  A header witness is a value of the newtype, as a
// witness member is.  "raw N" is ghost: a compiled value of N satisfies its invariants,
// so that its native representation can be read from them.  So no compiled expression has a type that
// mentions "raw N", and a member that an invariant uses, whose receiver may be raw, is ghost.  Each
// error has a control beside it that resolves.

module Witnesses {
  // A header witness is a value of the newtype, as a witness member is, where in the old syntax without
  // invariants it is a value of the base type.
  const c: int := 3
  newtype IntWitness = x: int | x < 10 witness c { // error: the witness has type IntWitness
    invariant this as int > 0
  }
  newtype NoInvariant = x: int | x < 10 witness c
  newtype Literal = x: int | x < 10 witness 3 {
    invariant this as int > 0
  }
}

module GhostRaw {
  newtype Even = int {
    invariant IsEven()
    witness 0
    predicate IsEven() { (this as int) % 2 == 0 } // error: must be ghost
    ghost predicate IsEven2() { (this as int) % 2 == 0 }
    function Half(): int { (this as int) / 2 }
    function Next(): Even { this + 2 }
  }

  newtype Small = x: int | 0 <= x < 100 {
    invariant this as int != 7
    witness 1
  }

  method Compiled(e: Even, r: raw Even, s: seq<raw Even>) returns (b: bool, t: raw Even) {
    var x := e.Half();
    var y := r; // error: y is not ghost
    ghost var y' := r;
    print r, "\n"; // error
    var z := 3 as raw Even; // error
    print z, "\n"; // error
    ghost var z' := 3 as raw Even;
    b := r is Even; // error: not run-time checkable
    print |s|, "\n"; // error
    t := e; // a value of Even
    var n := e + 2;
    var m: Small := 5;
    var k := m as int;
    ghost var g := r + 1;
  }

  ghost method Ghost(r: raw Even, s: seq<raw Even>) {
    var b := r is Even;
    var z := 3 as raw Even;
    var n := |s|;
  }
}
