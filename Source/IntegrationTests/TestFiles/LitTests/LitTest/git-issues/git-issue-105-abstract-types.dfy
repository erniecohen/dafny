// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of abstract types: section 4.3 of
// docs/dev/issue105-member-invariants-plan.md.  The name of an abstract type with invariants denotes
// the values of 'raw T' that satisfy them, so they are assumed in the abstract module, and each
// refinement that defines the type checks them wherever its own code makes a value of it.  The
// characteristics '(0)' and '(00)' of such a type are promises, which the abstract module may rely
// on, and each refinement that defines the type keeps with a witness.  Where an abstract type is only
// provided, its invariants are hidden, but its characteristics are not.

abstract module Spec {
  type T {
    function Size(): int
    invariant Size() > 0
  }
  lemma Positive(t: T) ensures t.Size() > 0 { }
  function Check(r: raw T): T requires r.Size() > 0 { r }
  function Bad(r: raw T): T { r }                      // error
}
module Impl refines Spec {
  datatype T = Mk(n: int) {
    function Size(): int { n }
  }
  method Make() returns (t: T) {
    t := Mk(0);                                        // error
  }
  method Good() returns (t: T) {
    t := Mk(3);
  }
}

abstract module Characteristics {
  type T(0) {
    function Size(): int
    invariant Size() > 0
  }
  type U(00) {
    function Size(): int
    invariant Size() > 0
  }
  type V {
    function Size(): int
    invariant Size() > 0
  }
  method Arrays() returns (s: int) ensures s > 0 {
    var a := new T[3];
    s := a[0].Size();
  }
  ghost function Some(): int {
    var u: U :| true; u.Size()
  }
  method NoInitializer() {
    var a := new V[3];                                 // error: V may be empty
  }
  method RawNoInitializer() {
    var a := new raw V[3];                             // error: so may 'raw V'
  }
}
module Witnessed refines Characteristics {
  datatype T = MkT(n: int) {
    function Size(): int { n }
    witness MkT(1)
  }
  datatype U = MkU(n: int) {
    function Size(): int { n }
    ghost witness MkU(1)
  }
  datatype V = MkV(n: int) {
    function Size(): int { n }
  }
}
module BadWitness refines Characteristics {
  datatype T = MkT(n: int) {
    function Size(): int { n }
    witness MkT(1)
  }
  datatype U = MkU(n: int) {
    function Size(): int { n }
    witness MkU(0)                                     // error
  }
  datatype V = MkV(n: int) {
    function Size(): int { n }
  }
}

// An abstract type refined by an abstract type that adds invariants, and then by datatypes
abstract module Layers0 {
  type T {
    function Size(): int
    invariant Size() > 0
  }
  type M {
    function Size(): int
  }
}
abstract module Layers1 refines Layers0 {
  type T ... {
    function Limit(): int
    invariant Size() < Limit()
  }
  type M ... {
    invariant Size() != 3
  }
  lemma Both(t: T, m: M) ensures 0 < t.Size() < t.Limit() && m.Size() != 3 { }
}
module Layers2 refines Layers1 {
  datatype T = MkT(n: int) {
    function Size(): int { 10 / n }                    // error: the invariants use Size, so it is raw
    function Limit(): int { 3 }
  }
  datatype M = MkM(n: int) {
    function Size(): int { n }
  }
  method Make() returns (t: T, m: M) {
    t := MkT(5);
    m := MkM(4);
  }
  method Bad() returns (t: T, t': T, m: M) {
    t := MkT(20);                                      // error: the invariant of Layers0
    t' := MkT(2);                                      // error: the invariant that Layers1 adds
    m := MkM(3);                                       // error: likewise
  }
}

// Abstract types that extend a trait with invariants
abstract module Traits0 {
  trait G {
    const k: int := 1
    invariant k != 0
  }
  type Opaque extends G {
    function Size(): int
    invariant Size() > 0
  }
  type Plain extends G
  lemma Pos(a: Opaque, p: Plain) ensures a.k != 0 && a.Size() > 0 && p.k != 0 { }
  function Up(a: Opaque): G { a }
}
module Traits1 refines Traits0 {
  datatype Opaque extends G = MkA(n: int) {
    function Size(): int { n }
  }
  datatype Plain extends G = MkP
  method Make() returns (a: Opaque, p: Plain) {
    p := MkP;
    a := MkA(0);                                       // error
  }
}

// Export sets
abstract module Exported {
  export Reveals reveals T provides T.Size
  export Provides provides T, T.Size
  type T(0) {
    function Size(): int
    invariant Size() > 0
  }
}
abstract module RevealedClient {
  import S = Exported`Reveals
  lemma L(t: S.T) ensures t.Size() > 0 { }
  method Arrays() returns (s: int) ensures s > 0 {
    var a := new S.T[2];
    s := a[0].Size();
  }
}
abstract module ProvidedClient {
  import S = Exported`Provides
  lemma L(t: S.T) ensures t.Size() > 0 { }             // error: the invariant is hidden
  method Arrays() returns (s: int) {
    var a := new S.T[2];                               // but the characteristics are not
    s := a[0].Size();
  }
}
