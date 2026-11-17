// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the invariants of traits, and of
// the classes, datatypes and abstract types that extend them, are verified, in any module, but not yet
// those of a newtype that extends a trait with invariants (stage 6 of section 7.2 of
// docs/dev/issue105-member-invariants-plan.md).  No module of a program that declares one is verified,
// so neither the abstract type below nor the refining module is.

module Newtypes {
  trait G {
    const k: int := 1
    invariant k != 0
  }
  newtype Small extends G = x: int | 0 <= x < 10
  type Opaque extends G
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
}
