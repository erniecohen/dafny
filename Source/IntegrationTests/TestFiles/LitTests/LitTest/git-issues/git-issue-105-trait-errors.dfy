// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of traits, rejected by the
// resolver.  An invariant applies to 'this' only members that are raw, its own or inherited (section
// 4.1 of docs/dev/issue105-member-invariants-plan.md).  The paradoxes of its section 3.3 cannot be
// built through a trait: an invariant may not use a dynamically bound member, and may not test or
// convert a raw value against its own type or an ancestor.  A test of a general trait's value against
// a datatype's type is ghost, since such a value may be raw there
// (git-issue-105-general-traits.dfy); one against a class's type, or a reference trait's, from a type
// that is not raw tests the dynamic type alone.  (A trait declares no witness:
// git-issue-105-trait-witness.dfy.)  A datatype that extends traits, none of which has invariants, has
// none, so it needs no witness.

module NotRaw {
  trait Account extends object {
    const balance: int
    invariant balance >= 0
    function Half(): int { balance / 2 }
  }
  class Savings extends Account {
    const rate: int
    invariant Half() >= 0 && rate > 0                  // error: 'Half' is not raw
    constructor (b: int, r: int) requires b >= 0 && r > 0 { balance := b; rate := r; }
  }
}

// the invariant of the trait calls an override that tests the type itself
module Liar {
  trait T {
    predicate No()
    invariant No()                                     // error: 'No' is dynamically bound
  }
  datatype L extends T = L {
    predicate No() { !(this is T) }
  }
}

// the invariant of the datatype calls a member through its trait
module Liar2 {
  trait T {
    function F(): bool
  }
  datatype L extends T = L {
    function F(): bool { true }
    invariant !(this as T).F()                         // error: 'F' is dynamically bound; converts a raw value to 'T'
  }
}

// the invariant of the datatype tests itself against its trait
module Liar3 {
  trait T {
    const b: bool
    invariant b
  }
  datatype L extends T = L {
    // error, twice: 'P', which the invariant uses, converts a raw value to 'raw T' and tests it against 'T'
    ghost predicate P() { (this as raw T) is T }
    invariant P()
  }
}

module TypeTests {
  trait Shape {
    const n: int := 1
    invariant n > 0
  }
  datatype Square extends Shape = Square(s: int) {
    invariant s >= 0
  }
  method General(sh: Shape) returns (b: bool, b': bool) {
    b := sh is Square;                                 // error: not run-time checkable
    b' := sh is raw Square;
  }
  trait P extends object {
    const k: int
    invariant k > 0
  }
  class C extends P {
    constructor () { k := 1; }
  }
  method Reference(p: P, o: object, o': object?, r: raw P) returns (b: bool, b': bool, b'': bool, b''': bool) {
    b := p is C;
    b' := o is P;
    b'' := o' is P?;
    b''' := r is raw C;
  }
  method RawReference(r: raw P) returns (b: bool) {
    b := r is P;                                       // error: not run-time checkable
  }
}

module Witness {
  trait T {
    const k: int := 1
  }
  datatype D extends T = D(x: int) {
    witness D(0)                                       // error: D neither declares nor inherits an invariant
  }
}
