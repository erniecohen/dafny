// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the invariants of traits, and of
// the classes and datatypes that extend them, are verified, but not yet those of a newtype or an
// abstract type that extends a trait with invariants, nor those of a trait or a class in a refining
// module (stages 5 and 6 of section 7.2 of docs/dev/issue105-member-invariants-plan.md).  No module of
// a program that declares one is verified.

module Newtypes {
  trait G {
    const k: int := 1
    invariant k != 0
  }
  newtype Small extends G = x: int | 0 <= x < 10
  type Opaque extends G
  type Hidden {
    invariant true
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
}
