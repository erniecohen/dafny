// Refinements of github-issue-409.dfy that are accepted.  Each refining module has a control, an
// assert false, that fails: the refinement does not make false provable.

// A refinement that adds no body to a recursive component: the inherited calls stay assumed, and
// the copies are not verified again.
abstract module Unchanged {
  function Even(n: nat): bool { if n == 0 then true else Odd(n - 1) }
  function Odd(n: nat): bool { if n == 0 then false else Even(n - 1) }
  lemma EvenDouble(n: nat) ensures Even(2 * n) { if n > 0 { EvenDouble(n - 1); } }
  function H(n: nat): bool
  method Count(n: nat) returns (r: nat) { r := 0; if n > 0 { r := Count(n - 1); r := r + 1; } }
}
module UnchangedRefined refines Unchanged {
  function H(n: nat): bool { Even(n) || Odd(n) }
  lemma Control() { assert false; }
}

// A refinement that closes a cycle in which every call, new or inherited, decreases
abstract module Decreasing {
  lemma P(n: nat) decreases n, 0
  lemma R(n: nat) decreases n, 1 { P(n); }
  function F(n: nat): nat decreases n, 0
  function G(n: nat): nat decreases n, 1 { F(n) }
  method N(n: nat) decreases n, 0
  method M(n: nat) decreases n, 1 { N(n); }
}
module DecreasingRefined refines Decreasing {
  lemma P(n: nat) { if n > 0 { R(n - 1); } }
  function F(n: nat): nat { if n == 0 then 0 else G(n - 1) + 1 }
  method N(n: nat) { if n > 0 { M(n - 1); } }
  lemma Control() { assert false; }
}

// Dispatch through a trait of the base, where the new override's call decreases
abstract module DispatchDecreasing {
  trait T { function M(n: nat): nat decreases n, 0 }
  function F(t: T, n: nat): nat decreases n, 1 { t.M(n) }
}
module DispatchDecreasingRefined refines DispatchDecreasing {
  class C extends T {
    constructor () {}
    function M(n: nat): nat decreases n, 0 { if n == 0 then 0 else F(this, n - 1) + 1 }
  }
  lemma Control() { assert false; }
}
