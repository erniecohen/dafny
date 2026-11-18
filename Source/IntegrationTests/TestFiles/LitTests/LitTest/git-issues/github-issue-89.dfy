// RUN: %exits-with 4 %verify --inherited-trait-axioms "%s" > "%t"
// RUN: %exits-with 4 %verify --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %verify "%s" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %audit --inherited-trait-axioms "%S/Inputs/github-issue-89-audit.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms "%S/Inputs/github-issue-89-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-89-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms "%S/Inputs/github-issue-89-refinement-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-89-refinement-rejected.dfy" >> "%t"
// RUN: %resolve "%S/Inputs/github-issue-89-refinement-option-off.dfy" >> "%t"
// RUN: %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-89-refinement-option-off.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --inherited-trait-axioms "%S/Inputs/github-issue-89-refinement-option-off.dfy" >> "%t"
// RUN: %exits-with 4 %verify --inherited-trait-axioms --general-traits=full "%S/Inputs/github-issue-89-general-traits.dfy" >> "%t"
// RUN: %exits-with 2 %verify --general-traits=full "%S/Inputs/github-issue-89-general-traits.dfy" >> "%t"
// RUN: %run --inherited-trait-axioms "%S/Inputs/github-issue-89-run.dfy" >> "%t"
// RUN: %run --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-89-run.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --inherited-trait-axioms "%S/Inputs/github-issue-89-library.dfy" --output "%t-with.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --inherited-trait-axioms --library "%t-with.doo" "%S/Inputs/github-issue-89-client.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --inherited-trait-axioms --type-system-refresh=false --general-newtypes=false --library "%t-with.doo" "%S/Inputs/github-issue-89-client.dfy" >> "%t"
// RUN: %exits-with 1 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --library "%t-with.doo" "%S/Inputs/github-issue-89-client.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%S/Inputs/github-issue-89-library.dfy" --output "%t-without.doo" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --inherited-trait-axioms --library "%t-without.doo" "%S/Inputs/github-issue-89-client.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination "%S/Inputs/github-issue-89-modular-termination-library.dfy" --output "%t-mt.doo" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --library "%t-mt.doo" "%S/Inputs/github-issue-89-modular-termination-client.dfy" >> "%t"
// RUN: %exits-with 1 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --inherited-trait-axioms --library "%t-mt.doo" "%S/Inputs/github-issue-89-modular-termination-client.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Feature (https://github.com/erniecohen/dafny/issues/89): under --inherited-trait-axioms, a bodiless ghost
// trait member marked {:axiom} is a trait axiom.  A class, datatype or newtype that inherits it need not
// implement it; its specification is a declared axiom of the trait, as it already is for callers through
// the trait.  An override is still checked.  Without the option, each class below that does not implement
// such a member is rejected, as before (the third and fourth RUN lines).
//
// Each use of an inherited axiom below has a control that ends in "assert false" and must fail: the
// axiom gives its own postcondition and nothing more.  The one exception is the issue's own program,
// whose axiom is "ensures false".
//
// The last three RUN lines: under --modular-termination a library and its client agree on
// --inherited-trait-axioms, so a library built without it loads for a client that does not use the
// option and is refused for one that does.

// The issue's program.  With "ensures false", a call proves false, and only a call does.
module Issue {
  trait T {
    lemma {:axiom} foo() ensures false
  }
  class C extends T {
    lemma {:axiom} bar() ensures false
  }
  lemma UsesTheAxiom(c: C) ensures false { c.foo(); }
  lemma Control(c: C) { assert false; }  // error
}

// A lemma, used through the class and through the trait.
module Lemma {
  trait T {
    ghost function F(): int
    lemma {:axiom} Positive() ensures F() > 0
  }
  class C extends T {
    ghost function F(): int { 7 }
  }
  lemma ThroughClass(c: C) ensures c.F() > 0 { c.Positive(); }
  lemma ThroughTrait(t: T) ensures t.F() > 0 { t.Positive(); }
  lemma Control(c: C) { c.Positive(); assert c.F() > 0; assert false; }  // error
  lemma NotWithoutACall(t: T) ensures t.F() > 0 { }  // error: the axiom is a lemma, not a fact
}

// Ghost functions and predicates, a ghost method, a two-state lemma and a two-state function.
module GhostMembers {
  trait T extends object {
    var data: int
    ghost function {:axiom} Rank(): nat
      ensures Rank() < 10
    ghost predicate {:axiom} Good(x: int)
      ensures Good(x) ==> x != 0
    ghost method {:axiom} Witness() returns (w: int)
      ensures Good(w)
    twostate lemma {:axiom} Grows()
      ensures old(data) <= data
    twostate function {:axiom} Delta(): int
      reads this
      ensures Delta() == data - old(data)
  }
  class C extends T {
    constructor () { data := 0; }
  }
  lemma Function(c: C) ensures c.Rank() < 10 { }
  lemma Predicate(c: C, x: int) requires c.Good(x) ensures x != 0 { }
  ghost method Method(c: C) ensures exists w :: c.Good(w) { var w := c.Witness(); }
  method TwoState(c: C)
    modifies c
  {
    var before := c.data;
    c.data := *;
    c.Grows();
    assert before <= c.data;
    assert c.Delta() == c.data - before;
    assert false;  // error
  }
  lemma ControlFunction(c: C) { assert c.Rank() < 10; assert false; }  // error
  lemma ControlPredicate(c: C, x: int) requires c.Good(x) { assert x != 0; assert false; }  // error
  ghost method ControlMethod(c: C) { var w := c.Witness(); assert c.Good(w); assert false; }  // error
  lemma NotStronger(c: C) ensures c.Rank() < 9 { }  // error
}

