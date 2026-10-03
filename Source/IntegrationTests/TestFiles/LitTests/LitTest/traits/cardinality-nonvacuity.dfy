// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait V {}
datatype D extends V = D(n: int)
predicate positive(v: V) {
  if v is D then 0 < (v as D).n else false
}
method NonVacuity() {
  var v: V := D(3);
  assert v is D;
  assert positive(v);
  assert false; // Required verification failure, not a resolution error.
}

module Retained {
  trait V<T> {}
  datatype D<T> extends V<T> = D(x: T)
  method NonVacuity() {
    var v: V<int> := D(3);
    assert v is D<int>;
    assert (v as D<int>).x == 3;
    assert false;
  }
}
module TraitChain {
  trait Root {}
  trait Child extends Root {}
  datatype D extends Child = D(n: int)
  method NonVacuity() {
    var v: Root := D(3);
    assert v is D;
    assert v is Child;
    assert (v as D).n == 3;
    assert false;
  }
}

module ProvidedFactory {
  export provides V, V.Tag, D, Make
  trait V { function Tag(): (r: int) ensures r == 3 }
  datatype D extends V = D(n: int) {
    function Tag(): (r: int) ensures r == 3 { 3 }
  }
  method Make() returns (v: V)
    ensures v.Tag() == 3
  { v := D(3); }
}
module ProvidedClient {
  import S = ProvidedFactory
  method Witness() {
    var v := S.Make();
    assert v.Tag() == 3;
    assert false;
  }
}
