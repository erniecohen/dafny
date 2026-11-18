// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%S/Inputs/git-issue-105-refines-library.dfy" --library "%S/Inputs/git-issue-105-refinement-library.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) verified in refining modules:
// the seventh step of section 10 of docs/dev/issue105-member-invariants-plan.md.  Inherited code was
// verified in its module, where each refined declaration had only the invariants declared so far.
// So where inherited code makes a value of a type whose invariants a refining module adds to, the
// invariants declared where the code was verified are assumed, and the added ones are checked, with
// the refining module as a related location.  A member that an added invariant uses becomes raw, and
// its inherited body is verified again.  A witness of a refining declaration replaces the inherited
// one, and is checked against all the invariants.  Code inherited from a library is checked too
// (Inputs/git-issue-105-refines-library.dfy).

abstract module Bank0 {
  datatype Account = Account(balance: int) {
    invariant balance >= 0
  }
  function Deposit(a: Account, n: nat): Account {
    Account(a.balance + n)                             // error in Bank1: the invariant it adds
  }
  function Withdraw(a: Account, n: nat): Account {
    Account(a.balance - n)                             // error here, and not again in Bank1
  }
  function Clear(a: Account): Account { Account(0) }
}
module Bank1 refines Bank0 {
  datatype Account ... {
    invariant balance < 100
  }
  // the inherited invariant and the added one are assumed of a value of the type
  lemma Range(a: Account) ensures 0 <= a.balance < 100 { }
}
// adds no invariant, so no inherited code is verified again
module Bank2 refines Bank0 { }

// Classes and traits: checked at the end of each inherited constructor's first phase
abstract module Cells0 {
  class Cell {
    const k: int
    invariant k > 0
    constructor (n: nat) { k := n + 1; }               // error in Cells1, at the implicit 'new;'
  }
  trait Named extends object {
    const name: string
    invariant |name| > 0
  }
  class Thing extends Named {
    constructor () {
      name := "a long name";
      new;                                             // error in Cells1: the invariant added to the trait
    }
  }
  class Short extends Named {
    constructor () { name := "abc"; }
  }
}
module Cells1 refines Cells0 {
  class Cell ... {
    invariant k < 10
  }
  trait Named ... {
    invariant |name| < 5
  }
  lemma Both(c: Cell, t: Named) ensures 0 < c.k < 10 && 0 < |t.name| < 5 { }
}

// An abstract import that a refining module binds to a refinement of its signature: the inherited code
// was verified against the invariants of the signature, so those the binding adds are checked
abstract module Lib0 {
  datatype D = D(x: int) {
    invariant x >= 0
  }
}
module Lib1 refines Lib0 {
  datatype D ... {
    invariant x < 10
  }
}
abstract module User0 {
  import L : Lib0
  function Make(): L.D { L.D(20) }                     // error in User1
  function Small(): L.D { L.D(2) }
}
module User1 refines User0 {
  import L = Lib1
}

// A refining witness replaces the inherited one; an inherited witness is checked against the added invariants
abstract module W0 {
  datatype E = E(x: int) {
    invariant x > 0
    witness E(5)                                       // error in W2
  }
}
module W1 refines W0 {
  datatype E ... {
    invariant x < 3
    witness E(1)
  }
}
module W2 refines W0 {
  datatype E ... {
    invariant x < 3
  }
}

// A member used by an added invariant becomes raw, so its inherited body is verified again, and
// so is the postcondition of an inherited by-method body
abstract module R0 {
  datatype N = N(x: int) {
    invariant x != 0
    function F(): int { 10 / x }                       // error in R1: 'this' is raw there
    function G(): int { 10 / x }
    function H(): int { if x != 0 then 1 else 2 } by method { return 1; }  // error in R1
  }
}
module R1 refines R0 {
  datatype N ... {
    invariant F() > 0
    invariant H() == 1
  }
}

// A check that is not split into the conjuncts of the invariants is made whole
abstract module S0 {
  datatype P = P(x: int) {
    invariant x > 0
  }
  function All(s: seq<raw P>): seq<P>
    requires forall i :: 0 <= i < |s| ==> s[i].x > 0
  {
    s                                                  // error in S1
  }
}
module S1 refines S0 {
  datatype P ... {
    invariant x < 10
  }
}