// The signature of a function axiom is a premise too: this result type is empty.  'dafny audit' says so
// (see the audit below).
module EmptyResult {
  type Empty = x: int | false witness *
  trait T {
    ghost function {:axiom} Get(): Empty
  }
  class C extends T { }
  lemma UsesTheAxiom(c: C) ensures false { var e := c.Get(); }
  lemma Control(c: C) { assert false; }  // error
}

// Generic traits: the axiom is inherited at the parent's type arguments.
module Generic {
  trait T<X> extends object {
    ghost function Size(x: X): nat
    lemma {:axiom} SizePositive(x: X) ensures Size(x) > 0
  }
  class C extends T<int> {
    ghost function Size(x: int): nat { if x < 0 then -x else x }
  }
  class D<Y> extends T<seq<Y>> {
    ghost function Size(s: seq<Y>): nat { |s| }
  }
  // The axiom is false for C, and nothing checks that: it is a premise, which 'dafny audit' lists.
  lemma AtInt(c: C) ensures false { c.SizePositive(0); assert c.Size(0) == 0; }
  lemma AtSeq<Y>(d: D<Y>, s: seq<Y>) ensures s != [] { d.SizePositive(s); }
  lemma ControlAtInt(c: C) { c.SizePositive(1); assert c.Size(1) > 0; assert false; }  // error
  lemma ControlAtSeq<Y>(d: D<Y>, s: seq<Y>) { d.SizePositive(s); assert |s| > 0; assert false; }  // error
}

// A diamond: one axiom, reached along two paths, is inherited once.
module Diamond {
  trait T<X> {
    ghost predicate P(x: X)
    lemma {:axiom} Holds(x: X) ensures P(x)
  }
  trait Left extends T<int> { }
  trait Right extends T<int> { }
  class C extends Left, Right {
    ghost predicate P(x: int) { x == x }
  }
  lemma Use(c: C) ensures c.P(3) { c.Holds(3); }
  lemma Control(c: C) { c.Holds(3); assert c.P(3); assert false; }  // error
}

// A subtrait redeclares a member that classes must implement as an axiom, with a stronger specification.
// Classes below the subtrait are exempt, and classes below the parent alone still implement it.
module Subtrait {
  trait T {
    ghost function F(): int
    lemma Bound() ensures F() >= 0
  }
  trait U extends T {
    lemma {:axiom} Bound() ensures F() >= 1
  }
  class BelowU extends U {
    ghost function F(): int { 5 }
  }
  class BelowT extends T {
    ghost function F(): int { 0 }
    lemma Bound() ensures F() >= 0 { }
  }
  lemma Use(c: BelowU) ensures c.F() >= 1 { c.Bound(); }
  lemma UseAsT(c: BelowU) ensures c.F() >= 0 { var t: T := c; t.Bound(); }
  lemma Control(c: BelowU) { c.Bound(); assert c.F() >= 1; assert false; }  // error
}

// An override of a trait axiom is checked as any override is.
module Override {
  trait T {
    ghost function F(): int
    lemma {:axiom} Positive() ensures F() > 0
    ghost function {:axiom} G(): int ensures G() > 0
  }
  class Proves extends T {
    ghost function F(): int { 3 }
    lemma Positive() ensures F() > 0 { }
    ghost function G(): int ensures G() > 0 { 1 }
  }
  class DoesNotProve extends T {
    ghost function F(): int { 0 }
    lemma Positive() ensures F() > 0 { }  // error: the postcondition does not hold
    ghost function G(): int ensures G() > 0 { 0 }  // error: the postcondition does not hold
  }
  class Weaker extends T {
    ghost function F(): int { 0 }
    lemma Positive() ensures F() >= 0 { }  // error: weaker than the trait's postcondition
  }
}

// Refinement.  An inherited axiom stays an axiom (Same).  A refinement may give it a body, which is then
// verified once, for every value of the trait (Proved, NotProved), or strengthen it, which is a new
// premise that 'dafny audit' lists at the refining declaration (Stronger).
module Base {
  trait T {
    ghost function F(): nat
    lemma {:axiom} NonNegative() ensures F() >= 0
    lemma {:axiom} Positive() ensures F() > 0
  }
  class C extends T {
    ghost function F(): nat { 2 }
  }
}
module Same refines Base {
  class D extends T {
    ghost function F(): nat { 1 }
  }
  lemma Use(c: C, d: D) ensures c.F() > 0 && d.F() > 0 { c.Positive(); d.Positive(); }
  lemma Control(d: D) { d.Positive(); assert false; }  // error
}
module Proved refines Same {
  trait T ... {
    lemma NonNegative... { }
  }
}
module NotProved refines Same {
  trait T ... {
    lemma Positive... { }  // error: the body does not prove the postcondition
  }
}
module Stronger refines Base {
  trait T ... {
    lemma Positive... ensures F() > 1
  }
  lemma Use(c: C) ensures c.F() > 1 { c.Positive(); }
  lemma Control(c: C) { c.Positive(); assert c.F() > 1; assert false; }  // error
}

// Static members, members with a body, and {:extern} members are as before: {:axiom} on them does not
// make a trait axiom, a body is still verified, and a compiled one is not rejected.
module Unchanged {
  trait T {
    static lemma {:axiom} Static() ensures 0 < 1
    static method {:axiom} StaticCompiled() returns (r: int) ensures r > 0
    lemma {:axiom} WithBody() ensures 0 < 1 { }
    lemma {:axiom} WithWrongBody() ensures 1 < 0 { }  // error: a body is verified, also with {:axiom}
    method {:axiom} CompiledWithBody() returns (r: int) ensures r > 0 { r := 1; }
    method {:extern} {:axiom} Extern() returns (r: int) ensures r > 0
  }
  class C extends T { }
}
