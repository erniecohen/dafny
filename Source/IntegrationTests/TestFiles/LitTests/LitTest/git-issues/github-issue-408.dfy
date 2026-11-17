// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-408-positive.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-408-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-unchecked-import-binding "%S/Inputs/github-issue-408-legacy.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/408): a module that refines B could bind
// B's abstract import "X : A" to a module that does not refine A, and so prove false from A's
// contracts, which B's inherited proofs assume.  Each binding below is now a resolution error,
// under both resolvers: an alias import, a literal nested module, a nested module that refines an
// unrelated module, another abstract import of an unrelated module, and an alias of such an
// abstract import.  Bindings to refinements of A are still accepted
// (Inputs/github-issue-408-positive.dfy).  Under the hidden --legacy-unchecked-import-binding,
// the issue's program proves false again (Inputs/github-issue-408-legacy.dfy).

abstract module A {
  function F(): int
  lemma L() ensures F() > 0
}

module Other {
  function F(): int { 0 }
  lemma L() { }
}

abstract module B {
  import X : A
  lemma P() ensures X.F() > 0 { X.L(); }
}

// The issue's program
module C refines B {
  import X = Other
  lemma Bad() ensures false { P(); }
}

module CLiteral refines B {
  module X {
    function F(): int { 0 }
    lemma L() { }
  }
  lemma Bad() ensures false { P(); }
}

module CNested refines B {
  module X refines Other { }
  lemma Bad() ensures false { P(); }
}

abstract module CAbstract refines B {
  import X : Other
  lemma Bad() ensures false { P(); }
}

abstract module DOther { import Y : Other }

abstract module CAliasOfAbstract refines B {
  import X = DOther.Y
  lemma Bad() ensures false { P(); }
}

// The issue's run-time variant: compiled, Div divided by zero.
abstract module NonZero {
  function F(): int ensures F() != 0
}

module Zero {
  function F(): int { 0 }
}

abstract module UsesNonZero {
  import X : NonZero
  method Div() returns (r: int) { r := 10 / X.F(); }
}

module DividesByZero refines UsesNonZero {
  import X = Zero
  method Main() { var r := Div(); print r, "\n"; }
}
