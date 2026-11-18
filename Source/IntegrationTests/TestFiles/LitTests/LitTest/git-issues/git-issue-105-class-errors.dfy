// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of classes, rejected by the
// resolver: the examples of section 6.2 of docs/dev/issue105-member-invariants-plan.md, and the rules
// of section 3.4.  An invariant reads only consts and functions that read nothing; a class with an
// invariant, its own or inherited, declares a constructor; a type test of a raw reference is ghost.
// (That a class declares no witness is git-issue-105-class-witness.dfy.)  Section 3.3 rests on the
// existing restriction that a function body, a lambda's in a method included, quantifies over a
// class type only with a bound.

module ReadsMutable {
  class C {
    var v: int
    invariant v >= 0                                   // error: reads a mutable field
    constructor () { v := 0; }
  }
}

module MaxModule {
  class Max {
    const x: int
    invariant forall c: Max :: c.x <= x                // error: depends on the allocated objects
    constructor () { x := 0; }
  }
}

module BoxModule {
  trait Shape extends object { predicate Fits(n: int) }
  class Box {
    const s: Shape
    const n: int
    invariant s.Fits(n)                                // error: 'Fits' is dynamically bound
    constructor (s: Shape, n: int) requires s.Fits(n) { this.s := s; this.n := n; }
  }
}

module Constructors {
  class NoCtor {
    const k: int
    invariant k > 0                                    // error: no constructor
  }
  trait T extends object {
    const j: int
    invariant j > 0
  }
  class Inherits extends T { }                         // error: no constructor, for an inherited invariant
  class Fine {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }
}

module TypeTests {
  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }
  method RawTest(r: raw Cell, r': raw Cell?) returns (b: bool, b': bool) {
    b := r is Cell;                                    // error: ghost
    b' := r' is Cell?;                                 // error: ghost
  }
  // from 'object', a test of the dynamic type alone, since every Cell satisfies the invariant
  method Test(o: object, o': object?, c': Cell?, r': raw Cell?) returns (b: bool, b': bool, b'': bool, n: bool) {
    b := o is Cell;
    b' := o' is Cell?;
    b'' := c' is Cell;
    n := r' is raw Cell;
  }
}

module Lambdas {
  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }
  method Lambda() {
    var f := () => forall c: Cell :: c.k > 0;          // error: depends on the allocated objects
    var g := () => exists c: Cell :: c.k == 2;         // error: depends on the allocated objects
  }
  function Unbounded(): bool { forall c: Cell :: c.k > 0 }  // error: depends on the allocated objects
}
