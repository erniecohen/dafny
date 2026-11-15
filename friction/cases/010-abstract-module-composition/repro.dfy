// A generic theorem stated once over an abstract operation, instantiated twice.
abstract module Op {
  function f(a: real, b: real): real
  lemma monotone(a: real, b: real, c: real, d: real)
    requires a <= c && b <= d
    ensures f(a,b) <= f(c,d)
  lemma Generic(a: real, b: real) requires a <= b ensures f(a,a) <= f(b,b) { monotone(a,a,b,b); }
}
module Sum refines Op {
  function f(a: real, b: real): real { a+b }
  lemma monotone(a: real, b: real, c: real, d: real) {}
}
module Max refines Op {
  function f(a: real, b: real): real { if a < b then b else a }
  lemma monotone(a: real, b: real, c: real, d: real) {}
}
// Composition needs both operations at once: an abstract module with two parameters.
abstract module Compose {
  import F : Op
  import G : Op
  lemma Composed(a: real, b: real) requires a <= b ensures F.f(G.f(a,a),a) <= F.f(G.f(b,b),b) {
    G.monotone(a,a,b,b); F.monotone(G.f(a,a),a,G.f(b,b),b);
  }
}
module SumOfMax refines Compose {
  import F = Sum
  import G = Max
}
