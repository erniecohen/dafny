// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-409-one-module.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-409-include.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-409-client.dfy" --library "%S/Inputs/github-issue-409-library.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-409-positive.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-409-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-inherited-termination-checks "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/409): a refining module that gives a body to a
// member without one can close a call cycle through a call it inherits.  The base module made no
// termination check at that call, because there the callee did not call back, and the refining module
// assumed the check, because the call site is inherited.  So Bad proved false, and compiled code could
// recurse forever.  An inherited termination check is now assumed only when its strongly connected
// component of the call graph is inherited unchanged: every member has a counterpart in the base with the
// same decreases clause, and every call between two members is a call between their counterparts.
// Otherwise the inherited call is checked, with the error it gets in one module
// (Inputs/github-issue-409-one-module.dfy), and the copy that holds it is verified for that check only.
// This holds when the base module is in an included file or a library too.  A refinement that adds no
// body to the component, or whose new calls and inherited calls decrease, is accepted
// (Inputs/github-issue-409-positive.dfy).  The hidden --legacy-inherited-termination-checks restores
// the old, unsound rule, under which this file verifies.

// The reproducer: lemmas
abstract module A {
  lemma P() ensures false decreases 2
  lemma R() ensures false decreases 1 { P(); }
}
module B refines A {
  lemma P() ensures false { R(); }
}

// Functions
abstract module FunctionsA {
  function F(n: nat): int decreases n
  function G(n: nat): int decreases n, 1 { F(n) }
}
module FunctionsB refines FunctionsA {
  function F(n: nat): int { G(n) + 1 }
  lemma Bad() ensures false { assert F(0) == G(0) + 1; assert G(0) == F(0); }
}

// Without decreases clauses
abstract module InferredA {
  lemma G(n: nat) requires n > 0 ensures false
  lemma F(n: nat) ensures false { G(n + 1); }
}
module InferredB refines InferredA {
  lemma G... { F(n - 1); }
}

// Compiled methods, which would recurse forever
abstract module MethodsA {
  method N(n: nat) decreases n, 1
  method M(n: nat) decreases n, 0 { N(n); }
}
module MethodsB refines MethodsA {
  method N(n: nat) { M(n); }
}

// Through a chain of refinements
abstract module ChainA {
  lemma P() ensures false decreases 2
  lemma R() ensures false decreases 1 { P(); }
}
abstract module ChainA1 refines ChainA { }
module ChainB refines ChainA1 {
  lemma P() ensures false { R(); }
}

// Dispatch through a trait of the base, overridden in a class that is new in the refinement
abstract module DispatchA {
  trait T { function M(n: nat): nat decreases n, 0 }
  function F(t: T, n: nat): nat decreases n, 1 { t.M(n + 1) }
}
module DispatchB refines DispatchA {
  class C extends T {
    constructor () {}
    function M(n: nat): nat decreases n, 0 { if n == 0 then 0 else F(this, n - 1) + 1 }
  }
  lemma Bad(c: C) ensures false { var t: T := c; assert c.M(1) == F(c, 0) + 1; assert F(t, 0) == t.M(1); }
}

// Two inherited calls in the cycle
abstract module TwoA {
  function H(n: nat): nat
  function G(n: nat): nat { F(n) }
  function F(n: nat): nat { H(n + 1) }
}
module TwoB refines TwoA {
  function H(n: nat): nat { if n == 0 then 0 else G(n - 1) + 1 }
  lemma Bad() ensures false { assert H(1) == G(0) + 1; assert G(0) == H(1); }
}

// A base that is not abstract
module GhostA {
  ghost function H(n: nat): nat
  ghost function G(n: nat): nat decreases n, 1 { H(n) }
}
module GhostB refines GhostA {
  ghost function H(n: nat): nat { G(n) + 1 }
  lemma Bad() ensures false { assert H(0) == G(0) + 1; }
}

// A by-method body
abstract module ByMethodA {
  function F(n: nat): nat decreases n, 1
  function G(n: nat): nat decreases n, 0 { 0 } by method { var x := F(n); return 0; }
}
module ByMethodB refines ByMethodA {
  function F(n: nat): nat { G(n) }
}

// An iterator
abstract module IteratorA {
  method N(n: nat) decreases n, 1
  iterator I(n: nat) decreases n, 0 { N(n); }
}
module IteratorB refines IteratorA {
  method N(n: nat) { var it := new I(n); var more := it.MoveNext(); }
}
