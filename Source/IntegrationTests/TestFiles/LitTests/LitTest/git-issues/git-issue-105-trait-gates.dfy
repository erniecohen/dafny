// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the invariants of traits, and of
// the classes, datatypes, abstract types and newtypes that extend them, are verified in every module.
// Until stage 6 of section 7.2 of docs/dev/issue105-member-invariants-plan.md, a newtype that extended
// a trait with invariants stopped the verification of the whole program, the abstract type and the
// refining module below included.  That gate is gone, so every module here is verified, and each has a
// negative control: a value of "raw Small" converted to G is checked against Small's header
// constraint, and a constructor in the refining module must establish the inherited invariant.

module Newtypes {
  trait G {
    const k: int := 1
    invariant k != 0
  }
  newtype Small extends G = x: int | 0 <= x < 10
  type Opaque extends G

  method UseSmall(s: Small, o: Opaque) {
    var g: G := s;
    assert g.k != 0;
    var h: G := o;
    assert h.k != 0;
  }

  ghost method RawSmall(r: raw Small) {
    var g: G := r; // error: r need not satisfy Small's constraint
  }
}

abstract module A {
  trait P extends object {
    const n: int
    invariant n > 0
  }
}
module B refines A {
  class C extends P {
    constructor () { n := 1; }
  }
  class D extends P {
    constructor () { n := 0; } // error: the invariant of P does not hold
  }
}
