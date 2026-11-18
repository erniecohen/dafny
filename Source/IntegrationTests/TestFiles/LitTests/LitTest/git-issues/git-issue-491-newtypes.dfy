// RUN: %exits-with 2 %verify --type-system-refresh --general-traits=datatype --general-newtypes "%s" > "%t"
// RUN: %exits-with 0 %verify --legacy-unchecked-type-variance "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Newtype counterpart of https://github.com/erniecohen/dafny/issues/491.

module Newtype {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  newtype N<+X(!new)> = n: int | n == 0 ==> (forall y: X :: F(y)) witness *
  lemma Contradiction(t: N<int>) requires (t as int) == 0 ensures false {
    assert F(0);
    assert false;
  }
  lemma FalseProof() {
    var zero: int := 0;
    var s := zero as N<Empty>;
    var t: N<int> := s;
    Contradiction(t);
    assert false;
  }
}
