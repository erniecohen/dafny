// RUN: %baredafny resolve --allow-deprecation --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny resolve --allow-deprecation --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny verify --allow-deprecation --solver-path "%z3" --type-system-refresh:false --boogie "/proc:__NoProcedureMatches__" --bprint "%t.bpl" "%s" > "%t"
// RUN: %OutputCheck --file-to-check "%t_LabeledRefinement.bpl" "%s"
// RUN: %baredafny verify --allow-deprecation --solver-path "%z3" --type-system-refresh:true --boogie "/proc:__NoProcedureMatches__" --bprint "%t.bpl" "%s" > "%t"
// RUN: %OutputCheck --file-to-check "%t_LabeledRefinement.bpl" "%s"

// The inherited trigger must keep the explicit old-state label after re-resolution.
// CHECK-L: { LabeledRefinement.C.P($Heap_at_0, $Heap, this, $as#n0#0) }

abstract module Base {
  predicate F(n: nat) { n >= 0 }

  lemma L() {
    var n: nat :| F(n);
  }
}

module Refinement refines Base { }
abstract module AbstractRefinement refines Base { }
module FurtherRefinement refines AbstractRefinement { }

module ConcreteBase {
  function Identity(n: nat): nat { n }
  predicate Pair(n: nat, m: nat) { n == m }

  lemma Nested() {
    var n: nat, m: nat :| Pair(Identity(n), m);
  }

  lemma NoCall() {
    var n: nat :| n == 0;
  }
}

module ConcreteRefinement refines ConcreteBase { }

abstract module GenericBase {
  ghost predicate P<T>(x: T) { true }
  lemma Generic<T>(w: T) {
    var x: T {:trigger P(x)} :| P(x);
  }
}
module GenericRefinement refines GenericBase { }
abstract module LabeledBase {
  class C {
    var value: int
    twostate predicate P(n: int) reads this { old(value) <= n }
    method M() modifies this {
      value := value + 1;
      label L:
      value := value + 1;
      ghost var n: int :| P@L(n);
    }
  }
}
module LabeledRefinement refines LabeledBase {
  class C ... {
    method M ... {
      ...;
      assert value == old(value) + 2;
    }
  }
}

abstract module NestedInstanceBase {
  class C {
    function G(): int { 0 }
  }
  predicate P(n: int, m: int) { n == m }
  lemma Test(c: C) {
    var n: int :| P(n, c.G());
  }
}
module NestedInstanceRefinement refines NestedInstanceBase { }
