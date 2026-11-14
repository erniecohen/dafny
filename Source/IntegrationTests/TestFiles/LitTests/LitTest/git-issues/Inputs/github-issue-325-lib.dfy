// A library with user-written {:auto_generated} members of every kind, some shadowing imported
// names, and opaque members whose generated reveal lemmas the printer still leaves out.
module Base {
  function f(): int { 20 }
  const c: int := 20
  lemma L() ensures true {}
}

module Lib {
  import opened Base
  function {:auto_generated} f(): int { 10 }
  const {:auto_generated} c: int := 10
  lemma {:auto_generated} L() ensures f() == 10 {}
  method {:auto_generated} M() returns (r: int) ensures r == 10 { r := 10; }
  predicate {:auto_generated} P(x: int) { x == f() }
  opaque function g(): int { 7 }
  lemma UsesF() ensures f() == 10 && c == 10 && P(10) {}

  class K {
    function {:auto_generated} h(): int { 3 }
    opaque function o(): int { 4 }
  }
  datatype D = D {
    function {:auto_generated} h(): int { 5 }
  }
}
