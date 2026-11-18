// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of reference traits and the
// classes that extend them: section 4.1 of docs/dev/issue105-member-invariants-plan.md, the vacuity
// control V3, and the rule of its section 4.4 that each ancestor's invariants are proved once.  A
// class's invariant is the conjunction of its ancestors' and its own, checked at 'new;' and assumed of
// every value of a trait type.  'raw C' is a subtype of 'raw P' for a parent trait 'P' with invariants.
// A raw reference reaches 'P', 'object' or a trait without invariants only as a finished value of its
// own type, so that conversion checks all of 'C's invariants, the inherited ones included.

module Accounts {
  trait Account extends object {
    const balance: int
    invariant balance >= 0
  }
  class Savings extends Account {
    const rate: int
    invariant rate > 0
    constructor (b: int, r: int) requires b >= 0 && r > 0 { balance := b; rate := r; }
  }
  class Plain extends Account {
    constructor () { balance := 0; }
  }
  class Overdrawn extends Account {
    constructor () { balance := -1; }                  // error: the inherited invariant, at the implicit 'new;'
  }

  // V3: a trait, with inheritance
  method V3(a: Account) {
    assert false;                                      // error
  }

  // assumed of values of the trait and of its classes, possibly-null ones and collection elements included
  method Assumed(a: Account, s: Savings, p: Plain, q: Account?, st: set<Account>) {
    assert a.balance >= 0;
    assert s.balance >= 0 && s.rate > 0;
    assert p.balance >= 0;
    assert q != null ==> q.balance >= 0;
    assert forall x <- st :: x.balance >= 0;
    var a': Account := s;
    var o: object := a;
    var st': set<Account> := {s, p};
  }
  method Dynamic(o: object) {
    if o is Account {
      assert (o as Account).balance >= 0;
    }
    if o is Savings {
      assert (o as Savings).rate > 0;
    }
  }

  // a raw Savings reaches Account only as a Savings, and a raw Account only as an Account
  method RawToTrait(r: raw Savings) {
    var a: Account := r;                               // error: each of Savings's two invariants
  }
  method RawToObject(r: raw Savings?) {
    var o: object? := r;                               // error: each of Savings's two invariants
  }
  method RawToRawTrait(r: raw Savings, r': raw Savings?) returns (x: raw Account, x': raw Account?) {
    x, x' := r, r';
  }
  method RawTrait(x: raw Account) {
    var a: Account := x;                               // error
  }
  method RawChecked(r: raw Savings, x: raw Account)
    requires r.balance >= 0 && r.rate > 0 && x.balance >= 0
  {
    var a: Account := r;
    var o: object := r;
    var a': Account := x;
  }
}

// Each ancestor's invariants once: a constructor of 'C' proves 'Top's invariant once, though 'C'
// reaches 'Top' twice
module Diamond {
  trait Top extends object {
    const t: int
    invariant t > 0
  }
  trait L extends Top {
    invariant t > 1
  }
  trait R extends Top {
    invariant t > 2
  }
  class C extends L, R {
    constructor (t: int) { this.t := t; }              // error: each of the three invariants, once
  }
  class Good extends L, R {
    constructor () { t := 3; }
  }
  method M(c: C, l: L) {
    assert c.t > 2 && l.t > 1;
    var top: Top := c;
    var r: R := c;
  }
}

// A class may extend a trait with invariants of another module
module Lib {
  trait P extends object {
    const n: int
    invariant n > 0
  }
}
module Client {
  import Lib
  @AssumeCrossModuleTermination
  class C extends Lib.P {
    const m: int
    invariant m > n
    constructor () { n := 1; m := 2; }
  }
  method M(c: C) {
    assert c.m > 1;
    var p: Lib.P := c;
    assert p.n > 0;
  }
  method R(r: raw C) {
    var p: Lib.P := r;                                 // error: each of C's two invariants
  }
}

// A member that is not raw, applied to a raw reference, checks the receiver against the finished type of
// the reference's own type, all its invariants included; a raw member and a stored field do not
module Receivers {
  trait Plain extends object {
    const z: int
    function Z(): int { z }
  }
  trait Account extends object {
    const balance: int
    invariant balance >= 0
    function Half(): int { balance / 2 }
    function Bal(): int { balance }
    invariant Bal() >= 0                               // so Bal is raw
  }
  class Savings extends Account, Plain {
    const rate: int
    invariant rate > 0
    constructor (b: int, r: int) requires b >= 0 && r > 0 { balance := b; rate := r; z := 0; }
  }
  method RawMember(r: raw Savings) returns (b: int, q: int, k: int) {
    b := r.Bal();
    q := r.z;
    k := r.rate;
  }
  method NotRaw(r: raw Savings) returns (h: int) {
    h := r.Half();                                     // error: each of Savings's three invariants
  }
  method OfAnotherParent(r: raw Savings) returns (z: int) {
    z := r.Z();                                        // error: each of Savings's three invariants
  }
  method OfTheTrait(a: raw Account) returns (h: int) {
    h := a.Half();                                     // error: each of Account's two invariants
  }
  method Checked(r: raw Savings) returns (h: int, z: int)
    requires r.balance >= 0 && r.rate > 0
  {
    h := r.Half();
    z := r.Z();
  }
}

// Two references whose least common supertype is a trait with invariants join at that trait if neither
// is raw, and at its raw type otherwise
module Joins {
  trait P extends object {
    const n: int
    invariant n > 0
  }
  class C extends P {
    constructor () { n := 1; }
  }
  class D extends P {
    const m: int
    invariant m > n
    constructor () { n := 1; m := 2; }
  }
  method Finished(c: C, d: D, b: bool) {
    var x := if b then c else d;                       // a P
    var y: P := x;
    var s := {c, d};                                   // a set<P>
    var t: set<P> := s;
  }
  method FinishedQ(c: C?, d: D?, b: bool) {
    var x := if b then c else d;                       // a P?
    var y: P? := x;
  }
  method Raw(c: raw C, d: D, b: bool) {
    var x := if b then c else d;                       // a raw P
    var o: object := x;                                // error: P's invariant
  }
  method RawQ(c: raw C?, d: raw D?, b: bool) {
    var x := if b then c else d;                       // a raw P?
    var s: set<object?> := {x};                        // error
  }
  method RawChecked(c: raw C, d: D, b: bool) requires c.n > 0 {
    var x := if b then c else d;                       // a raw P, which P's invariant alone makes a P
    var o: object := x;
  }
}
