// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-410-positive.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-410-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-inherited-name-capture "%S/Inputs/github-issue-410-legacy.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/410): a refining module re-resolves the
// code it inherits in its own scope, so a declaration it adds could capture a name that the
// inherited code used.  The inherited proofs, made about the old meaning, then hold of the new
// one, and each module below proves false.  Every capture is now a resolution error, under both
// resolvers: a function, a newtype, a nested module, a class member that hides a module-level
// constant, a member of an added parent trait, a local variable of a statement skeleton, a
// datatype constructor or a constant that turns a pattern variable into a pattern, a capture
// through a chain of refinements, a constructor of the module bound to an abstract import, and
// a function of an opened abstract import.
// Refinements that keep every inherited name's meaning are still accepted
// (Inputs/github-issue-410-positive.dfy).  Under the hidden --legacy-inherited-name-capture,
// the first program proves false again (Inputs/github-issue-410-legacy.dfy).

// A function that captures a function of an opened import.
module L1 {
  function F(): int { 1 }
}
module A1 {
  import opened L1
  lemma P() ensures F() == 1 { }
}
module B1 refines A1 {
  function F(): int { 2 }
  lemma Bad() ensures false { P(); }
}

// A newtype that captures a newtype of an opened import.
module L2 {
  newtype T = x: int | 0 <= x < 10
}
module A2 {
  import opened L2
  lemma P(t: T) ensures t as int < 10 { }
}
module B2 refines A2 {
  newtype T = x: int | 0 <= x < 100
  lemma Bad() ensures false { var t: T := 50; P(t); }
}

// A nested module that captures a module of an opened import.
module Outer3 {
  module N { function F(): int { 1 } }
}
module A3 {
  import opened Outer3
  lemma P() ensures N.F() == 1 { }
}
module B3 refines A3 {
  module N { function F(): int { 2 } }
  lemma Bad() ensures false { P(); }
}

// A class member that hides a module-level constant.
module A4 {
  const x: int := 1
  class C {
    lemma P() ensures x == 1 { }
  }
}
module B4 refines A4 {
  class C ... {
    const x: int := 2
    lemma Bad() ensures false { P(); }
  }
}

// A member of a parent trait that the refinement adds.
module A5 {
  function f(): int { 0 }
  trait S { function f(): int { 1 } }
  type T {
    lemma L() ensures f() == 0 { }
  }
}
module B5 refines A5 {
  class T extends S { constructor () {} }
  method Bad() ensures false {
    var t := new T();
    t.L();
    assert t.f() == 1;
  }
}

// A local variable of a statement skeleton.
module A6 {
  const x: int := 1
  method M() { assert x == 1; }
}
module B6 refines A6 {
  method M ... ensures false { var x := 2; ...; }
}

// A datatype constructor that turns a pattern variable into a pattern.
abstract module A7 {
  type T
  function F(t: T): int { match t case foo => 1 case _ => 2 }
  lemma P(t: T) ensures F(t) == 1 { }
}
module B7 refines A7 {
  datatype T = foo | bar
  lemma Bad() ensures false { P(bar); }
}

// A capture through a chain of refinements.
module L8 {
  function F(): int { 1 }
}
module A8 {
  import opened L8
  lemma P() ensures F() == 1 { }
}
module A8a refines A8 { }
module B8 refines A8a {
  function F(): int { 2 }
  lemma Bad() ensures false { P(); }
}

// A constant that turns a pattern variable into a pattern.
module A9 {
  function F(n: int): int { match n case zero => 1 case _ => 2 }
  lemma P(n: int) ensures F(n) == 1 { }
}
module B9 refines A9 {
  const zero: int := 0
  lemma Bad() ensures false { P(5); }
}

// A constructor of the module that a refinement binds to an abstract import.
abstract module I10 { type T }
abstract module A10 {
  import X : I10
  function F(t: X.T): int { match t case foo => 1 case _ => 2 }
  lemma P(t: X.T) ensures F(t) == 1 { }
}
module I10a refines I10 { datatype T = foo | bar }
module B10 refines A10 {
  import X = I10a
  lemma Bad() ensures false { P(X.bar); }
}

// A function that captures a function of an opened abstract import.
abstract module I11 {
  function F(): int
  lemma L() ensures F() > 0
}
abstract module A11 {
  import opened X : I11
  lemma P() ensures F() > 0 { L(); }
}
abstract module B11 refines A11 {
  function F(): int { 0 }
  lemma Bad() ensures false { P(); }
}
