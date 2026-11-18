// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --synonym-refines-abstract-type --member-invariants "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --synonym-refines-abstract-type --member-invariants "%S/Inputs/git-issue-105-abstract-synonym-raw.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of an abstract type that a type
// synonym refines, under --synonym-refines-abstract-type: the abstract type's members, its invariants
// among them, become members of the type that the synonym names, so all of them are checked where the
// refining module makes a value of it.  Inherited code that says 'raw T' is rejected, since 'T' is a
// synonym there (Inputs/git-issue-105-abstract-synonym-raw.dfy).

abstract module Spec {
  type T {
    function Size(): int
    invariant Size() > 0
  }
  lemma Pos(t: T) ensures t.Size() > 0 { }
}
module Impl refines Spec {
  type T = D
  datatype D = MkD(n: int) {
    function Size(): int { n }
    invariant n < 10
  }
  lemma Both(d: D) ensures 0 < d.Size() && d.n < 10 { }
  method Make() returns (t: T, d: D) {
    t := MkD(0);                                       // error: the invariant of Spec
    d := MkD(20);                                      // error: the invariant of D
  }
}
